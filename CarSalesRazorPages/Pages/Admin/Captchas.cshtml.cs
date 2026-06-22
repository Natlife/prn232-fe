using System.Net.Http.Json;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace CarSalesRazorPages.Pages.Admin;

[Authorize(Roles = "Admin")]
public class CaptchasModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public CaptchasModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public List<DepositCaptchaViewModel> Captchas { get; set; } = new();
    public List<CarViewModel> Cars { get; set; } = new();

    private bool AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (string.IsNullOrEmpty(token)) return false;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!AttachJwtToken())
        {
            TempData["ErrorMessage"] = "Phiên đăng nhập không có token hoặc đã hết hạn. Vui lòng đăng nhập lại.";
            Cars = new List<CarViewModel>();
            return Page();
        }

        try
        {
            var captchaResponse = await _httpClient.GetFromJsonAsync<ODataResponse<DepositCaptchaViewModel>>($"{_apiBaseUrl}/odata/DepositCaptchas?$expand=Car&$orderby=CreatedAt desc");
            Captchas = captchaResponse?.Value ?? new List<DepositCaptchaViewModel>();

            var carsResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>($"{_apiBaseUrl}/odata/Cars?$filter=Status eq 'Available' or Status eq 'Reserved'");
            Cars = carsResponse?.Value ?? new List<CarViewModel>();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải danh sách Captcha: " + ex.Message;
            Cars = new List<CarViewModel>();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGenerateCaptchaAsync(int carId, string? code)
    {
        try
        {
            AttachJwtToken();
            var payload = new { CarId = carId, Code = code };
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/odata/DepositCaptchas/generate", payload);

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            var msg = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : (response.IsSuccessStatusCode ? "Tạo mã thành công." : "Lỗi không xác định.");
            TempData[response.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = msg;
        }
        catch (Exception ex) { TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message; }

        return RedirectToPage();
    }
}
