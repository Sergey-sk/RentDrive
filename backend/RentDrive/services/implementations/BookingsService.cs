using Microsoft.EntityFrameworkCore;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.bookingsDto;
using RentDrive.services.interfaces;
using Serilog;

namespace RentDrive.services.implementations
{
    public class BookingsService : IBookingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly Serilog.ILogger _logger = Log.ForContext<BookingsService>();

        public BookingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(List<BookingDto>, int)> GetUserBookingsAsync(string userId, BookingQueryParameters queryParameters)
        {
            var query = _context.Bookings
                .Include(b => b.RentItem)
                .Where(b => b.CustomerId == userId)
                .AsQueryable();

            query = GetSortedList(query, queryParameters);

            var page = queryParameters.Page ?? 1;
            var pageSize = queryParameters.PageSize ?? 10;

            var totalItems = await query.CountAsync();
            var pageCount = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var bookingDtos = pagedItems.Select(b => BookingDto.ToDto(b)).ToList();

            return (bookingDtos, pageCount);
        }

        public async Task<BookingDto?> GetUserBookingByIdAsync(string userId, int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.RentItem)
                .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == userId);

            if (booking == null) return null;

            return BookingDto.ToDto(booking);
        }

        public async Task<(List<BookingDto>, int)> GetBookingRequestsAsync(string ownerId, BookingQueryParameters queryParameters)
        {
            var query = _context.Bookings
                .Include(b => b.RentItem)
                .Where(b => b.RentItem.OwnerId == ownerId)
                .AsQueryable();

            query = GetSortedList(query, queryParameters);

            var page = queryParameters.Page ?? 1;
            var pageSize = queryParameters.PageSize ?? 10;

            var totalItems = await query.CountAsync();
            var pageCount = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedItems = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var bookingDtos = pagedItems.Select(b => BookingDto.ToDto(b)).ToList();

            return (bookingDtos, pageCount);
        }

        public async Task<BookingDto?> GetBookingRequestByIdAsync(int bookingId, string userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.RentItem)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.RentItem.OwnerId == userId);

            if (booking == null) return null;

            return BookingDto.ToDto(booking);
        }

        public async Task<BookingDto> CreateBookingAsync(string userId, int itemId, CreateBookingDto createDto)
        {
            var rentItem = await _context.RentItems
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == itemId) ?? throw new ArgumentException("Объявление не найдено.");

            if (rentItem.OwnerId == userId) throw new ArgumentException("Нельзя забронировать свой товар.");

            bool isOverlap = await _context.Bookings.AnyAsync(b =>
                b.RentItemId == itemId &&
                b.Status != BookingStatus.Cancelled &&
                createDto.StartDate < b.EndDate && createDto.EndDate > b.StartDate);

            if (isOverlap) throw new ArgumentException("Бронь на эти даты недоступна.");

            if (createDto.StartDate < DateTime.Today || createDto.EndDate < createDto.StartDate)
                throw new ArgumentException("Неверная дата бронирования.");

            var totalDays = (decimal)(createDto.EndDate - createDto.StartDate).TotalDays;

            if (totalDays == 0) totalDays = 1;

            var newBooking = new Booking
            {
                StartDate = createDto.StartDate,
                EndDate = createDto.EndDate,
                TotalPrice = totalDays * rentItem.PricePerDay,
                RentItemId = itemId,
                RentItem = rentItem,
                CustomerId = userId
            };

            _context.Bookings.Add(newBooking);
            await _context.SaveChangesAsync();

            return BookingDto.ToDto(newBooking);
        }

        public async Task<bool> DeleteBookingAsync(string userId, int id)
        {
            return await _context.Bookings
                .Where(b => b.Id == id
                       && b.CustomerId == userId
                       && b.Status == BookingStatus.Pending)
                .ExecuteDeleteAsync() != 0;
        }

        public async Task<BookingDto?> UpdateBookingStatusAsync(int id, string ownerId, bool isApproved)
        {
            var booking = await _context.Bookings
                .Include(b => b.RentItem)
                .FirstOrDefaultAsync(b => b.Id == id && b.RentItem.OwnerId == ownerId);

            if (booking == null) return null;

            if (booking.Status != BookingStatus.Pending)
                throw new ArgumentException("Нельзя изменить статус подтвержденного/отклоненного бронирования.");

            if (isApproved)
            {
                booking.Status = booking.StartDate == DateTime.Today ? BookingStatus.Active : BookingStatus.Confirmed;
            }
            else
            {
                booking.Status = BookingStatus.Cancelled;
            }


            _context.Bookings.Update(booking);
            await _context.SaveChangesAsync();

            return BookingDto.ToDto(booking);
        }

        private IQueryable<Booking> GetSortedList(IQueryable<Booking> source, BookingQueryParameters queryParameters)
        {
            IQueryable<Booking> query = source;

            if (!string.IsNullOrWhiteSpace(queryParameters.Search))
            {
                query = query.Where(b => b.RentItem.Title.Contains(queryParameters.Search));
            }

            if (queryParameters.MinTotalPrice.HasValue)
                query = query.Where(b => b.TotalPrice >= queryParameters.MinTotalPrice.Value);

            if (queryParameters.MaxTotalPrice.HasValue)
                query = query.Where(b => b.TotalPrice <= queryParameters.MaxTotalPrice.Value);

            if (!string.IsNullOrWhiteSpace(queryParameters.Status))
            {
                query = queryParameters.Status.ToLower() switch
                {
                    "pending" => query.Where(b => b.Status == BookingStatus.Pending),
                    "confirmed" => query.Where(b => b.Status == BookingStatus.Confirmed),
                    "active" => query.Where(b => b.Status == BookingStatus.Active),
                    "complited" => query.Where(b => b.Status == BookingStatus.Completed),
                    "cancelled" => query.Where(b => b.Status == BookingStatus.Cancelled),
                    _ => query
                };
            }

            if (queryParameters.FromDate.HasValue)
                query = query.Where(b => b.StartDate >= queryParameters.FromDate.Value);

            if (queryParameters.ToDate.HasValue)
                query = query.Where(b => b.EndDate <= queryParameters.ToDate.Value);

            bool isDesc = queryParameters.SortDirection?.ToLower() == "desc";

            return queryParameters.SortBy?.ToLower() switch
            {
                "created_at" or "createdat" => isDesc ? query.OrderByDescending(b => b.CreatedAt)
                                                      : query.OrderBy(b => b.CreatedAt),

                "start_date" or "startdate" => isDesc ? query.OrderByDescending(b => b.StartDate)
                                                      : query.OrderBy(b => b.StartDate),

                "end_date" or "enddate" => isDesc ? query.OrderByDescending(b => b.EndDate)
                                                      : query.OrderBy(b => b.EndDate),

                "price" or "total_price" or "totalprice" => isDesc ? query.OrderByDescending(b => b.TotalPrice)
                                                        : query.OrderBy(b => b.TotalPrice),

                _ => isDesc ? query.OrderByDescending(b => b.CreatedAt)
                            : query.OrderBy(b => b.CreatedAt)
            };
        }
    }
}
