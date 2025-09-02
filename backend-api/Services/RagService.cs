using backend_api.Models;

namespace backend_api.Services
{
    public class RagService : IRagService
    {
        private readonly IOllamaService _ollamaService;
        private readonly ILogger<RagService> _logger;

        public RagService(
            IOllamaService ollamaService,
            ILogger<RagService> logger)
        {
            _ollamaService = ollamaService;
            _logger = logger;
        }

        public async Task<QuestionResponse> AnswerQuestionAsync(QuestionRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Soru işleniyor: {Question}", request.Question);

                // 1. Question embedding oluştur
                var questionEmbedding = await _ollamaService.GenerateEmbeddingAsync(request.Question);
                
                // 2. Vector search - benzer chunk'ları bul
                var similarVectors = await _ollamaService.SearchSimilarAsync(request.Question, request.MaxResults ?? 5);
                
                if (!similarVectors.Any())
                {
                    return new QuestionResponse
                    {
                        Success = false,
                        Answer = "Üzgünüm, sorunuzla ilgili bilgi bulamadım. Lütfen farklı bir soru deneyin.",
                        Sources = new List<SourceInfo>(),
                        Confidence = 0.0,
                        ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds
                    };
                }

                // 3. Context oluştur
                var context = BuildContext(similarVectors);
                
                // 4. LLM ile cevap oluştur
                var answer = await _ollamaService.GenerateAnswerAsync(context, request.Question);
                
                // 5. Confidence hesapla (distance'a göre)
                var confidence = CalculateConfidence(similarVectors);
                
                // 6. Source bilgilerini hazırla
                var sources = similarVectors.Select(v => new SourceInfo
                {
                    FileName = v.Metadata.FileName,
                    ChunkIndex = v.Metadata.ChunkIndex,
                    Relevance = Math.Max(0, 1 - v.Distance), // Distance'ı relevance'a çevir
                    ChunkText = v.Document.Length > 200 ? v.Document.Substring(0, 200) + "..." : v.Document
                }).ToList();

                var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;

                _logger.LogInformation("Soru yanıtlandı: {Question} - {ProcessingTime}s", request.Question, processingTime);

                return new QuestionResponse
                {
                    Success = true,
                    Answer = answer,
                    Sources = sources,
                    Confidence = confidence,
                    ProcessingTime = processingTime,
                    ContextLength = context.Length,
                    RetrievedChunks = similarVectors.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Soru yanıtlama hatası: {Question}", request.Question);
                
                return new QuestionResponse
                {
                    Success = false,
                    Answer = "Üzgünüm, sorunuzu yanıtlarken bir hata oluştu. Lütfen daha sonra tekrar deneyin.",
                    Sources = new List<SourceInfo>(),
                    Confidence = 0.0,
                    ProcessingTime = (DateTime.UtcNow - startTime).TotalSeconds,
                    Error = ex.Message
                };
            }
        }

        public async Task<List<SearchResult>> SearchDocumentsAsync(string query)
        {
            try
            {
                _logger.LogInformation("Doküman arama: {Query}", query);

                var similarVectors = await _ollamaService.SearchSimilarAsync(query, 10);
                
                var searchResults = similarVectors.Select(v => new SearchResult
                {
                    FileName = v.Metadata.FileName,
                    ChunkIndex = v.Metadata.ChunkIndex,
                    Relevance = Math.Max(0, 1 - v.Distance),
                    ChunkText = v.Document,
                    ChunkSize = v.Metadata.ChunkSize,
                    CreatedAt = v.Metadata.CreatedAt
                }).ToList();

                _logger.LogInformation("Doküman arama tamamlandı: {Query} için {Count} sonuç", query, searchResults.Count);
                
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doküman arama hatası: {Query}", query);
                return new List<SearchResult>();
            }
        }

        public async Task<bool> IsSystemReadyAsync()
        {
            try
            {
                var ollamaConnected = await _ollamaService.IsConnectedAsync();
                
                _logger.LogInformation("Sistem durumu: Ollama={OllamaConnected}, Ready={IsReady}", 
                    ollamaConnected, ollamaConnected);
                
                return ollamaConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sistem durumu kontrol hatası");
                return false;
            }
        }

        private string BuildContext(List<VectorResult> vectors)
        {
            var contextBuilder = new System.Text.StringBuilder();
            
            foreach (var vector in vectors.OrderBy(v => v.Metadata.ChunkIndex))
            {
                contextBuilder.AppendLine($"Doküman: {vector.Metadata.FileName}");
                contextBuilder.AppendLine($"Bölüm {vector.Metadata.ChunkIndex + 1}:");
                contextBuilder.AppendLine(vector.Document);
                contextBuilder.AppendLine();
            }
            
            return contextBuilder.ToString().Trim();
        }

        private double CalculateConfidence(List<VectorResult> vectors)
        {
            if (!vectors.Any()) return 0.0;
            
            // En iyi 3 sonucun ortalama relevance'ı
            var topResults = vectors.Take(3);
            var avgRelevance = topResults.Average(v => Math.Max(0, 1 - v.Distance));
            
            return Math.Round(avgRelevance, 2);
        }
    }
}
