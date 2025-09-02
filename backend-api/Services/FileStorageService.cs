using Microsoft.AspNetCore.Hosting;

namespace backend_api.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileStorageService> _logger;
        private readonly IRedisService _redisService;
        private readonly string _uploadsPath;

        public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger, IRedisService redisService)
        {
            _environment = environment;
            _logger = logger;
            _redisService = redisService;
            // Docker container'da /app/data/uploaded_files, local'de ise relative path
            _uploadsPath = Path.Combine(_environment.ContentRootPath, "data", "uploaded_files");
            
            // Uploads dizinini oluştur
            if (!Directory.Exists(_uploadsPath))
            {
                Directory.CreateDirectory(_uploadsPath);
                _logger.LogInformation("Upload dizini oluşturuldu: {UploadsPath}", _uploadsPath);
            }
            else
            {
                _logger.LogInformation("Upload dizini mevcut: {UploadsPath}", _uploadsPath);
            }
        }

        public async Task<FileUploadResult> UploadFileAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new FileUploadResult
                    {
                        Success = false,
                        ErrorMessage = "Dosya seçilmedi veya dosya boş."
                    };
                }

                // Dosya adını güvenli hale getir
                var fileName = Path.GetFileName(file.FileName);
                var baseName = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                
                // Dosya hash'ini hesapla (duplicate kontrolü için)
                var fileHash = await CalculateFileHashAsync(file);
                
                // Aynı hash'li dosya var mı kontrol et
                var existingFile = await FindFileByHashAsync(fileHash);
                if (existingFile != null)
                {
                    _logger.LogInformation("Aynı dosya zaten mevcut: {ExistingFile} (Hash: {Hash})", 
                        existingFile.Name, fileHash);
                    
                    await _redisService.LogEventAsync("INFO", "FILE_DUPLICATE", "Aynı dosya zaten mevcut", 
                        fileName: existingFile.Name, filePath: existingFile.FullName, fileSize: existingFile.Length, details: $"Hash: {fileHash}");
                    
                    return new FileUploadResult
                    {
                        Success = true,
                        FileName = existingFile.Name,
                        FilePath = existingFile.FullName,
                        FileSize = existingFile.Length,
                        StorageType = "FileStorage",
                        IsDuplicate = true,
                        DuplicateOf = existingFile.Name,
                        FileHash = fileHash
                    };
                }

                // Özel karakterleri temizle ve güvenli dosya adı oluştur
                var sanitizedBaseName = SanitizeFileName(baseName);
                var safeFileName = $"{sanitizedBaseName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";

                var filePath = Path.Combine(_uploadsPath, safeFileName);

                // Dosyayı kaydet
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Hash'i dosya adına ekle (metadata olarak)
                var hashFileName = $"{Path.GetFileNameWithoutExtension(safeFileName)}_{fileHash.Substring(0, 8)}{extension}";
                var hashFilePath = Path.Combine(_uploadsPath, hashFileName);
                
                // Hash'li dosya adıyla kopya oluştur (opsiyonel)
                File.Copy(filePath, hashFilePath, false);

                _logger.LogInformation("Dosya başarıyla yüklendi: {FileName}", safeFileName);
                await _redisService.LogEventAsync("INFO", "FILE_UPLOADED", "Dosya başarıyla yüklendi", fileName: safeFileName, filePath: filePath, fileSize: file.Length);

                return new FileUploadResult
                {
                    Success = true,
                    FileName = safeFileName,
                    FilePath = filePath,
                    FileSize = file.Length,
                    StorageType = "FileStorage",
                    FileHash = fileHash,
                    IsDuplicate = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya yükleme sırasında hata oluştu.");
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_uploadsPath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Dosya silindi: {FileName}", fileName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya silme sırasında hata oluştu: {FileName}", fileName);
                return false;
            }
        }

        public async Task<FileInfo?> GetFileInfoAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_uploadsPath, fileName);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya bilgisi alınırken hata oluştu: {FileName}", fileName);
                return null;
            }
        }

        public async Task<Stream?> GetFileStreamAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_uploadsPath, fileName);
                if (File.Exists(filePath))
                {
                    return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya stream'i alınırken hata oluştu: {FileName}", fileName);
                return null;
            }
        }

        public async Task<IEnumerable<FileInfo>> ListFilesAsync()
        {
            try
            {
                if (Directory.Exists(_uploadsPath))
                {
                    var directoryInfo = new DirectoryInfo(_uploadsPath);
                    return directoryInfo.GetFiles().OrderByDescending(f => f.CreationTime);
                }
                return Enumerable.Empty<FileInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya listesi alınırken hata oluştu");
                return Enumerable.Empty<FileInfo>();
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "file";

            // Geçersiz karakterleri temizle
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            
            // Boşlukları alt çizgi ile değiştir
            sanitized = sanitized.Replace(" ", "_");
            
            // Birden fazla alt çizgiyi tek alt çizgi yap
            while (sanitized.Contains("__"))
            {
                sanitized = sanitized.Replace("__", "_");
            }
            
            // Başında ve sonunda alt çizgi varsa temizle
            sanitized = sanitized.Trim('_');
            
            // Eğer boş kaldıysa default isim ver
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "file";
            
            // Maksimum uzunluk kontrolü (255 karakter)
            if (sanitized.Length > 200)
                sanitized = sanitized.Substring(0, 200);
            
            return sanitized;
        }

        private async Task<string> CalculateFileHashAsync(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private async Task<FileInfo?> FindFileByHashAsync(string fileHash)
        {
            try
            {
                if (!Directory.Exists(_uploadsPath))
                    return null;

                var files = Directory.GetFiles(_uploadsPath);
                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    var existingHash = await CalculateFileHashFromPathAsync(filePath);
                    
                    if (existingHash.Equals(fileHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return fileInfo;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya hash kontrolü sırasında hata oluştu");
                return null;
            }
        }

        private async Task<string> CalculateFileHashFromPathAsync(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
