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
        private static readonly DateTime _applicationStartTime = DateTime.UtcNow;

        public HealthController(ILogger<HealthController> logger, IRedisService redisService)
        {
            _logger = logger;
            _redisService = redisService;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var uptime = DateTime.UtcNow - _applicationStartTime;
                var redisStatus = await CheckRedisConnection();

                var healthInfo = new
                {
                    status = "healthy",
                    uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                    services = new
                    {
                        redis = redisStatus
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
                    uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"
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
    }
}
