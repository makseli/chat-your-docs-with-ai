using Microsoft.AspNetCore.Mvc;
using backend_api.Services;
using backend_api.Models;

namespace backend_api.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly ILogger<UploadController> _logger;
        private readonly IRedisService _redisService;
        private readonly IFileStorageService _fileStorageService;

        public UploadController(ILogger<UploadController> logger, IRedisService redisService, IFileStorageService fileStorageService)
        {
            _logger = logger;
            _redisService = redisService;
            _fileStorageService = fileStorageService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(UploadResponse), 200)]
        [ProducesResponseType(typeof(UploadResponse), 400)]
        [ProducesResponseType(typeof(UploadResponse), 500)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                // File storage service ile dosyayı yükle
                var uploadResult = await _fileStorageService.UploadFileAsync(file);
                
                if (!uploadResult.Success)
                {
                    var errorResponse = new UploadResponse
                    {
                        Success = false,
                        Message = "Dosya yükleme başarısız",
                        Timestamp = DateTime.UtcNow,
                        Error = uploadResult.ErrorMessage
                    };
                    
                    _logger.LogWarning("Dosya yükleme başarısız: {Error}", uploadResult.ErrorMessage);
                    return BadRequest(errorResponse);
                }

                _logger.LogInformation("Dosya başarıyla yüklendi: {FileName} - Storage: {StorageType}", 
                    uploadResult.FileName, uploadResult.StorageType);

                // Duplicate dosya ise Redis kuyruğa ekleme
                bool queueResult = false;
                string queueStatus = "Duplicate dosya, kuyruğa eklenmedi";
                bool redisConnected = false;

                if (!uploadResult.IsDuplicate)
                {
                    // Sadece yeni dosyalar için Redis kuyruğa ekle
                    queueResult = await _redisService.EnqueueFileAsync(uploadResult.FileName!, uploadResult.FilePath!);
                    redisConnected = await _redisService.IsConnectedAsync();
                    queueStatus = queueResult ? "Redis kuyruğuna eklendi" : "Local queue'ya eklendi (Redis bağlantısı yok)";
                }
                else
                {
                    // Duplicate dosya için Redis durumunu kontrol et
                    redisConnected = await _redisService.IsConnectedAsync();
                }

                var message = uploadResult.IsDuplicate 
                    ? "Aynı dosya zaten mevcut, duplicate olarak işaretlendi"
                    : "Dosya başarıyla yüklendi ve kuyruğa eklendi";

                var successResponse = new UploadResponse
                {
                    Success = true,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    File = new FileDetails
                    {
                        FileName = uploadResult.FileName!,
                        FilePath = uploadResult.FilePath!,
                        FileSize = uploadResult.FileSize,
                        StorageType = uploadResult.StorageType,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        UploadTime = DateTime.UtcNow,
                        FileHash = uploadResult.FileHash,
                        IsDuplicate = uploadResult.IsDuplicate,
                        DuplicateOf = uploadResult.DuplicateOf
                    },
                    Queue = new QueueInfo
                    {
                        Queued = queueResult,
                        Status = queueStatus,
                        RedisConnected = redisConnected
                    }
                };

                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yükleme sırasında beklenmeyen hata oluştu");
                
                var errorResponse = new UploadResponse
                {
                    Success = false,
                    Message = "Dosya yükleme sırasında bir hata oluştu",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                };

                return StatusCode(500, errorResponse);
            }
        }
    }
}
