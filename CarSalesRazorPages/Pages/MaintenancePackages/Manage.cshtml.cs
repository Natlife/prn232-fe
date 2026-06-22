using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.MaintenancePackages;

[Authorize(Roles = "Admin")]
public class ManageModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "http://localhost:5084/odata/MaintenancePackages";

    public ManageModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<MaintenancePackage> Packages { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ODataResponse<MaintenancePackage>>(ApiUrl);
            Packages = response?.Value ?? new List<MaintenancePackage>();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải danh sách gói bảo dưỡng: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostCreateAsync([FromBody] MaintenancePackage package)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(ApiUrl, package);
            if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Thêm gói thành công!" });
            return new JsonResult(new { success = false, message = "Thêm gói thất bại: " + await response.Content.ReadAsStringAsync() });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message }); }
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, [FromBody] MaintenancePackage package)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{ApiUrl}({id})", package);
            if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Cập nhật gói thành công!" });
            return new JsonResult(new { success = false, message = "Cập nhật thất bại: " + await response.Content.ReadAsStringAsync() });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message }); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{ApiUrl}({id})");
            if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Xóa gói thành công!" });
            return new JsonResult(new { success = false, message = "Xóa thất bại: " + await response.Content.ReadAsStringAsync() });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message }); }
    }
}
