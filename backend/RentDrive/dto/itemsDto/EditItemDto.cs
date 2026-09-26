namespace RentDrive.dto.itemsDto
{
    public class EditItemDto
    {
        public string? Title { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public decimal? PricePerDay { get; set; }
        public List<string> KeepImageUrls { get; set; } = [];
        public IFormFileCollection? NewImageFiles { get; set; }
    }
}
