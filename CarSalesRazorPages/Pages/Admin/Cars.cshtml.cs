using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace CarSalesRazorPages.Pages.Admin;

[Authorize(Roles = "Admin")]
public class CarsModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public CarsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public List<CarViewModel> Cars { get; set; } = new();

    private bool AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (string.IsNullOrEmpty(token)) return false;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(
                $"{_apiBaseUrl}/odata/Cars?$filter=Status ne 'Inactive'&$expand=Brand&$orderby=CreatedAt desc");
            Cars = response?.Value ?? new List<CarViewModel>();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải danh sách xe: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostDeleteCarAsync(int id)
    {
        if (!AttachJwtToken()) { TempData["ErrorMessage"] = "Phiên đăng nhập không có token. Vui lòng đăng nhập lại."; return RedirectToPage(); }
        try
        {
            var response = await _httpClient.DeleteAsync($"{_apiBaseUrl}/odata/Cars({id})");
            TempData[response.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = response.IsSuccessStatusCode ? "Xóa xe thành công!" : "Xóa thất bại.";
        }
        catch (Exception ex) { TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message; }
        return RedirectToPage();
    }
}
