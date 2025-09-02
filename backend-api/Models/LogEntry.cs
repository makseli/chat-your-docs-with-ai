namespace backend_api.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Event { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long? FileSize { get; set; }
        public string? Error { get; set; }
    }

    public class LogsResponse
    {
        public bool Success { get; set; }
        public int TotalCount { get; set; }
        public List<LogEntry> Logs { get; set; } = new();
        public DateTime RetrievedAt { get; set; }
    }
}
