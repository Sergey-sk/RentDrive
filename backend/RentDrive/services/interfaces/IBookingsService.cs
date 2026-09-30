using RentDrive.dto.bookingsDto;

namespace RentDrive.services.interfaces
{
    public interface IBookingsService
    {
        Task<(List<BookingDto>, int)> GetUserBookingsAsync(string userId, BookingQueryParameters queryParameters);
        Task<DetailedBookingDto?> GetUserBookingByIdAsync(string userId, int id);
        Task<(List<BookingDto>, int)> GetBookingRequestsAsync(string ownerId, BookingQueryParameters queryParameters);
        Task<DetailedRequestBookingDto?> GetBookingRequestByIdAsync(int bookingId, string userId);
        Task<DetailedBookingDto> CreateBookingAsync(string userId, int itemId, CreateBookingDto createDto);
        Task<DetailedBookingDto?> UpdateBookingStatusAsync(int id, string ownerId, bool isApproved);
        Task<bool> DeleteBookingAsync(string userId, int id);
    }
}
