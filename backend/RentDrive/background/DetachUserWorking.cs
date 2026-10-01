using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.background
{
    public class DetachUserWorking : BackgroundService
    {
        private readonly IDeleteQueue<string> _deleteQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly Serilog.ILogger _logger = Log.ForContext<DetachUserWorking>();

        public DetachUserWorking(IDeleteQueue<string> deleteQueue, IServiceProvider serviceProvider)
        {
            _deleteQueue = deleteQueue;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("Воркер удаления пользователя запущен.");

            try
            {
                await foreach(var userId in _deleteQueue.Reader.ReadAllAsync())
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        await context.Bookings
                            .Where(b => b.CustomerId == userId)
                            .ExecuteUpdateAsync(s => s.SetProperty(b => b.CustomerId, (string?)null));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Ошибка при удалении пользователя {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Ошибка фонового воркера удаления пользователей.");
            }
            finally
            {
                _logger.Information("Воркер удаления пользователей завершил работу.");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _deleteQueue.Complete();
            return base.StopAsync(cancellationToken);
        }
    }
}
