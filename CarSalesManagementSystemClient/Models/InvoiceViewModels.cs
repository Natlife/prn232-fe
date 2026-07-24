using System;
using System.Collections.Generic;

namespace CarSalesManagementSystemClient.Models
{
    public class InvoiceLineViewModel
    {
        public string ItemType { get; set; } = "";
        public int ReferenceId { get; set; }
        public string Description { get; set; } = "";
        public decimal SubTotal { get; set; }
    }

    /// <summary>Ánh xạ MasterInvoiceViewDto trả về từ API (/api/invoices).</summary>
    public class InvoiceListItemViewModel
    {
        public int MasterInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceType { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public int? StaffId { get; set; }

        public string PurchaseType { get; set; } = "Buyout";  // Deposit | Buyout
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid | Deposited | Paid | Refunded
        public string InvoiceStatus { get; set; } = "";       // PendingVerification | Confirmed | Completed | Cancelled

        public decimal TotalSubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public decimal? DepositAmount { get; set; }
        public decimal? DepositPaidAmount { get; set; }
        public DateTime? DepositExpiresAt { get; set; }
        public DateTime? ExpiredAt { get; set; }
        public int RemainingSeconds { get; set; }
        public bool IsDepositCaptchaUsed { get; set; }
        public bool IsFinalCaptchaUsed { get; set; }

        // Chỉ có giá trị khi Admin/Staff xem (backend trả kèm mã).
        public string? DepositCaptchaCode { get; set; }
        public string? FinalCaptchaCode { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? Notes { get; set; }

        public List<InvoiceLineViewModel> Lines { get; set; } = new();

        // ----- Helpers cho View -----
        public bool IsDeposit => string.Equals(PurchaseType, "Deposit", StringComparison.OrdinalIgnoreCase);
        public bool IsCancelled => string.Equals(InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase);
        public bool IsPaid => string.Equals(PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
        public bool IsDeposited => string.Equals(PaymentStatus, "Deposited", StringComparison.OrdinalIgnoreCase);

        /// <summary>Số tiền còn phải trả để tất toán.</summary>
        public decimal RemainingAmount => Math.Max(0, TotalAmount - (DepositPaidAmount ?? 0));

        /// <summary>Bước xác thực tiếp theo khách cần làm: "deposit", "final", hoặc null nếu xong/hủy.</summary>
        public string? NextAction
        {
            get
            {
                if (IsCancelled || IsPaid) return null;
                if (IsDeposit && !IsDepositCaptchaUsed) return "deposit"; // chưa xác nhận cọc
                if (!IsFinalCaptchaUsed) return "final";                 // cọc xong -> tất toán, hoặc mua đứt
                return null;
            }
        }
    }
}
