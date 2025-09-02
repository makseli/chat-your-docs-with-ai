using backend_api.Models;

namespace backend_api.Services
{
    public interface IRedisService
    {
        Task<bool> IsConnectedAsync();
        Task<bool> EnqueueFileAsync(string fileName, string filePath);
        Task<string?> DequeueFileAsync();
        void StartConnectionRetry();
        void StopConnectionRetry();
        Task LogEventAsync(string level, string eventType, string message, string? details = null, string? fileName = null, string? filePath = null, long? fileSize = null, string? error = null);
        Task<List<LogEntry>> GetRecentLogsAsync(int count = 50);
    }
}
