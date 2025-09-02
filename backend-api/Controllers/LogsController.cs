using Microsoft.AspNetCore.Mvc;
using backend_api.Services;
using backend_api.Models;

namespace backend_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        private readonly ILogger<LogsController> _logger;
        private readonly IRedisService _redisService;

        public LogsController(ILogger<LogsController> logger, IRedisService redisService)
        {
            _logger = logger;
            _redisService = redisService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(LogsResponse), 200)]
        [ProducesResponseType(typeof(LogsResponse), 500)]
        public async Task<IActionResult> GetLogs([FromQuery] int count = 50)
        {
            try
            {
                // Count parametresini sınırla (1-100 arası)
                count = Math.Max(1, Math.Min(100, count));

                var logs = await _redisService.GetRecentLogsAsync(count);
                var redisConnected = await _redisService.IsConnectedAsync();

                var response = new LogsResponse
                {
                    Success = true,
                    TotalCount = logs.Count,
                    Logs = logs,
                    RetrievedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Log listesi istendi: {Count} kayıt döndürüldü, Redis bağlantısı: {RedisConnected}", 
                    logs.Count, redisConnected);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log listesi alınırken hata oluştu");

                var errorResponse = new LogsResponse
                {
                    Success = false,
                    TotalCount = 0,
                    Logs = new List<LogEntry>(),
                    RetrievedAt = DateTime.UtcNow
                };

                return StatusCode(500, errorResponse);
            }
        }
    }
}
