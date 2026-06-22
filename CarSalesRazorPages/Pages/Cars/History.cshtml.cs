using System.Net.Http.Json;
using System.Security.Claims;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace CarSalesRazorPages.Pages.Cars;

[Authorize]
public class HistoryModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public HistoryModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public List<PurchaseRequestHistoryViewModel> HistoryList { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var customerIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerIdClaim))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin tài khoản người dùng.";
                return RedirectToPage("/Index");
            }

            int customerId = int.Parse(customerIdClaim);
            var requestUri = $"{_apiBaseUrl}/odata/PurchaseRequests?$filter=CustomerId eq {customerId}&$expand=Car&$orderby=CreatedAt desc";
            var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PurchaseRequestHistoryViewModel>>(requestUri);
            HistoryList = odataResponse?.Value ?? new List<PurchaseRequestHistoryViewModel>();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải lịch sử: " + ex.Message;
        }

        return Page();
    }
}
