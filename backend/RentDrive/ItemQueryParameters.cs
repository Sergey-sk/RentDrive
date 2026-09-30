namespace RentDrive
{
    public class ItemQueryParameters
    {
        public string? SortBy { get; set; } = "title";
        public string? SortDirection { get; set; } = "desc";
        public string? Search { get; set; } = string.Empty;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public int? Page
        {
            get;
            set => field = value switch
            {
                < 1 => 1,
                null => 1,
                _ => value
            };
        } = 1;
        public int? PageSize
        {
            get;
            set => field = value switch
            {
                < 1 => 10,
                > 100 => 100,
                null => 10,
                _ => value
            };
        } = 10;
    }
}
