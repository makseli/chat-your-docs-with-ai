namespace backend_api.Models
{
    public class ProcessedFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ProcessedAt { get; set; }
        public int ChunkCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class FilesResponse
    {
        public bool Success { get; set; }
        public List<ProcessedFileInfo> Files { get; set; } = new();
        public int TotalCount { get; set; }
        public DateTime RetrievedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
