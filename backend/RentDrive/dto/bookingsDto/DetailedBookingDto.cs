using RentDrive.db.models;

namespace RentDrive.dto.bookingsDto
{
    public class DetailedBookingDto
    {
        public BookingDto Booking { get; set; } = null!;

        public string OwnerFirstName { get; set; } = string.Empty;
        public string OwnerLastName { get; set; } = string.Empty;
        public string OwnerPhoneNumber { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;

        public static DetailedBookingDto ToDto(Booking model)
        {
            return new DetailedBookingDto
            {
                Booking = BookingDto.ToDto(model),
                OwnerFirstName = model.RentItem?.Owner?.FirstName ?? string.Empty,
                OwnerLastName = model.RentItem?.Owner?.LastName ?? string.Empty,
                OwnerPhoneNumber = model.RentItem?.Owner?.PhoneNumber ?? string.Empty,
                OwnerEmail = model.RentItem?.Owner?.Email ?? string.Empty,
            };
        }
    }
}
