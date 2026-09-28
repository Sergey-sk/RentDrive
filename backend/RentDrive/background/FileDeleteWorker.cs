using RentDrive.endpoints;
using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.background
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
            _logger.Information("Воркер удаления файлов запущен.");

            try
            {
                await foreach(var filePaths in _deleteQueue.Reader.ReadAllAsync())
                {
                    try
                    {
                        if (filePaths == null || filePaths.Count == 0) continue;

                        using var scope = _serviceProvider.CreateScope();
                        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();

                        await fileService.RemoveImageAsync(filePaths);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Ошибка при удалении пакета файлов: {@FilePaths}.", filePaths);
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.Fatal(ex, "Ошибка фонового воркера удаления файлов.");
            }
            finally
            {
                _logger.Information("Воркер удаления файлов завершил работу.");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _deleteQueue.Complete();
            return base.StopAsync(cancellationToken);
        }
    }
}
