using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
{
    public class PartCategoryViewModel
    {
        public int CategoryId { get; set; }
        
        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100, ErrorMessage = "Tên danh mục không vượt quá 100 ký tự")]
        public string CategoryName { get; set; } = null!;
        
        public string? Description { get; set; }
    }

    public class PartViewModel
    {
        public int PartId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục phụ tùng")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên phụ tùng không được để trống")]
        [StringLength(150, ErrorMessage = "Tên phụ tùng không vượt quá 150 ký tự")]
        public string PartName { get; set; } = null!;

        [Required(ErrorMessage = "Mã phụ tùng không được để trống")]
        [StringLength(50, ErrorMessage = "Mã phụ tùng không vượt quá 50 ký tự")]
        public string PartCode { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Tên thương hiệu không vượt quá 100 ký tự")]
        public string? Brand { get; set; }

        [Required(ErrorMessage = "Giá bán không được để trống")]
        [Range(1000, 1000000000, ErrorMessage = "Giá bán phải lớn hơn 1,000 VND")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Số lượng không được để trống")]
        [Range(0, 100000, ErrorMessage = "Số lượng phải từ 0 đến 100,000")]
        public int Quantity { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không vượt quá 1000 ký tự")]
        public string? Description { get; set; }

        [Url(ErrorMessage = "Hình ảnh phải là đường dẫn URL hợp lệ")]
        [StringLength(500, ErrorMessage = "Đường dẫn hình ảnh không vượt quá 500 ký tự")]
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái")]
        public string Status { get; set; } = "Available";

        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiredAt { get; set; }

        public int MinStockLevel { get; set; } = 5;
        public int MaxStockLevel { get; set; } = 100;
        public string? WarehouseLocation { get; set; }
        public int WarrantyMonths { get; set; }
        public string? UnitOfMeasure { get; set; }
        public bool CanEditPartCode { get; set; } = true;

        public PartCategoryViewModel? Category { get; set; }
    }

    public class UpdatePartViewModel
    {
        public int PartId { get; set; }

        [Required(ErrorMessage = "Tên phụ tùng không được để trống")]
        [StringLength(150, ErrorMessage = "Tên phụ tùng không vượt quá 150 ký tự")]
        public string PartName { get; set; } = null!;

        [Required(ErrorMessage = "Mã phụ tùng không được để trống")]
        [StringLength(50, ErrorMessage = "Mã phụ tùng không vượt quá 50 ký tự")]
        public string PartCode { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng chọn danh mục phụ tùng")]
        public int CategoryId { get; set; }

        [StringLength(100, ErrorMessage = "Tên thương hiệu không vượt quá 100 ký tự")]
        public string? Brand { get; set; }

        [Required(ErrorMessage = "Giá bán không được để trống")]
        [Range(0, 1000000000, ErrorMessage = "Giá bán không được âm")]
        public decimal Price { get; set; }

        [Range(0, 100000, ErrorMessage = "Mức cảnh báo tối thiểu không được âm")]
        public int MinStockLevel { get; set; } = 5;

        [Range(1, 100000, ErrorMessage = "Sức chứa tối đa phải lớn hơn 0")]
        public int MaxStockLevel { get; set; } = 100;

        [Required(ErrorMessage = "Đơn vị tính không được để trống")]
        public string UnitOfMeasure { get; set; } = "Cái";

        public string? WarehouseLocation { get; set; }

        [Range(0, 1000, ErrorMessage = "Số tháng bảo hành không được âm")]
        public int WarrantyMonths { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả không vượt quá 1000 ký tự")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "Đường dẫn hình ảnh không vượt quá 500 ký tự")]
        public string? ImageUrl { get; set; }

        public string Status { get; set; } = "Available";

        public int CurrentQuantity { get; set; }
        public DateTime? CurrentExpiredAt { get; set; }
        public bool CanEditPartCode { get; set; } = true;
    }

    public class InventoryAdjustmentViewModel
    {
        [Required(ErrorMessage = "Phụ tùng là bắt buộc")]
        public int PartId { get; set; }

        [Required(ErrorMessage = "Số lượng điều chỉnh không được bằng 0")]
        public int AdjustmentQuantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do điều chỉnh")]
        public string Reason { get; set; } = null!;

        public string? Notes { get; set; }
    }


    public class PartSearchViewModel
    {
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SearchTerm { get; set; }
        public string? SortBy { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 6;
    }

    public class CartItemViewModel
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = null!;
        public string PartCode { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class PartOrderDetailViewModel
    {
        public int OrderDetailId { get; set; }
        public int OrderId { get; set; }
        public int PartId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public PartViewModel Part { get; set; } = null!;
    }

    public class PartOrderViewModel
    {
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string CustomerPhone { get; set; } = null!;
        public string? CustomerEmail { get; set; }
        public string? ShippingAddress { get; set; }
        public string DeliveryMethod { get; set; } = "Pickup";
        public decimal ShippingFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PartOrderDetailViewModel> PartOrderDetails { get; set; } = new();
    }

    public class PartOrderCreateViewModel
    {
        [Required(ErrorMessage = "Họ tên người nhận không được để trống")]
        [StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự")]
        public string CustomerName { get; set; } = null!;

        [Required(ErrorMessage = "Số điện thoại nhận hàng không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [StringLength(20, ErrorMessage = "Số điện thoại không vượt quá 20 ký tự")]
        public string CustomerPhone { get; set; } = null!;

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email không vượt quá 100 ký tự")]
        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức nhận hàng")]
        public string DeliveryMethod { get; set; } = "Pickup";

        [StringLength(255, ErrorMessage = "Địa chỉ nhận hàng không vượt quá 255 ký tự")]
        public string? ShippingAddress { get; set; }
    }

    public class InventoryTransactionViewModel
    {
        public int TransactionId { get; set; }
        public int PartId { get; set; }
        public string TransactionType { get; set; } = null!;
        public int Quantity { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; } = "N/A";
        public string? Notes { get; set; }
        public string TransactionDate { get; set; } = null!;
    }

    public class NewPartViewModel
    {
        [Required(ErrorMessage = "Danh mục phụ tùng mới là bắt buộc")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên phụ tùng mới là bắt buộc")]
        public string PartName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã phụ tùng mới là bắt buộc")]
        public string PartCode { get; set; } = string.Empty;

        public string? Brand { get; set; }

        [Required(ErrorMessage = "Giá bán ra là bắt buộc")]
        [Range(0, 1000000000, ErrorMessage = "Giá bán ra không được âm")]
        public decimal Price { get; set; }

        [Range(0, 100000, ErrorMessage = "Định mức tối thiểu không được âm")]
        public int MinStockLevel { get; set; } = 5;

        [Range(1, 100000, ErrorMessage = "Định mức tối đa phải lớn hơn 0")]
        public int MaxStockLevel { get; set; } = 100;

        public string UnitOfMeasure { get; set; } = "Cái";

        public string? WarehouseLocation { get; set; }

        [Range(0, 1000, ErrorMessage = "Số tháng bảo hành không được âm")]
        public int WarrantyMonths { get; set; }

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
    }

    public class SupplierViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
    }

    public class InventoryReceiptItemViewModel
    {
        public bool IsNewPart { get; set; }

        public int? SupplierId { get; set; }

        public int? PartId { get; set; }

        [Required(ErrorMessage = "Số lượng nhập là bắt buộc")]
        [Range(1, 100000, ErrorMessage = "Số lượng nhập phải lớn hơn 0")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Giá nhập là bắt buộc")]
        [Range(0, 1000000000, ErrorMessage = "Giá nhập không được âm")]
        public decimal ImportPrice { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public NewPartViewModel? NewPart { get; set; }
    }

    public class InventoryReceiptCreateViewModel
    {
        public int? SupplierId { get; set; }

        public string? Notes { get; set; }

        public List<InventoryReceiptItemViewModel> Items { get; set; } = new List<InventoryReceiptItemViewModel>();
    }
}
