using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.PartOrders;

[Authorize(Roles = "Admin")]
public class ManageModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string OrdersApiUrl = "http://localhost:5084/api/PartOrders";

    public ManageModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<PartOrderViewModel> Orders { get; set; } = new();
    public string? ErrorMessage { get; set; }

    private void AppendAuthorizationHeader()
    {
        var token = User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task OnGetAsync()
    {
        try
        {
            AppendAuthorizationHeader();
            var requestUri = $"{OrdersApiUrl}?$expand=Customer,PartOrderDetails($expand=Part)&$orderby=CreatedAt desc";
            var response = await _httpClient.GetFromJsonAsync<List<PartOrderViewModel>>(requestUri);
            Orders = response ?? new List<PartOrderViewModel>();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể tải danh sách đơn hàng quản trị: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
    {
        try
        {
            AppendAuthorizationHeader();
            var payload = new { OrderId = id, Status = status };
            var response = await _httpClient.PutAsJsonAsync($"{OrdersApiUrl}/{id}", payload);
            if (response.IsSuccessStatusCode)
            {
                string message = status == "Cancelled" ? "Đã hủy đơn hàng thành công!" : $"Cập nhật trạng thái đơn hàng sang '{status}' thành công!";
                return new JsonResult(new { success = true, message });
            }
            return new JsonResult(new { success = false, message = "Không thể cập nhật đơn hàng." });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message }); }
    }
}
