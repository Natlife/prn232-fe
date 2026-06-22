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
public class EditCarModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public EditCarModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            var car = await _httpClient.GetFromJsonAsync<CarViewModel>($"{_apiBaseUrl}/odata/Cars({id})");
            if (car == null) return NotFound();
            Form = new CarFormViewModel { CarId = car.CarId, BrandId = car.BrandId, CarName = car.CarName, Model = car.Model, Year = car.Year, Color = car.Color, Mileage = car.Mileage, FuelType = car.FuelType, Transmission = car.Transmission, Price = car.Price, Description = car.Description, ImageUrl = car.ImageUrl, Status = car.Status };
            await LoadBrands();
            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải thông tin xe: " + ex.Message;
            return RedirectToPage("/Admin/Cars");
        }
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!ModelState.IsValid) { await LoadBrands(); return Page(); }

        if (!AttachJwtToken())
        {
            TempData["ErrorMessage"] = "Phiên đăng nhập không có token. Vui lòng đăng nhập lại.";
            await LoadBrands(); return Page();
        }

        var payload = new { CarId = id, BrandId = Form.BrandId, CarName = Form.CarName, Model = Form.Model, Year = Form.Year, Color = Form.Color, Mileage = Form.Mileage, FuelType = Form.FuelType, Transmission = Form.Transmission, Price = Form.Price, Description = Form.Description, ImageUrl = Form.ImageUrl, Status = Form.Status, CreatedAt = DateTime.Now };

        try
        {
            var response = await _httpClient.PutAsync($"{_apiBaseUrl}/odata/Cars({id})",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode) { TempData["SuccessMessage"] = "Cập nhật xe thành công!"; return RedirectToPage("/Admin/Cars"); }
            TempData["ErrorMessage"] = "Cập nhật thất bại: " + await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message; }

        await LoadBrands(); return Page();
    }
}
