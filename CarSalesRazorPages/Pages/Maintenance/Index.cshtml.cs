using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Maintenance;

public class IndexModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "http://localhost:5084/api";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<MaintenancePackageViewModel> Packages { get; set; } = new();
    public List<AppointmentHistoryViewModel> Appointments { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;

    private class ApiResponse<T> { public bool Success { get; set; } public string? Message { get; set; } public T? Data { get; set; } }

    private void AppendAuthorizationHeader()
    {
        var token = User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task OnGetAsync(int page = 1)
    {
        CurrentPage = page;

        var response = await _httpClient.GetAsync($"{ApiUrl}/MaintenancePackages/available");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse<List<MaintenancePackageViewModel>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (apiResult != null && apiResult.Success)
                Packages = apiResult.Data ?? new List<MaintenancePackageViewModel>();
        }

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int customerId))
            {
                AppendAuthorizationHeader();
                var historyResponse = await _httpClient.GetAsync($"{ApiUrl}/MaintenanceAppointments/customer/{customerId}");
                if (historyResponse.IsSuccessStatusCode)
                {
                    var historyContent = await historyResponse.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<AppointmentHistoryViewModel>>>(historyContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResult != null && apiResult.Success && apiResult.Data != null)
                    {
                        var allAppointments = apiResult.Data.OrderByDescending(x => x.CreatedAt).ToList();
                        int pageSize = 5;
                        TotalPages = Math.Max(1, (int)Math.Ceiling(allAppointments.Count / (double)pageSize));
                        CurrentPage = Math.Clamp(page, 1, TotalPages);
                        Appointments = allAppointments.Skip((CurrentPage - 1) * pageSize).Take(pageSize).ToList();
                    }
                }
            }
        }
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        AppendAuthorizationHeader();
        var reqObj = new { Status = "Cancelled", Reason = "Khách hàng tự hủy" };
        var response = await _httpClient.PutAsync($"{ApiUrl}/MaintenanceAppointments/{id}/status",
            new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json"));

        TempData[response.IsSuccessStatusCode ? "Success" : "Error"] = response.IsSuccessStatusCode ? "Bạn đã hủy!!!" : "Đã xảy ra lỗi khi hủy lịch hẹn.";
        return RedirectToPage();
    }
}
