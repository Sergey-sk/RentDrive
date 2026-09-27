using RentDrive.endpoints;
using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive
{
    public class FileDeleteWorker : BackgroundService
    {
        private readonly IFileDeleteQueue _deleteQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly Serilog.ILogger _logger = Log.ForContext<FileDeleteWorker>();

        public FileDeleteWorker(IFileDeleteQueue deleteQueue, IServiceProvider serviceProvider)
        {
            _deleteQueue = deleteQueue;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if(_deleteQueue.TryDequeue(out var filePaths) && filePaths != null)
                    {
                        using var scope = _serviceProvider.CreateScope();

                        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();

                        fileService.RemoveImage(filePaths);
                    }
                    else
                    {
                        await Task.Delay(3000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка в фоновом воркере удаления файлов");
                }
            }
        }
    }
}
