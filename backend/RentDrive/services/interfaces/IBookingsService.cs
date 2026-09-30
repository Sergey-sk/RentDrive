using RentDrive.dto.bookingsDto;

namespace RentDrive.services.interfaces
{
    public interface IBookingsService
    {
        Task<(List<BookingDto>, int)> GetUserBookingsAsync(string userId, BookingQueryParameters queryParameters);
        Task<BookingDto?> GetUserBookingByIdAsync(string userId, int id);
        Task<(List<BookingDto>, int)> GetBookingRequestsAsync(string ownerId, BookingQueryParameters queryParameters);
        Task<BookingDto?> GetBookingRequestByIdAsync(int bookingId, string userId);
        Task<BookingDto> CreateBookingAsync(string userId, int itemId, CreateBookingDto createDto);
        Task<BookingDto?> UpdateBookingStatusAsync(int id, string ownerId, bool isApproved);
        Task<bool> DeleteBookingAsync(string userId, int id);
    }
}
