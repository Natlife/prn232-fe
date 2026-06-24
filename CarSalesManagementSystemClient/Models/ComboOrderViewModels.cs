using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models;

public class ComboOrderItemInputViewModel
{
    public string ItemType { get; set; } = null!;
    public int ReferenceId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class ComboOrderCreateViewModel
{
    [Required(ErrorMessage = "So dien thoai khong duoc de trong")]
    [Phone(ErrorMessage = "So dien thoai khong dung dinh dang")]
    [StringLength(20)]
    public string CustomerPhone { get; set; } = null!;

    [Required]
    public string PurchaseType { get; set; } = "Buyout";

    [StringLength(255)]
    public string? ShippingAddress { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public string? ChatSessionId { get; set; }

    public List<ComboOrderItemInputViewModel> Items { get; set; } = new();
}

public class ComboOrderItemPreviewViewModel
{
    public string ItemType { get; set; } = null!;
    public int ReferenceId { get; set; }
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal SubTotal { get; set; }
    public string? ImageUrl { get; set; }
}

public class ComboOrderPreviewViewModel
{
    public List<ComboOrderItemPreviewViewModel> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string DraftToken { get; set; } = null!;
}

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
    public string PurchaseType { get; set; } = "Buyout";
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
    public List<ComboOrderItemViewModel> Items { get; set; } = new();
}
