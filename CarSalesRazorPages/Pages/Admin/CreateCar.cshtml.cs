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
public class CreateCarModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public CreateCarModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    [BindProperty]
    public CarFormViewModel Form { get; set; } = new();

    public IEnumerable<CarBrandViewModel> Brands { get; set; } = new List<CarBrandViewModel>();

    private bool AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (string.IsNullOrEmpty(token)) return false;
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return true;
    }

    private async Task LoadBrands()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarBrandViewModel>>($"{_apiBaseUrl}/odata/CarBrands");
            Brands = response?.Value ?? new List<CarBrandViewModel>();
        }
        catch { Brands = new List<CarBrandViewModel>(); }
    }

    public async Task OnGetAsync() => await LoadBrands();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadBrands(); return Page(); }

        if (!AttachJwtToken())
        {
            TempData["ErrorMessage"] = "Phiên đăng nhập không có token. Vui lòng đăng nhập lại.";
            await LoadBrands(); return Page();
        }

        var payload = new { BrandId = Form.BrandId, CarName = Form.CarName, Model = Form.Model, Year = Form.Year, Color = Form.Color, Mileage = Form.Mileage, FuelType = Form.FuelType, Transmission = Form.Transmission, Price = Form.Price, Description = Form.Description, ImageUrl = Form.ImageUrl, Status = Form.Status, CreatedAt = DateTime.Now };

        try
        {
            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/odata/Cars",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode) { TempData["SuccessMessage"] = "Thêm xe thành công!"; return RedirectToPage("/Admin/Cars"); }
            TempData["ErrorMessage"] = "Thêm xe thất bại: " + await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message; }

        await LoadBrands(); return Page();
    }
}
