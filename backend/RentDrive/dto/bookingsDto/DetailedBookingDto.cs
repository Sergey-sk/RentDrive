namespace RentDrive.dto.bookingsDto
{
    public class DetailedBookingDto
    {
        public BookingDto Booking { get; set; } = null!;

        public string OwnerFirstName { get; set; } = string.Empty;
        public string OwnerLastName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
    }
}
