namespace RentDrive
{
    public class BookingQueryParameters
    {
        public string? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
        public decimal? MinTotalPrice { get; set; }
        public decimal? MaxTotalPrice { get; set; }
        public string? SortBy { get; set; } = "created_at";
        public string? SortDirection { get; set; } = "desc";

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
