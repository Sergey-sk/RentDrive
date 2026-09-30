using RentDrive.db.models;

namespace RentDrive.dto.bookingsDto
{
    public class BookingDto
    {
        public int Id { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;

        public int RentItemId { get; set; }
        public string RentItemTitle { get; set; } = string.Empty;
        public string? RentItemMainImageUrl { get; set; }
        public decimal PricePerDay { get; set; }

        public static BookingDto ToDto(Booking model)
        {
            return new BookingDto
            {
                Id = model.Id,
                StartDate = model.StartDate.ToString("dd.MM.yyyy"),
                EndDate = model.EndDate.ToString("dd.MM.yyyy"),
                TotalPrice = Math.Round(model.TotalPrice, 2),
                Status = model.Status.ToString(),
                CreatedAt = model.CreatedAt.ToString("dd.MM.yyyy"),
                RentItemId = model.RentItemId,
                RentItemTitle = model.RentItem.Title,
                RentItemMainImageUrl = model.RentItem.ImageUrls.FirstOrDefault() ?? string.Empty,
                PricePerDay = Math.Round(model.RentItem.PricePerDay, 2)
            };
        }
    }
}
