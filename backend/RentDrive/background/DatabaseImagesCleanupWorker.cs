using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using Serilog;

namespace RentDrive.background
{
    public class DatabaseImagesCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Serilog.ILogger _logger = Log.ForContext<DatabaseImagesCleanupWorker>();

        public DatabaseImagesCleanupWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.Information("Проверка целостности изображений.");

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

                    var items = await context.RentItems.ToListAsync(stoppingToken);
                    bool isDbChanged = false;

                    foreach (var item in items)
                    {
                        var validUrls = new List<string>();

                        foreach (var imgUrl in item.ImageUrls)
                        {
                            var fullPath = Path.Combine(env.WebRootPath, imgUrl);

                            if (File.Exists(fullPath))
                                validUrls.Add(imgUrl);
                            else
                            {
                                _logger.Warning("Обнаружена ссылка на несуществующее изображение в объекте Id {ItemId}: {Path}", item.Id, imgUrl);
                                isDbChanged = true;
                            }
                        }

                        if (item.ImageUrls.Count != validUrls.Count)
                            item.ImageUrls = validUrls;
                    }

                    if (isDbChanged)
                    {
                        _logger.Information("Проверка зваершена. Ссылки на несуществующие изображения удалены.");
                        await context.SaveChangesAsync();
                    }
                    else
                        _logger.Information("Проверка завершена. Все изображения валидны.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка очистки бд.");
                }
            }
        }
    }
}
