using System;
using System.Collections.Generic;

namespace CarSalesManagementSystemClient.Models
{
    public class UnifiedCartItem
    {
        public string ItemType { get; set; } = string.Empty; // "Part", "Service", "Package"
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal => Price * Quantity;
        // Optionally store an image URL if available
        public string? ImageUrl { get; set; }
        public string Purpose { get; set; } = "Standalone"; // "Standalone" (Mua riêng) or "Maintenance" (Bảo dưỡng)
    }

    public class UnifiedCart
    {
        public List<UnifiedCartItem> Items { get; set; } = new List<UnifiedCartItem>();

        public decimal TotalAmount
        {
            get
            {
                decimal total = 0;
                foreach (var item in Items)
                {
                    total += item.SubTotal;
                }
                return total;
            }
        }

        public void AddItem(UnifiedCartItem newItem)
        {
            if (newItem.ItemType == "Package" || newItem.ItemType == "Service" || newItem.ItemType == "Car")
            {
                // Cars, Services and Packages can only be added once (quantity 1 max per type/id)
                var existing = Items.Find(i => i.ItemType == newItem.ItemType && i.ItemId == newItem.ItemId);
                if (existing == null)
                {
                    newItem.Quantity = 1;
                    Items.Add(newItem);
                }
            }
            else // Part
            {
                var existing = Items.Find(i => i.ItemType == "Part" && i.ItemId == newItem.ItemId);
                if (existing != null)
                {
                    existing.Quantity += newItem.Quantity;
                }
                else
                {
                    Items.Add(newItem);
                }
            }
        }

        public void RemoveItem(string itemType, int itemId)
        {
            var item = Items.Find(i => i.ItemType == itemType && i.ItemId == itemId);
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void UpdateQuantity(string itemType, int itemId, int quantity)
        {
            if (itemType != "Part") return; // Only Parts can have quantity > 1 updated this way

            var item = Items.Find(i => i.ItemType == itemType && i.ItemId == itemId);
            if (item != null)
            {
                if (quantity <= 0)
                {
                    Items.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
            }
        }
    }

    public class UnifiedCheckoutPostModel
    {
        public string CustomerName { get; set; } = null!;
        public string CustomerPhone { get; set; } = null!;
        public string? CustomerEmail { get; set; }

        public string DeliveryMethod { get; set; } = "Pickup"; // "Pickup" or "Shipping"
        public string? ShippingAddress { get; set; }

        public string? CarName { get; set; }
        public string? LicensePlate { get; set; }
        public string? AppointmentDate { get; set; }
        public string? AppointmentTime { get; set; }
        public string? Note { get; set; }

        /// <summary>"Deposit" (đặt cọc) hoặc "Buyout" (mua đứt) — áp dụng cho hóa đơn tổng.</summary>
        public string PurchaseType { get; set; } = "Buyout";
    }
}
