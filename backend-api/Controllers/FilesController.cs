using Microsoft.AspNetCore.Mvc;
using backend_api.Services;
using backend_api.Models;

namespace backend_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IOllamaService _ollamaService;
        private readonly IRedisService _redisService;

        public FilesController(ILogger<FilesController> logger, IOllamaService ollamaService, IRedisService redisService)
        {
            _logger = logger;
            _ollamaService = ollamaService;
            _redisService = redisService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(FilesResponse), 200)]
        [ProducesResponseType(typeof(FilesResponse), 500)]
        public async Task<IActionResult> GetFiles()
        {
            try
            {
                // Ollama'dan işlenmiş dosyaları al
                var processedFiles = await _ollamaService.GetProcessedFilesAsync();
                
                // Redis'ten log'ları al ve dosya durumlarını kontrol et
                var logs = await _redisService.GetRecentLogsAsync(100);
                
                // Dosya durumlarını güncelle
                var filesWithStatus = new List<ProcessedFileInfo>();
                
                // İşlenmiş dosyaları ekle
                filesWithStatus.AddRange(processedFiles);
                
                // Log'lardan yüklenmiş ama henüz işlenmemiş dosyaları bul
                var uploadedFiles = logs
                    .Where(log => log.Event == "FILE_UPLOADED" && !string.IsNullOrEmpty(log.FileName))
                    .Select(log => new ProcessedFileInfo
                    {
                        FileName = log.FileName,
                        FileSize = log.FileSize ?? 0,
                        ProcessedAt = log.Timestamp,
                        ChunkCount = 0,
                        Status = "Queued"
                    })
                    .Where(f => !processedFiles.Any(pf => pf.FileName == f.FileName))
                    .ToList();
                
                filesWithStatus.AddRange(uploadedFiles);
                
                // Hata durumundaki dosyaları bul
                var errorFiles = logs
                    .Where(log => log.Event == "FILE_PROCESSING_ERROR" && !string.IsNullOrEmpty(log.FileName))
                    .Select(log => new ProcessedFileInfo
                    {
                        FileName = log.FileName,
                        FileSize = log.FileSize ?? 0,
                        ProcessedAt = log.Timestamp,
                        ChunkCount = 0,
                        Status = "Error"
                    })
                    .ToList();
                
                // Hata durumundaki dosyaları güncelle
                foreach (var errorFile in errorFiles)
                {
                    var existingFile = filesWithStatus.FirstOrDefault(f => f.FileName == errorFile.FileName);
                    if (existingFile != null)
                    {
                        existingFile.Status = "Error";
                    }
                    else
                    {
                        filesWithStatus.Add(errorFile);
                    }
                }
                
                // Dosyaları tarihe göre sırala
                filesWithStatus = filesWithStatus
                    .OrderByDescending(f => f.ProcessedAt)
                    .ToList();

                var response = new FilesResponse
                {
                    Success = true,
                    Files = filesWithStatus,
                    TotalCount = filesWithStatus.Count,
                    RetrievedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Dosya listesi istendi: {Count} dosya döndürüldü", filesWithStatus.Count);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya listesi alınırken hata oluştu");

                var errorResponse = new FilesResponse
                {
                    Success = false,
                    Files = new List<ProcessedFileInfo>(),
                    TotalCount = 0,
                    RetrievedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };

                return StatusCode(500, errorResponse);
            }
        }

        [HttpGet("{fileName}")]
        [ProducesResponseType(typeof(VectorResult[]), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetFileVectors(string fileName)
        {
            try
            {
                var vectors = await _ollamaService.GetVectorsByFileAsync(fileName);
                
                if (vectors.Count == 0)
                {
                    return NotFound(new { message = $"No vectors found for file: {fileName}" });
                }

                _logger.LogInformation("Dosya vector'ları istendi: {FileName} için {Count} vector", fileName, vectors.Count);

                return Ok(vectors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya vector'ları alınırken hata oluştu: {FileName}", fileName);
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }
    }
}
