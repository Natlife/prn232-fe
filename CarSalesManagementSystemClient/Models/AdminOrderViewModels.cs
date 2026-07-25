using System;
using System.Collections.Generic;

namespace CarSalesManagementSystemClient.Models
{
    public class AdminOrderListItemViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty; // "Maintenance" hoặc "Part"
        public int SourceId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? AppointmentDateTime { get; set; }
        public string? DeliveryMethod { get; set; }
    }

    public class AdminPartOrderDetailItem
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal SubTotal { get; set; }
    }

    public class AdminPartOrderDetailsViewModel
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string DeliveryMethod { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public decimal ShippingFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<AdminPartOrderDetailItem> Details { get; set; } = new();
    }
}
