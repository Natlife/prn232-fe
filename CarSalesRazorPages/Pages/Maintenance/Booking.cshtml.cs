using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Maintenance;

[Authorize]
public class BookingModel : PageModel, IValidatableObject
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "http://localhost:5084/api";

    public BookingModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng chọn gói bảo dưỡng.")]
    public int PackageId { get; set; }

    [BindProperty]
    public string PackageName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập tên xe (Ví dụ: Honda Civic).")]
    [Display(Name = "Tên xe")]
    public string CarName { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập biển số xe.")]
    [Display(Name = "Biển số xe")]
    public string LicensePlate { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ.")]
    [RegularExpression(@"^\d+$", ErrorMessage = "Số điện thoại không hợp lệ.")]
    [Display(Name = "Số điện thoại")]
    public string CustomerPhone { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng chọn ngày đặt lịch.")]
    [Display(Name = "Ngày đặt lịch")]
    public DateOnly AppointmentDate { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng chọn giờ đặt lịch.")]
    [Display(Name = "Giờ đặt lịch")]
    public TimeOnly AppointmentTime { get; set; }

    [BindProperty]
    [Display(Name = "Ghi chú thêm")]
    public string? Note { get; set; }

    public string? ErrorMessage { get; set; }

    private class ApiResponse<T> { public bool Success { get; set; } public string? Message { get; set; } public T? Data { get; set; } }

    private void AppendAuthorizationHeader()
    {
        var token = User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var response = await _httpClient.GetAsync($"{ApiUrl}/MaintenancePackages/{id}");
        if (!response.IsSuccessStatusCode) return RedirectToPage("/Maintenance/Index");

        var content = await response.Content.ReadAsStringAsync();
        var apiResult = JsonSerializer.Deserialize<ApiResponse<MaintenancePackageViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (apiResult == null || !apiResult.Success || apiResult.Data == null) return RedirectToPage("/Maintenance/Index");

        var package = apiResult.Data;
        if (package.Status != "Available")
        {
            TempData["Error"] = "Gói bảo dưỡng này hiện đã ngừng cung cấp. Vui lòng chọn gói khác.";
            return RedirectToPage("/Maintenance/Index");
        }

        PackageId = package.PackageId;
        PackageName = package.PackageName ?? string.Empty;
        AppointmentDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
        AppointmentTime = new TimeOnly(9, 0);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var checkResponse = await _httpClient.GetAsync($"{ApiUrl}/MaintenancePackages/{PackageId}");
        if (checkResponse.IsSuccessStatusCode)
        {
            var content = await checkResponse.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse<MaintenancePackageViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (apiResult == null || apiResult.Data == null || apiResult.Data.Status != "Available")
            {
                TempData["Error"] = "Gói bảo dưỡng này hiện đã ngừng cung cấp. Vui lòng chọn gói khác.";
                return RedirectToPage("/Maintenance/Index");
            }
        }

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out int customerId))
        {
            ErrorMessage = "Lỗi xác thực. Vui lòng đăng nhập lại.";
            return Page();
        }

        var payload = new
        {
            CustomerId = customerId,
            PackageId = PackageId,
            CustomerName = User.Identity?.Name ?? "Khách hàng",
            CustomerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            CustomerPhone = CustomerPhone,
            CarName = CarName,
            LicensePlate = LicensePlate,
            AppointmentDate = AppointmentDate,
            AppointmentTime = AppointmentTime,
            Note = Note ?? "",
            Status = "Pending"
        };

        AppendAuthorizationHeader();
        var response = await _httpClient.PostAsync($"{ApiUrl}/MaintenanceAppointments",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        if (response.IsSuccessStatusCode)
        {
            TempData["Success"] = "Đặt lịch thành công! Chúng tôi sẽ liên hệ lại với bạn sớm nhất.";
            return RedirectToPage("/Maintenance/Index");
        }

        var errorDetail = await response.Content.ReadAsStringAsync();
        ErrorMessage = $"Lỗi từ hệ thống (API): {response.StatusCode} - {errorDetail}";
        return Page();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTime.Now.Date);
        var nowTime = TimeOnly.FromDateTime(DateTime.Now);

        if (AppointmentDate < today)
        {
            yield return new ValidationResult("Ngày đặt lịch không được ở trong quá khứ.", new[] { nameof(AppointmentDate) });
        }
        else if (AppointmentDate == today && AppointmentTime <= nowTime)
        {
            yield return new ValidationResult("Giờ đặt lịch không hợp lệ (phải chọn giờ trong tương lai).", new[] { nameof(AppointmentTime) });
        }
    }
}
