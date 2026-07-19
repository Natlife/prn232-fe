using System;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
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
        public decimal PackagePrice { get; set; }

        [Display(Name = "Trạng thái")]
        public string Status { get; set; } = "Available";

        public DateTime CreatedAt { get; set; }
    }
}
