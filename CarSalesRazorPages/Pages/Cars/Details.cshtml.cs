using System.Net.Http.Json;
using System.Security.Claims;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace CarSalesRazorPages.Pages.Cars;

public class DetailsModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public DetailsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public CarViewModel? Car { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Car = await _httpClient.GetFromJsonAsync<CarViewModel>($"{_apiBaseUrl}/odata/Cars({id})?$expand=Brand");
            if (Car == null) return NotFound();
            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải thông tin chi tiết xe: " + ex.Message;
            return RedirectToPage("/Cars/Index");
        }
    }

    public async Task<IActionResult> OnPostSubmitPurchaseRequestAsync(string endpoint, [FromBody] System.Text.Json.JsonElement payload)
    {
        try
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var requestUri = $"{_apiBaseUrl}/api/car-sales/requests";

            int carId = payload.TryGetProperty("carId", out var carProp) ? carProp.GetInt32() : 0;
            string name = User.Identity?.Name ?? "Khách hàng";
            string phone = payload.TryGetProperty("customerPhone", out var phoneProp) ? phoneProp.GetString() ?? "0900000000" : "0900000000";
            string email = payload.TryGetProperty("customerEmail", out var emailProp) ? emailProp.GetString() ?? "" : "";
            string message = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? (endpoint == "deposit" ? "Đặt cọc xe" : "Mua đứt xe") : endpoint;

            var dto = new
            {
                CarId = carId,
                CustomerName = name,
                CustomerPhone = phone,
                CustomerEmail = email,
                Message = message
            };

            var response = await _httpClient.PostAsJsonAsync(requestUri, dto);
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
        }
    }
}
