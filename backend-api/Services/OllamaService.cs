using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend_api.Models;

namespace backend_api.Services
{
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OllamaService> _logger;
        private readonly string _ollamaHost;
        private readonly string _embeddingModel;
        private readonly string _chatModel;
        
        // Simple in-memory vector storage
        private static readonly List<VectorData> _vectorStorage = new();
        private static readonly object _storageLock = new();

        public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _ollamaHost = configuration["OLLAMA_HOST"] ?? "http://ollama:11434";
            _embeddingModel = configuration["EMBEDDING_MODEL"] ?? "nomic-embed-text";
            _chatModel = configuration["CHAT_MODEL"] ?? "llama3.1:8b";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var request = new
                {
                    model = _embeddingModel,
                    prompt = text
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_ollamaHost}/api/embeddings", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseContent);

                if (result?.Embedding != null)
                {
                    _logger.LogDebug("Embedding oluşturuldu: {Text} için {Length} boyut", text, result.Embedding.Length);
                    return result.Embedding;
                }

                throw new InvalidOperationException("Embedding response is null or empty");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding oluşturma hatası: {Text}", text);
                
                // Fallback: mock embedding
                return GenerateMockEmbedding(text);
            }
        }

        public async Task<string> GenerateAnswerAsync(string context, string question)
        {
            try
            {
                var prompt = BuildPrompt(context, question);
                
                var request = new
                {
                    model = _chatModel,
                    prompt = prompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.7,
                        top_p = 0.9,
                        max_tokens = 1000
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_ollamaHost}/api/generate", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseContent);

                if (!string.IsNullOrEmpty(result?.Response))
                {
                    _logger.LogInformation("LLM cevap oluşturuldu: {Question}", question);
                    return result.Response.Trim();
                }

                throw new InvalidOperationException("LLM response is null or empty");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM cevap oluşturma hatası: {Question}", question);
                return "Üzgünüm, şu anda sorunuzu yanıtlayamıyorum. Lütfen daha sonra tekrar deneyin.";
            }
        }

        public async Task<bool> IsModelAvailableAsync(string modelName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ollamaHost}/api/tags");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaTagsResponse>(content);

                // Model ismini kontrol et - tam eşleşme veya tag ile eşleşme
                var isAvailable = result?.Models?.Any(m => 
                    m.Name == modelName || 
                    m.Name == $"{modelName}:latest" ||
                    m.Name.StartsWith($"{modelName}:")
                ) ?? false;
                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model kontrol hatası: {ModelName}", modelName);
                return false;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ollamaHost}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ollama bağlantı kontrol hatası");
                return false;
            }
        }

        public string GetCurrentModelName()
        {
            return _chatModel;
        }

        private string BuildPrompt(string context, string question)
        {
            return $@"Aşağıdaki bağlamı kullanarak soruyu yanıtlayın. Eğer bağlamda yanıt bulamıyorsanız, 'Bağlamda bu bilgi bulunmuyor' yazın.

Bağlam:
{context}

Soru: {question}

Yanıt:";
        }

        private float[] GenerateMockEmbedding(string text)
        {
            // Mock embedding - gerçekte Ollama'dan gelecek
            var random = new Random(text.GetHashCode());
            var embedding = new float[768]; // nomic-embed-text boyutu
            
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(random.NextDouble() * 2 - 1); // -1 ile 1 arası
            }
            
            return embedding;
        }

        public async Task StoreVectorAsync(VectorData vectorData)
        {
            try
            {
                lock (_storageLock)
                {
                    _vectorStorage.Add(vectorData);
                }
                
                _logger.LogInformation("Vector stored: {Id}", vectorData.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector storage hatası: {Id}", vectorData.Id);
            }
        }

        public async Task<List<VectorResult>> SearchSimilarAsync(string query, int nResults = 5)
        {
            try
            {
                var queryEmbedding = await GenerateEmbeddingAsync(query);
                
                lock (_storageLock)
                {
                    var results = _vectorStorage
                        .Select(v => new VectorResult
                        {
                            Id = v.Id,
                            Document = v.Document,
                            Distance = CalculateDistance(queryEmbedding, v.Embedding),
                            Metadata = v.Metadata
                        })
                        .OrderBy(v => v.Distance)
                        .Take(nResults)
                        .ToList();

                    _logger.LogInformation("Vector search tamamlandı: {Query} için {Count} sonuç", query, results.Count);
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector search hatası: {Query}", query);
                return new List<VectorResult>();
            }
        }

        public async Task<List<VectorResult>> GetVectorsByFileAsync(string fileName)
        {
            try
            {
                lock (_storageLock)
                {
                    var results = _vectorStorage
                        .Where(v => v.Metadata.FileName == fileName)
                        .OrderBy(v => v.Metadata.ChunkIndex)
                        .Select(v => new VectorResult
                        {
                            Id = v.Id,
                            Document = v.Document,
                            Distance = 0.0,
                            Metadata = v.Metadata
                        })
                        .ToList();

                    _logger.LogInformation("Dosya vector'ları getirildi: {FileName} için {Count} vector", fileName, results.Count);
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya vector'ları getirme hatası: {FileName}", fileName);
                return new List<VectorResult>();
            }
        }

        public async Task<VectorStats> GetVectorStatsAsync()
        {
            try
            {
                lock (_storageLock)
                {
                    var stats = new VectorStats
                    {
                        TotalVectors = _vectorStorage.Count,
                        UniqueFiles = _vectorStorage.Select(v => v.Metadata.FileName).Distinct().Count(),
                        IsConnected = true
                    };

                    _logger.LogInformation("Vector stats: {TotalVectors} vectors, {UniqueFiles} files", stats.TotalVectors, stats.UniqueFiles);
                    return stats;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector stats hatası");
                return new VectorStats
                {
                    TotalVectors = 0,
                    UniqueFiles = 0,
                    IsConnected = false
                };
            }
        }

        public async Task<List<ProcessedFileInfo>> GetProcessedFilesAsync()
        {
            try
            {
                lock (_storageLock)
                {
                    var files = _vectorStorage
                        .GroupBy(v => v.Metadata.FileName)
                        .Select(g => new ProcessedFileInfo
                        {
                            FileName = g.Key,
                            ChunkCount = g.Count(),
                            FileSize = g.First().Metadata.FileSize,
                            ProcessedAt = g.First().Metadata.ProcessedAt,
                            Status = "Processed"
                        })
                        .OrderByDescending(f => f.ProcessedAt)
                        .ToList();

                    _logger.LogInformation("Processed files listesi getirildi: {Count} dosya", files.Count);
                    return files;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processed files listesi hatası");
                return new List<ProcessedFileInfo>();
            }
        }

        private double CalculateDistance(float[] embedding1, float[] embedding2)
        {
            if (embedding1.Length != embedding2.Length)
                return double.MaxValue;

            double sum = 0;
            for (int i = 0; i < embedding1.Length; i++)
            {
                double diff = embedding1[i] - embedding2[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }
    }

    // Response models
    public class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    public class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;
    }

    public class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = new();
    }

    public class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
