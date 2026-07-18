#pragma warning disable CS8618
using System;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
{
    public class ServiceSummaryViewModel
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public int EstimatedDurationMinutes { get; set; }
    }

    public class MaintenancePackageViewModel
    {
        public int PackageId { get; set; }
        public string? PackageName { get; set; }
        public string? Description { get; set; }
        public decimal PackagePrice { get; set; }
        public string? Status { get; set; }
        
        public List<ServiceSummaryViewModel> Services { get; set; } = new();
        public List<int> ServiceIds { get; set; } = new();
        
        public decimal TotalBasePrice { get; set; }
        public decimal SavingAmount { get; set; }
        public int TotalDurationMinutes { get; set; }
    }

    public class AppointmentDetailViewModel
    {
        public int AppointmentDetailId { get; set; }
        public int? PackageId { get; set; }
        public string? PackageName { get; set; }
        public int? ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public List<ServiceSummaryViewModel> PackageServices { get; set; } = new();
    }

    public class ConsumedPartViewModel
    {
        public int ConsumedPartId { get; set; }
        public int PartId { get; set; }
        public string? PartName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public bool IsIncurred { get; set; }
        public bool ApprovedByCustomer { get; set; }
        public string? Notes { get; set; }
    }

    public class CustomerCarViewModel
    {
        public int CustomerCarId { get; set; }
        public int CustomerId { get; set; }
        public int BrandId { get; set; }
        public string? BrandName { get; set; }
        public string Model { get; set; } = null!;
        public int? Year { get; set; }
        public string? VIN { get; set; }
        public string LicensePlate { get; set; } = null!;
        public string? Color { get; set; }
    }

    public class UnifiedPartItemViewModel
    {
        public int PartId { get; set; }
        public int Quantity { get; set; }
    }

    public class BookingViewModel : IValidatableObject
    {
        public int? CustomerId { get; set; }
        public List<int> PackageIds { get; set; } = new();
        public List<int> ServiceIds { get; set; } = new();
        public List<UnifiedPartItemViewModel> PartItems { get; set; } = new();

        [Required(ErrorMessage = "Vui lòng chọn xe.")]
        public int CustomerCarId { get; set; }

        public string? CarName { get; set; }
        public string? LicensePlate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên người liên hệ.")]
        [Display(Name = "Tên người liên hệ")]
        public string CustomerName { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string CustomerPhone { get; set; } = null!;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt lịch.")]
        [Display(Name = "Ngày đặt lịch")]
        public DateOnly AppointmentDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ đặt lịch.")]
        [Display(Name = "Giờ đặt lịch")]
        public TimeOnly AppointmentTime { get; set; }

        [Display(Name = "Ghi chú thêm")]
        public string? Note { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.Now.Date);
            var nowTime = TimeOnly.FromDateTime(DateTime.Now);

            if (AppointmentDate < today)
            {
                yield return new ValidationResult("Ngày đặt lịch không được ở trong quá khứ.", new[] { nameof(AppointmentDate) });
            }
            else if (AppointmentDate == today && AppointmentTime <= nowTime)
            {
                yield return new ValidationResult("Giờ đặt lịch không hợp lệ (phải chọn giờ trong tương lai).", new[] { nameof(AppointmentTime) });
            }
        }
    }

    public class AppointmentHistoryViewModel
    {
        public int AppointmentId { get; set; }
        public int CustomerId { get; set; }
        public int CustomerCarId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = null!;
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; }
        public CustomerCarViewModel? CustomerCar { get; set; }
        public List<AppointmentDetailViewModel> Details { get; set; } = new();
        public List<ConsumedPartViewModel> ConsumedParts { get; set; } = new();
    }

    public class MaintenanceIndexViewModel
    {
        public List<MaintenancePackageViewModel> Packages { get; set; } = new List<MaintenancePackageViewModel>();
        public List<AppointmentHistoryViewModel> Appointments { get; set; } = new List<AppointmentHistoryViewModel>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public MaintenancePackageSearchViewModel Filter { get; set; } = new MaintenancePackageSearchViewModel();
    }

    public class ServicesIndexViewModel
    {
        public List<ServiceSummaryViewModel> Services { get; set; } = new();
        public string? SearchTerm { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalPages { get; set; } = 1;
    }

    public class ServiceBookingViewModel : IValidatableObject
    {
        public int ServiceId { get; set; }
        public string? ServiceName { get; set; }

        public List<int> ServiceIds { get; set; } = new();
        public List<int> PackageIds { get; set; } = new();

        public int CustomerCarId { get; set; }
        public string? CarName { get; set; }
        public string? LicensePlate { get; set; }
        public string? CustomerName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string CustomerPhone { get; set; } = null!;

        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày đặt lịch.")]
        [Display(Name = "Ngày đặt lịch")]
        public DateOnly AppointmentDate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giờ đặt lịch.")]
        [Display(Name = "Giờ đặt lịch")]
        public TimeOnly AppointmentTime { get; set; }

        [Display(Name = "Ghi chú thêm")]
        public string? Note { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.Now.Date);
            var nowTime = TimeOnly.FromDateTime(DateTime.Now);

            if (AppointmentDate < today)
                yield return new ValidationResult("Ngày đặt lịch không được ở trong quá khứ.", new[] { nameof(AppointmentDate) });
            else if (AppointmentDate == today && AppointmentTime <= nowTime)
                yield return new ValidationResult("Giờ đặt lịch không hợp lệ (phải chọn giờ trong tương lai).", new[] { nameof(AppointmentTime) });
        }
    }
}
