using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using Serilog;

namespace RentDrive.background
{
    public class BookingStatusWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Serilog.ILogger _logger = Log.ForContext<BookingStatusWorker>();

        public BookingStatusWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("Фоновый воркер управления статусам бронирований запущен");

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.Information("Плановое обновление статусов бронирований");

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var today = DateTime.Today;

                    int cancelledCount = await context.Bookings
                        .Where(b => b.Status == db.models.BookingStatus.Pending && b.StartDate <= today)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, db.models.BookingStatus.Cancelled), stoppingToken);

                    int activatedCount = await context.Bookings
                        .Where(b => b.Status == db.models.BookingStatus.Confirmed && b.StartDate <= today)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, db.models.BookingStatus.Active), stoppingToken);

                    int completedCount = await context.Bookings
                        .Where(b => b.Status == db.models.BookingStatus.Active && b.EndDate < today)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, db.models.BookingStatus.Active), stoppingToken);

                    _logger.Information("Обновление завершено. Отклонено: {Cancelled}, Активировано: {Activated}, Завершено: {Completed}",
                        cancelledCount, activatedCount, completedCount);
                }catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка при выполнении фонового обновления статусов бронирований.");
                }
            }
        }
    }
}
