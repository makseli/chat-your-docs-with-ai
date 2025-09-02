namespace backend_api.Services
{
    public interface IFileStorageService
    {
        Task<FileUploadResult> UploadFileAsync(IFormFile file);
        Task<bool> DeleteFileAsync(string fileName);
        Task<FileInfo?> GetFileInfoAsync(string fileName);
        Task<Stream?> GetFileStreamAsync(string fileName);
        Task<IEnumerable<FileInfo>> ListFilesAsync();
    }

    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long FileSize { get; set; }
        public string? ErrorMessage { get; set; }
        public string StorageType { get; set; } = string.Empty;
        public bool IsDuplicate { get; set; } = false;
        public string? DuplicateOf { get; set; }
        public string? FileHash { get; set; }
    }
}
