using RentDrive.db.models;

namespace RentDrive.dto.itemsDto
{
    public class RentItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PricePerDay { get; set; }
        public List<string> ImageUrls { get; set; } = [];

        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;

        public static RentItemDto ToDto(RentItem model)
        {
            return new RentItemDto
            {
                Id = model.Id,
                Title = model.Title,
                Description = model.Description,
                PricePerDay = Math.Round(model.PricePerDay, 2),
                ImageUrls = model.ImageUrls,
                OwnerId = model.OwnerId,
                OwnerName = model?.Owner?.FirstName ?? string.Empty,
            };
        }
    }
}
