using RentDrive.db.models;

namespace RentDrive.dto.bookingsDto
{
    public class DetailedRequestBookingDto
    {
        public BookingDto Booking { get; set; } = null!;

        public string CustomerFirstName { get; set; } = string.Empty;
        public string CustomerLastName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;

        public static DetailedRequestBookingDto ToDto(Booking model)
        {
            return new DetailedRequestBookingDto
            {
                Booking = BookingDto.ToDto(model),
                CustomerFirstName = model.Customer?.FirstName ?? string.Empty,
                CustomerLastName = model.Customer?.LastName ?? string.Empty,
                CustomerEmail = model.Customer?.Email ?? string.Empty,
            };
        }
    }
}
