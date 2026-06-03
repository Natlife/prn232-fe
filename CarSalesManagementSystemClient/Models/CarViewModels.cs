using System;
using System.Collections.Generic;

namespace CarSalesManagementSystemClient.Models;

public class CarBrandViewModel
{
    public int BrandId { get; set; }
    public string BrandName { get; set; } = null!;
    public string? Country { get; set; }
    public string? Description { get; set; }
}

public class CarViewModel
{
    public int CarId { get; set; }
    public int BrandId { get; set; }
    public string CarName { get; set; } = null!;
    public string? Model { get; set; }
    public int Year { get; set; }
    public string? Color { get; set; }
    public int Mileage { get; set; }
    public string FuelType { get; set; } = null!;
    public string Transmission { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public CarBrandViewModel Brand { get; set; } = null!;
}

public class CarSearchViewModel
{
    public int? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Transmission { get; set; }
    public string? FuelType { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 6;
}

public class PagedResultViewModel<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

public class PurchaseRequestHistoryViewModel
{
    public int RequestId { get; set; }
    public int CarId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string CustomerPhone { get; set; } = null!;
    public string? CustomerEmail { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateTime? DepositDate { get; set; }
    public DateTime? DepositExpiry { get; set; }
    public string? CaptchaCode { get; set; }
    public CarViewModel Car { get; set; } = null!;
}

public class DepositCaptchaViewModel
{
    public int CaptchaId { get; set; }
    public string Code { get; set; } = null!;
    public int CarId { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public CarViewModel Car { get; set; } = null!;
}

public class CarFormViewModel
{
    public int CarId { get; set; }
    public int BrandId { get; set; }
    public string CarName { get; set; } = null!;
    public string? Model { get; set; }
    public int Year { get; set; } = DateTime.Now.Year;
    public string? Color { get; set; }
    public int Mileage { get; set; }
    public string FuelType { get; set; } = "Gasoline";
    public string Transmission { get; set; } = "Automatic";
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = "Available";
}

public class ODataResponse<T>
{
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();

    [System.Text.Json.Serialization.JsonPropertyName("@odata.count")]
    public int? Count { get; set; }
}
