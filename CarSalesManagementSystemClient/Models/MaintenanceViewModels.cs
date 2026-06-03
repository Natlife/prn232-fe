using System;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
{
    public class MaintenancePackageViewModel
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int EstimatedDuration { get; set; }
        public string Status { get; set; }
    }

    public class BookingViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn gói bảo dưỡng.")]
        public int PackageId { get; set; }

        public string PackageName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên xe (Ví dụ: Honda Civic).")]
        [Display(Name = "Tên xe")]
        public string CarName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập biển số xe.")]
        [Display(Name = "Biển số xe")]
        public string LicensePlate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ.")]
        [Display(Name = "Số điện thoại")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt lịch.")]
        [Display(Name = "Ngày đặt lịch")]
        public DateOnly AppointmentDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ đặt lịch.")]
        [Display(Name = "Giờ đặt lịch")]
        public TimeOnly AppointmentTime { get; set; }

        [Display(Name = "Ghi chú thêm")]
        public string Note { get; set; }
    }

    public class AppointmentHistoryViewModel
    {
        public int AppointmentId { get; set; }
        public string CarName { get; set; }
        public string LicensePlate { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public MaintenancePackageViewModel Package { get; set; }
    }

    public class MaintenanceIndexViewModel
    {
        public List<MaintenancePackageViewModel> Packages { get; set; } = new List<MaintenancePackageViewModel>();
        public List<AppointmentHistoryViewModel> Appointments { get; set; } = new List<AppointmentHistoryViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }
}
