using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
{
    public class ComboOrderItemViewModel
    {
        public int ItemId { get; set; }
        public int ComboOrderId { get; set; }
        public string ItemType { get; set; } = null!;
        public int ReferenceId { get; set; }
        public string ItemName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
    }

    public class ComboOrderViewModel
    {
        public int ComboOrderId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string CustomerPhone { get; set; } = null!;
        public string? CustomerEmail { get; set; }
        public string? ShippingAddress { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }
        public string Source { get; set; } = null!;
        public string? ChatSessionId { get; set; }
        public string PurchaseType { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal? DepositAmount { get; set; }
        public DateTime? DepositExpiresAt { get; set; }
        
        public string? CaptchaCode { get; set; }
        public DateTime? CaptchaGeneratedAt { get; set; }
        public bool IsCaptchaUsed { get; set; }
        public DateTime? CaptchaUsedAt { get; set; }

        public string? FinalCaptchaCode { get; set; }
        public DateTime? FinalCaptchaGeneratedAt { get; set; }
        public bool IsFinalCaptchaUsed { get; set; }
        public DateTime? FinalCaptchaUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual List<ComboOrderItemViewModel> Items { get; set; } = new List<ComboOrderItemViewModel>();
    }

    public class ComboOrderConfirmViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên của bạn.")]
        [Display(Name = "Họ và tên")]
        public string CustomerName { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string CustomerPhone { get; set; } = null!;

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        [Display(Name = "Email")]
        public string? CustomerEmail { get; set; }

        [Display(Name = "Địa chỉ giao hàng (nếu có phụ tùng)")]
        public string? ShippingAddress { get; set; }

        [Display(Name = "Ghi chú thêm")]
        public string? Note { get; set; }

        [Required]
        [Display(Name = "Hình thức thanh toán")]
        public string PurchaseType { get; set; } = "Buyout"; // "Deposit" or "Buyout"

        [Required]
        public string Draft { get; set; } = null!;
    }
}
