using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.services.implementations
{
    public class FileService:IFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly Serilog.ILogger _logger = Log.ForContext<FileService>();

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<List<string>> SaveImageAsync(IFormFileCollection? files)
        {
            if (files == null) return [];

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            List<string> paths = [];

            foreach (var file in files)
            {
                if (file == null) continue;

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                    throw new ArgumentException("Недопустимый формат файла. Разрешены только изображения.");

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

                paths.Add( $"uploads/images/{uniqueFileName}");
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
    }
}
