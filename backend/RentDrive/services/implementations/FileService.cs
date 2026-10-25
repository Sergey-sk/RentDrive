using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.services.implementations
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly Serilog.ILogger _logger = Log.ForContext<FileService>();
        private readonly Dictionary<string, byte[]> _imgSignatures = new()
        {
            { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
            { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
            { ".webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 } }
        };

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<List<string>> SaveImageAsync(IFormFileCollection? files)
        {
            if (files == null) return [];

            List<string> paths = [];

            foreach (var file in files)
            {
                if (file == null) continue;

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!await IsValidImage(file)) return [];

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string randSuffix = Guid.NewGuid().ToString("N")[..5];

                var uniqueFileName = $"{timestamp}_{randSuffix}{extension}";

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "images");

                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fs);
                }

                paths.Add($"uploads/images/{uniqueFileName}");
            }

            return paths;
        }

        public async Task RemoveImageAsync(List<string> imgUrls)
        {
            await Task.Run(() =>
            {
                foreach (var imgUrl in imgUrls)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(imgUrl)) continue;

                        string fulPath = Path.Combine(_env.WebRootPath, imgUrl);

                        if (File.Exists(fulPath)) File.Delete(fulPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Не удалось удалить файл: {Path}", imgUrl);
                    }
                }
            });
        }

        private async Task<bool> IsValidImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_imgSignatures.ContainsKey(extension))
                return false;

            var expectedSignature = _imgSignatures[extension];

            using var stream = file.OpenReadStream();
            byte[] buffer = new byte[expectedSignature.Length];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            if(bytesRead < expectedSignature.Length)
                return false;

            if(extension == ".webp")
            {
                return buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
                               buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50;
            }

            return buffer.SequenceEqual(expectedSignature);
        }
    }
}
