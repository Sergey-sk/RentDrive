namespace RentDrive.dto.itemsDto
{
    public class PagedItemsDto
    {
        public List<RentItemDto> Items { get; set; } = [];
        public int PageCount { get; set; }
        public int TotalItems { get; set; }
    }
}
