using backend_api.Models;

namespace backend_api.Services
{
    public interface IRagService
    {
        Task<QuestionResponse> AnswerQuestionAsync(QuestionRequest request);
        Task<List<SearchResult>> SearchDocumentsAsync(string query);
        Task<bool> IsSystemReadyAsync();
    }
}
