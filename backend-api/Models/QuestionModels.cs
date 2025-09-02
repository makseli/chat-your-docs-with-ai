namespace backend_api.Models
{
    public class QuestionRequest
    {
        public string Question { get; set; } = string.Empty;
        public int? MaxResults { get; set; } = 5;
        public double? MinRelevance { get; set; } = 0.5;
    }

    public class QuestionResponse
    {
        public bool Success { get; set; }
        public string Answer { get; set; } = string.Empty;
        public List<SourceInfo> Sources { get; set; } = new();
        public double Confidence { get; set; }
        public double ProcessingTime { get; set; }
        public int ContextLength { get; set; }
        public int RetrievedChunks { get; set; }
        public string? Error { get; set; }
    }

    public class SourceInfo
    {
        public string FileName { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public double Relevance { get; set; }
        public string ChunkText { get; set; } = string.Empty;
    }

    public class SearchResult
    {
        public string FileName { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public double Relevance { get; set; }
        public string ChunkText { get; set; } = string.Empty;
        public int ChunkSize { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class VectorResult
    {
        public string Id { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;
        public double Distance { get; set; }
        public VectorMetadata Metadata { get; set; } = new();
    }

    public class VectorMetadata
    {
        public string JobId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int ChunkSize { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    public class CollectionStats
    {
        public string CollectionName { get; set; } = string.Empty;
        public long TotalDocuments { get; set; }
        public bool IsConnected { get; set; }
    }

    public class VectorData
    {
        public string Id { get; set; } = string.Empty;
        public string Document { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public VectorMetadata Metadata { get; set; } = new();
    }

    public class VectorStats
    {
        public int TotalVectors { get; set; }
        public int UniqueFiles { get; set; }
        public bool IsConnected { get; set; }
    }
}
