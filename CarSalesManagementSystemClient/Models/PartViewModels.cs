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

        public PartCategoryViewModel? Category { get; set; }
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
        public string ShippingAddress { get; set; } = null!;
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

        [Required(ErrorMessage = "Địa chỉ nhận hàng không được để trống")]
        [StringLength(255, ErrorMessage = "Địa chỉ nhận hàng không vượt quá 255 ký tự")]
        public string ShippingAddress { get; set; } = null!;
    }
}
