using Microsoft.AspNetCore.Mvc;
using backend_api.Services;
using backend_api.Models;

namespace backend_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionController : ControllerBase
    {
        private readonly ILogger<QuestionController> _logger;
        private readonly IRagService _ragService;
        private readonly IOllamaService _ollamaService;

        public QuestionController(ILogger<QuestionController> logger, IRagService ragService, IOllamaService ollamaService)
        {
            _logger = logger;
            _ragService = ragService;
            _ollamaService = ollamaService;
        }

        [HttpPost("ask")]
        [ProducesResponseType(typeof(QuestionResponse), 200)]
        [ProducesResponseType(typeof(QuestionResponse), 400)]
        [ProducesResponseType(typeof(QuestionResponse), 500)]
        public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Question))
                {
                    return BadRequest(new QuestionResponse
                    {
                        Success = false,
                        Answer = "Soru boş olamaz.",
                        Error = "Question is required"
                    });
                }

                var response = await _ragService.AnswerQuestionAsync(request);
                
                if (response.Success)
                {
                    _logger.LogInformation("Soru başarıyla yanıtlandı: {Question} - {ProcessingTime}s", 
                        request.Question, response.ProcessingTime);
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning("Soru yanıtlanamadı: {Question} - {Error}", 
                        request.Question, response.Error);
                    return StatusCode(500, response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Soru yanıtlama hatası: {Question}", request.Question);
                
                var errorResponse = new QuestionResponse
                {
                    Success = false,
                    Answer = "Üzgünüm, sorunuzu yanıtlarken bir hata oluştu.",
                    Error = ex.Message
                };
                
                return StatusCode(500, errorResponse);
            }
        }

        [HttpPost("search")]
        [ProducesResponseType(typeof(List<SearchResult>), 200)]
        [ProducesResponseType(typeof(List<SearchResult>), 400)]
        public async Task<IActionResult> SearchDocuments([FromBody] SearchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Query))
                {
                    return BadRequest(new List<SearchResult>());
                }

                var results = await _ragService.SearchDocumentsAsync(request.Query);
                
                _logger.LogInformation("Doküman arama tamamlandı: {Query} - {Count} sonuç", 
                    request.Query, results.Count);
                
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Doküman arama hatası: {Query}", request.Query);
                return StatusCode(500, new List<SearchResult>());
            }
        }

        [HttpGet("stats")]
        [ProducesResponseType(typeof(VectorStats), 200)]
        public async Task<IActionResult> GetVectorStats()
        {
            try
            {
                var stats = await _ollamaService.GetVectorStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector stats hatası");
                return StatusCode(500, new VectorStats { IsConnected = false });
            }
        }

        [HttpPost("vector")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> AddVector([FromBody] VectorData vectorData)
        {
            try
            {
                await _ollamaService.StoreVectorAsync(vectorData);
                _logger.LogInformation("Vector eklendi: {Id}", vectorData.Id);
                return Ok(new { success = true, message = "Vector added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vector ekleme hatası: {Id}", vectorData.Id);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("status")]
        [ProducesResponseType(typeof(SystemStatusResponse), 200)]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                var isReady = await _ragService.IsSystemReadyAsync();
                
                var response = new SystemStatusResponse
                {
                    IsReady = isReady,
                    Timestamp = DateTime.UtcNow,
                    Services = new Dictionary<string, bool>
                    {
                        ["RAG"] = isReady,
                        ["ChromaDB"] = await _ragService.IsSystemReadyAsync(), // Bu daha detaylı olabilir
                        ["Ollama"] = await _ragService.IsSystemReadyAsync()
                    }
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sistem durumu kontrol hatası");
                
                return Ok(new SystemStatusResponse
                {
                    IsReady = false,
                    Timestamp = DateTime.UtcNow,
                    Services = new Dictionary<string, bool>
                    {
                        ["RAG"] = false,
                        ["ChromaDB"] = false,
                        ["Ollama"] = false
                    }
                });
            }
        }
    }

    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class SystemStatusResponse
    {
        public bool IsReady { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, bool> Services { get; set; } = new();
    }
}
