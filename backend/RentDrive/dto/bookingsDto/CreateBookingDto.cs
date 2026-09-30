using FluentValidation;

namespace RentDrive.dto.bookingsDto
{
    public class CreateBookingDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
