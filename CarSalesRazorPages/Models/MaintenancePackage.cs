using System;
using System.ComponentModel.DataAnnotations;

namespace CarSalesRazorPages.Models
{
    public class MaintenancePackage
    {
        public int PackageId { get; set; }

        [Required]
        [Display(Name = "Tên gói")]
        public string PackageName { get; set; } = null!;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Giá tiền")]
        public decimal Price { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Available";

        public DateTime CreatedAt { get; set; }
    }
}
