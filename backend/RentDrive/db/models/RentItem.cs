namespace RentDrive.db.models
{
    public class RentItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PricePerDay { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;

        public string OwnerId { get; set; } = string.Empty;
        public User Owner { get; set; } = null!;

        public List<Booking> Bookings { get; set; } = [];
    }
}
