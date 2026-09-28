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
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.Information("Проверка целостности изображений.");

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

                    var pageSize = 100;
                    var page = 0;
                    bool hasMoreItems = true;
                    var totalFixed = 0;

                    while (hasMoreItems)
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        var currentChunk = await context.RentItems
                            .OrderBy(i => i.Id)
                            .Skip(page * pageSize)
                            .Take(pageSize)
                            .ToListAsync(stoppingToken);

                        if(currentChunk.Count == 0)
                        {
                            hasMoreItems = false;
                            break;
                        }

                        bool chunkChanged = false;

                        foreach(var item in currentChunk)
                        {
                            var validUrls = new List<string>();

                            foreach(var imgUrl in item.ImageUrls)
                            {
                                var fullPath = Path.Combine(env.WebRootPath, imgUrl);

                                if (File.Exists(fullPath))
                                    validUrls.Add(imgUrl);
                                else
                                {
                                    _logger.Warning("Обнаружена ссылка на несуществующее изображение в объекте Id {ItemId}: {Path}", item.Id, imgUrl);
                                    chunkChanged = true;
                                }
                            }

                            if(item.ImageUrls.Count != validUrls.Count)
                            {
                                item.ImageUrls = validUrls;
                                totalFixed++;
                            }
                        }

                        if (chunkChanged)
                            await context.SaveChangesAsync();

                        page++;

                        _logger.Information("Проверка зваершена. Исправлено объявлений: {Count}.", totalFixed);
                    }
                }
                catch(OperationCanceledException ex)
                {
                    _logger.Information("Проверка целостности прервана по сигналу отмены.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка очистки бд.");
                }
            }
        }
    }
}
