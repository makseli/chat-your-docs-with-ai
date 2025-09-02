using StackExchange.Redis;
using System.Collections.Concurrent;
using backend_api.Models;
using System.Text.Json;

namespace backend_api.Services
{
    public class RedisService : IRedisService, IDisposable
    {
        private readonly ILogger<RedisService> _logger;
        private readonly string _connectionString;
        private readonly string _queueName = "file_processing_queue";
        private readonly string _logsListName = "application_logs";
        private readonly ConcurrentQueue<string> _localQueue = new();
        private IConnectionMultiplexer? _connection;
        private Timer? _retryTimer;
        private bool _disposed = false;
        private readonly object _lockObject = new();

        public RedisService(ILogger<RedisService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6379";
            
            // İlk bağlantı denemesini yap
            _ = Task.Run(InitializeConnectionAsync);
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                if (_connection == null || !_connection.IsConnected)
                    return false;

                var database = _connection.GetDatabase();
                await database.PingAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EnqueueFileAsync(string fileName, string filePath)
        {
            try
            {
                // Önce Redis'e bağlanmaya çalış
                if (await IsConnectedAsync())
                {
                    var database = _connection!.GetDatabase();
                    var fileInfo = new
                    {
                        fileName = fileName,
                        filePath = filePath,
                        timestamp = DateTime.UtcNow,
                        id = Guid.NewGuid().ToString()
                    };
                    
                    var queueLength = await database.ListLeftPushAsync(_queueName, System.Text.Json.JsonSerializer.Serialize(fileInfo));
                    _logger.LogInformation("Dosya Redis kuyruğuna eklendi: {FileName}", fileName);
                    await LogEventAsync("INFO", "FILE_QUEUED", "Dosya Redis kuyruğuna eklendi", 
                        details: $"Queue Length: {queueLength}, Job ID: {fileInfo.id}", 
                        fileName: fileName, filePath: filePath);
                    return true;
                }
                else
                {
                    // Redis bağlantısı yoksa local queue'ya ekle
                    var localJobId = Guid.NewGuid().ToString();
                    _localQueue.Enqueue(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        fileName = fileName,
                        filePath = filePath,
                        timestamp = DateTime.UtcNow,
                        id = localJobId
                    }));
                    
                    _logger.LogWarning("Redis bağlantısı yok, dosya local queue'ya eklendi: {FileName}", fileName);
                    await LogEventAsync("WARNING", "FILE_QUEUED_LOCAL", "Redis bağlantısı yok, dosya local queue'ya eklendi", 
                        details: $"Local Queue Count: {_localQueue.Count}, Job ID: {localJobId}", 
                        fileName: fileName, filePath: filePath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya kuyruğa eklenirken hata oluştu: {FileName}", fileName);
                
                // Hata durumunda da local queue'ya ekle
                var errorJobId = Guid.NewGuid().ToString();
                _localQueue.Enqueue(System.Text.Json.JsonSerializer.Serialize(new
                {
                    fileName = fileName,
                    filePath = filePath,
                    timestamp = DateTime.UtcNow,
                    id = errorJobId
                }));
                
                await LogEventAsync("ERROR", "FILE_QUEUE_ERROR", "Dosya kuyruğa eklenirken hata oluştu", 
                    details: $"Error Job ID: {errorJobId}, Local Queue Count: {_localQueue.Count}", 
                    fileName: fileName, filePath: filePath, error: ex.Message);
                
                return false;
            }
        }

        public async Task<string?> DequeueFileAsync()
        {
            try
            {
                if (await IsConnectedAsync())
                {
                    var database = _connection!.GetDatabase();
                    var result = await database.ListRightPopAsync(_queueName);
                    return result.HasValue ? result.ToString() : null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis'ten dosya alınırken hata oluştu");
            }
            
            return null;
        }

        public void StartConnectionRetry()
        {
            if (_retryTimer != null)
                return;

            _retryTimer = new Timer(async _ => await TryReconnectAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            _logger.LogInformation("Redis bağlantı yeniden deneme timer'ı başlatıldı (10 saniye aralıklarla)");
        }

        public void StopConnectionRetry()
        {
            _retryTimer?.Dispose();
            _retryTimer = null;
            _logger.LogInformation("Redis bağlantı yeniden deneme timer'ı durduruldu");
        }

        private async Task InitializeConnectionAsync()
        {
            await TryReconnectAsync();
            StartConnectionRetry();
        }

        private async Task TryReconnectAsync()
        {
            try
            {
                if (_connection != null && _connection.IsConnected)
                    return;

                lock (_lockObject)
                {
                    if (_connection != null)
                    {
                        _connection.Dispose();
                    }
                }

                _connection = await ConnectionMultiplexer.ConnectAsync(_connectionString);
                _connection.ConnectionFailed += OnConnectionFailed;
                _connection.ConnectionRestored += OnConnectionRestored;

                _logger.LogInformation("Redis bağlantısı başarıyla kuruldu");
                await LogEventAsync("INFO", "REDIS_CONNECTED", "Redis bağlantısı başarıyla kuruldu");

                // Local queue'daki dosyaları Redis'e aktar
                await ProcessLocalQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis bağlantısı kurulamadı, 10 saniye sonra tekrar denenecek");
                await LogEventAsync("WARNING", "REDIS_CONNECTION_FAILED", "Redis bağlantısı kurulamadı", error: ex.Message);
            }
        }

        private async Task ProcessLocalQueueAsync()
        {
            try
            {
                var database = _connection!.GetDatabase();
                var processedCount = 0;

                while (_localQueue.TryDequeue(out var item))
                {
                    await database.ListLeftPushAsync(_queueName, item);
                    processedCount++;
                }

                if (processedCount > 0)
                {
                    _logger.LogInformation("{Count} dosya local queue'dan Redis'e aktarıldı", processedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Local queue işlenirken hata oluştu");
            }
        }

        private async void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
        {
            _logger.LogWarning("Redis bağlantısı kesildi: {Reason}", e.FailureType);
            await LogEventAsync("WARNING", "REDIS_DISCONNECTED", "Redis bağlantısı kesildi", details: e.FailureType.ToString());
        }

        private async void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
        {
            _logger.LogInformation("Redis bağlantısı yeniden kuruldu");
            await LogEventAsync("INFO", "REDIS_RECONNECTED", "Redis bağlantısı yeniden kuruldu");
            _ = Task.Run(ProcessLocalQueueAsync);
        }

        public async Task LogEventAsync(string level, string eventType, string message, string? details = null, string? fileName = null, string? filePath = null, long? fileSize = null, string? error = null)
        {
            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Event = eventType,
                    Message = message,
                    Details = details,
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize,
                    Error = error
                };

                var logJson = JsonSerializer.Serialize(logEntry);

                if (await IsConnectedAsync())
                {
                    var database = _connection!.GetDatabase();
                    await database.ListLeftPushAsync(_logsListName, logJson);
                    
                    // Log listesini 1000 kayıtla sınırla
                    await database.ListTrimAsync(_logsListName, 0, 999);
                }
                else
                {
                    // Redis bağlantısı yoksa local log'a yaz
                    _logger.LogInformation("Redis bağlantısı yok, log local'e yazıldı: {Event} - {Message}", eventType, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log yazma sırasında hata oluştu: {Event}", eventType);
            }
        }

        public async Task<List<LogEntry>> GetRecentLogsAsync(int count = 50)
        {
            try
            {
                if (await IsConnectedAsync())
                {
                    var database = _connection!.GetDatabase();
                    var logs = await database.ListRangeAsync(_logsListName, 0, count - 1);
                    
                    var logEntries = new List<LogEntry>();
                    foreach (var log in logs)
                    {
                        try
                        {
                            var logEntry = JsonSerializer.Deserialize<LogEntry>(log.ToString()!);
                            if (logEntry != null)
                                logEntries.Add(logEntry);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Log deserialize hatası");
                        }
                    }
                    
                    return logEntries.OrderByDescending(l => l.Timestamp).ToList();
                }
                else
                {
                    _logger.LogWarning("Redis bağlantısı yok, log listesi alınamadı");
                    return new List<LogEntry>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log listesi alınırken hata oluştu");
                return new List<LogEntry>();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopConnectionRetry();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
