using Microsoft.AspNetCore.Mvc;
using backend_api.Services;

namespace backend_api.Controllers
{
    [ApiController]
    [Route("/")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly IRedisService _redisService;
        private readonly IOllamaService _ollamaService;
        private static readonly DateTime _applicationStartTime = DateTime.UtcNow;

        public HealthController(ILogger<HealthController> logger, IRedisService redisService, IOllamaService ollamaService)
        {
            _logger = logger;
            _redisService = redisService;
            _ollamaService = ollamaService;
        }

        [HttpGet]
        [HttpOptions]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var uptime = DateTime.UtcNow - _applicationStartTime;
                var redisStatus = await CheckRedisConnection();
                var ollamaStatus = await CheckOllamaConnection();
                var pythonWorkerStatus = await CheckPythonWorkerStatus();
                var frontendStatus = await CheckFrontendStatus();

                var redisConnected = ((dynamic)redisStatus).isConnected;
                var ollamaConnected = ((dynamic)ollamaStatus).isConnected;
                var pythonWorkerActive = ((dynamic)pythonWorkerStatus).isActive;
                var frontendRunning = ((dynamic)frontendStatus).isRunning;
                var allServicesHealthy = redisConnected && ollamaConnected && pythonWorkerActive && frontendRunning;

                var healthInfo = new
                {
                    status = allServicesHealthy ? "healthy" : "degraded",
                    uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    services = new
                    {
                        redis = redisStatus,
                        ollama = ollamaStatus,
                        pythonWorker = pythonWorkerStatus,
                        frontend = frontendStatus
                    }
                };

                _logger.LogInformation("Health check requested - Status: {Status}, Uptime: {Uptime}", 
                    healthInfo.status, healthInfo.uptime);

                return Ok(healthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check sırasında hata oluştu.");
                
                var uptime = DateTime.UtcNow - _applicationStartTime;
                var errorResponse = new
                {
                    status = "unhealthy",
                    error = ex.Message,
                    uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                return StatusCode(500, errorResponse);
            }
        }

        private async Task<object> CheckRedisConnection()
        {
            try
            {
                var isConnected = await _redisService.IsConnectedAsync();
                
                if (isConnected)
                {
                    return new
                    {
                        status = "connected",
                        isConnected = true
                    };
                }
                else
                {
                    return new
                    {
                        status = "disconnected",
                        isConnected = false,
                        message = "Redis bağlantısı yok, 10 saniye aralıklarla yeniden deneme yapılıyor"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis bağlantı kontrolü başarısız.");
                return new
                {
                    status = "disconnected",
                    error = ex.Message,
                    isConnected = false
                };
            }
        }

        private async Task<object> CheckOllamaConnection()
        {
            try
            {
                var isConnected = await _ollamaService.IsConnectedAsync();
                var currentChatModel = _ollamaService.GetCurrentModelName();
                var embeddingModelAvailable = await _ollamaService.IsModelAvailableAsync("nomic-embed-text");
                var chatModelAvailable = await _ollamaService.IsModelAvailableAsync(currentChatModel);
                
                if (isConnected && embeddingModelAvailable && chatModelAvailable)
                {
                    return new
                    {
                        status = "connected",
                        isConnected = true,
                        embeddingModel = "nomic-embed-text",
                        chatModel = currentChatModel,
                        modelsAvailable = true
                    };
                }
                else
                {
                    return new
                    {
                        status = "degraded",
                        isConnected = isConnected,
                        embeddingModel = embeddingModelAvailable ? "nomic-embed-text" : "not available",
                        chatModel = chatModelAvailable ? currentChatModel : "not available",
                        modelsAvailable = embeddingModelAvailable && chatModelAvailable,
                        message = "Ollama bağlantısı var ama modeller eksik"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama bağlantı kontrolü başarısız.");
                return new
                {
                    status = "disconnected",
                    isConnected = false,
                    error = ex.Message,
                    modelsAvailable = false
                };
            }
        }

        private async Task<object> CheckPythonWorkerStatus()
        {
            try
            {
                // Redis'te son Python worker log'unu kontrol et
                var recentLogs = await _redisService.GetRecentLogsAsync(10);
                var workerLogs = recentLogs.Where(log => 
                    log.Event == "PYTHON_WORKER_STARTED" || 
                    log.Event == "PYTHON_WORKER_HEARTBEAT" ||
                    log.Event == "FILE_PROCESSING_COMPLETED" || 
                    log.Event == "FILE_PROCESSING_ERROR").ToList();

                if (workerLogs.Any())
                {
                    var lastWorkerActivity = workerLogs.Max(log => log.Timestamp);
                    var timeSinceLastActivity = DateTime.UtcNow - lastWorkerActivity;
                    
                    // Son 5 dakika içinde aktivite varsa aktif kabul et
                    var isActive = timeSinceLastActivity.TotalMinutes < 5;
                    
                    return new
                    {
                        status = isActive ? "active" : "inactive",
                        isActive = isActive,
                        lastActivity = lastWorkerActivity.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        timeSinceLastActivity = $"{timeSinceLastActivity.TotalMinutes:F1} minutes ago",
                        totalLogs = workerLogs.Count
                    };
                }
                else
                {
                    return new
                    {
                        status = "unknown",
                        isActive = false,
                        message = "Python worker log'u bulunamadı"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Python worker durum kontrolü başarısız.");
                return new
                {
                    status = "unknown",
                    isActive = false,
                    error = ex.Message
                };
            }
        }

        private async Task<object> CheckFrontendStatus()
        {
            try
            {
                // Frontend Docker servisinin çalışıp çalışmadığını kontrol et
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await httpClient.GetAsync("http://rag_frontend:80");
                
                if (response.IsSuccessStatusCode)
                {
                    return new
                    {
                        status = "running",
                        isRunning = true,
                        port = 80,
                        message = "Frontend servisi çalışıyor"
                    };
                }
                else
                {
                    return new
                    {
                        status = "error",
                        isRunning = false,
                        port = 80,
                        message = $"Frontend servisi hata döndürüyor: {response.StatusCode}"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Frontend servisi bağlantı hatası.");
                return new
                {
                    status = "unreachable",
                    isRunning = false,
                    port = 80,
                    message = "Frontend servisine bağlanılamıyor"
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Frontend servisi timeout.");
                return new
                {
                    status = "timeout",
                    isRunning = false,
                    port = 80,
                    message = "Frontend servisi yanıt vermiyor"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frontend durum kontrolü başarısız.");
                return new
                {
                    status = "error",
                    isRunning = false,
                    error = ex.Message
                };
            }
        }
    }
}
