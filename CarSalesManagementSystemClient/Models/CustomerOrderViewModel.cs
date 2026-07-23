using System;
using System.Collections.Generic;

namespace CarSalesManagementSystemClient.Models
{
    public class CustomerOrderViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public bool IsEstimatedAmount { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? AppointmentDateTime { get; set; }
        public string? DeliveryMethod { get; set; }
    }

    public class MaintenanceOrderDetailsWrapper
    {
        public AppointmentHistoryViewModel Appointment { get; set; } = null!;
        public MasterInvoiceViewModel? Invoice { get; set; }
    }

    public class PartOrderDetailsWrapper
    {
        public PartOrderViewModel Order { get; set; } = null!;
        public MasterInvoiceViewModel? Invoice { get; set; }
    }

    public class MasterInvoiceViewModel
    {
        public int MasterInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int? StaffId { get; set; }
        public decimal TotalSubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PurchaseType { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string InvoiceStatus { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal? DepositAmount { get; set; }
        public decimal? DepositPaidAmount { get; set; }
        public DateTime? DepositExpiresAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
