namespace CarSalesManagementSystemClient.Models
{
    public class MaintenancePackageSearchViewModel
    {
        public string? SearchTerm { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MaxDuration { get; set; }
        public string? SortBy { get; set; }
        
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 6;
    }
}
