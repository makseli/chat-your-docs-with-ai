using backend_api.Models;

namespace backend_api.Services
{
    public interface IOllamaService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<string> GenerateAnswerAsync(string context, string question);
        Task<bool> IsModelAvailableAsync(string modelName);
        Task<bool> IsConnectedAsync();
        string GetCurrentModelName();
        
        // Vector storage and search
        Task StoreVectorAsync(VectorData vectorData);
        Task<List<VectorResult>> SearchSimilarAsync(string query, int nResults = 5);
        Task<List<VectorResult>> GetVectorsByFileAsync(string fileName);
        Task<VectorStats> GetVectorStatsAsync();
        Task<List<ProcessedFileInfo>> GetProcessedFilesAsync();
    }
}
