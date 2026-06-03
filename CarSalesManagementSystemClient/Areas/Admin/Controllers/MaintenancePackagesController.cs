using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using CarSalesManagementSystemClient.Models;
using System.Text;

namespace CarSalesManagementSystemClient.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MaintenancePackagesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api";

        public MaintenancePackagesController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        private void AppendAuthorizationHeader()
        {
            var token = User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public T Data { get; set; }
        }

        // GET: Admin/MaintenancePackages
        public async Task<IActionResult> Index()
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<List<MaintenancePackageViewModel>>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (apiResult != null && apiResult.Success)
                {
                    return View(apiResult.Data ?? new List<MaintenancePackageViewModel>());
                }
            }
            return View(new List<MaintenancePackageViewModel>());
        }

        // POST: Admin/MaintenancePackages/Save
        [HttpPost]
        public async Task<IActionResult> Save([FromForm] MaintenancePackageViewModel model)
        {
            AppendAuthorizationHeader();
            HttpResponseMessage response;

            if (model.PackageId == 0)
            {
                // Create
                response = await _httpClient.PostAsync($"{_apiUrl}/MaintenancePackages", 
                    new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));
            }
            else
            {
                // Update
                response = await _httpClient.PutAsync($"{_apiUrl}/MaintenancePackages/{model.PackageId}", 
                    new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));
            }

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = model.PackageId == 0 ? "Thêm gói bảo dưỡng thành công!" : "Cập nhật thành công!" });
            }

            return Json(new { success = false, message = "Có lỗi xảy ra khi lưu dữ liệu." });
        }

        // DELETE: Admin/MaintenancePackages/Delete/5
        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.DeleteAsync($"{_apiUrl}/MaintenancePackages/{id}");
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xóa gói bảo dưỡng thành công!" });
            }
            return Json(new { success = false, message = "Không thể xóa gói bảo dưỡng này." });
        }
        
        // GET: Admin/MaintenancePackages/Get/5 (for Edit modal)
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<MaintenancePackageViewModel>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (apiResult != null && apiResult.Success)
                {
                    return Json(new { success = true, data = apiResult.Data });
                }
            }
            return Json(new { success = false, message = "Không tìm thấy gói bảo dưỡng." });
        }
    }
}
