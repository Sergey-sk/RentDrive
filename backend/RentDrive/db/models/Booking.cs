namespace RentDrive.db.models
{
    public class Booking
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalPrice { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int RentItemId { get; set; }
        public RentItem RentItem { get; set; } = null!;

        public string CustomerId { get; set; } = string.Empty;
        public User Customer { get; set; } = null!;
    }
}
