namespace backend_api.Models
{
    public class UploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public FileDetails? File { get; set; }
        public QueueInfo? Queue { get; set; }
        public string? Error { get; set; }
    }

    public class FileDetails
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string StorageType { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadTime { get; set; }
        public string? FileHash { get; set; }
        public bool IsDuplicate { get; set; } = false;
        public string? DuplicateOf { get; set; }
    }

    public class QueueInfo
    {
        public bool Queued { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool RedisConnected { get; set; }
        public string? Error { get; set; }
    }
}
