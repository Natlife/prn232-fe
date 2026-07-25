using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MaintenancePackagesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public MaintenancePackagesController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiUrl = $"{(configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/')}/api";
        }

        private void AppendAuthorizationHeader()
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public T? Data { get; set; }
        }

        private async Task<string> ReadApiErrorAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                return $"{(int)response.StatusCode} {response.ReasonPhrase}";

            try
            {
                var apiResult = JsonSerializer.Deserialize<ApiResponse<object>>(content, JsonOptions);
                if (apiResult != null && !string.IsNullOrEmpty(apiResult.Message))
                {
                    return apiResult.Message;
                }
            }
            catch
            {
                // Ignore parse errors and fall back to raw content
            }
            return $"{(int)response.StatusCode} {response.ReasonPhrase}: {content}";
        }

        [HttpGet("/Admin/MaintenancePackages")]
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10;
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<MaintenancePackageViewModel>>>(content, JsonOptions);
                    if (apiResult?.Success == true)
                    {
                        var allPackages = apiResult.Data ?? new List<MaintenancePackageViewModel>();
                        int totalItems = allPackages.Count;
                        int totalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);
                        if (totalPages == 0) totalPages = 1;
                        if (page < 1) page = 1;
                        if (page > totalPages) page = totalPages;

                        ViewBag.CurrentPage = page;
                        ViewBag.TotalPages = totalPages;
                        
                        try {
                            var srvResp = await _httpClient.GetAsync($"{_apiUrl}/Services/available");
                            if (srvResp.IsSuccessStatusCode) {
                                var srvContent = await srvResp.Content.ReadAsStringAsync();
                                var srvResult = JsonSerializer.Deserialize<ApiResponse<List<ServiceSummaryViewModel>>>(srvContent, JsonOptions);
                                if (srvResult?.Success == true) {
                                    ViewBag.Services = srvResult.Data;
                                }
                            }
                        } catch { }

                        var paginatedData = allPackages.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        return View(paginatedData);
                    }
                }

                TempData["ErrorMessage"] = "Khong the tai danh sach goi bao duong: " + await ReadApiErrorAsync(response);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Khong the tai danh sach goi bao duong: " + ex.Message;
            }

            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;

            // Fetch Services for dropdown
            try {
                var srvResp = await _httpClient.GetAsync($"{_apiUrl}/Services/available");
                if (srvResp.IsSuccessStatusCode) {
                    var srvContent = await srvResp.Content.ReadAsStringAsync();
                    var srvResult = JsonSerializer.Deserialize<ApiResponse<List<ServiceSummaryViewModel>>>(srvContent, JsonOptions);
                    if (srvResult?.Success == true) {
                        ViewBag.Services = srvResult.Data;
                    }
                }
            } catch { }

            return View(new List<MaintenancePackageViewModel>());
        }

        [HttpGet("/Admin/MaintenancePackages/Get/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<MaintenancePackageViewModel>>(content, JsonOptions);
                    if (apiResult?.Success == true && apiResult.Data != null)
                    {
                        return Json(new { success = true, data = apiResult.Data });
                    }
                }

                return Json(new { success = false, message = "Khong the tai goi bao duong: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Khong the tai goi bao duong: " + ex.Message });
            }
        }

        [HttpPost("/Admin/MaintenancePackages/Save")]
        public async Task<IActionResult> Save([FromForm] MaintenancePackageViewModel model)
        {
            try
            {
                AppendAuthorizationHeader();
                var body = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
                var response = model.PackageId == 0
                    ? await _httpClient.PostAsync($"{_apiUrl}/MaintenancePackages", body)
                    : await _httpClient.PutAsync($"{_apiUrl}/MaintenancePackages/{model.PackageId}", body);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = model.PackageId == 0 ? "Them goi bao duong thanh cong!" : "Cap nhat thanh cong!"
                    });
                }

                return Json(new { success = false, message = "Luu that bai: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Luu that bai: " + ex.Message });
            }
        }

        [HttpDelete("/Admin/MaintenancePackages/Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"{_apiUrl}/MaintenancePackages/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Xoa goi bao duong thanh cong!" });
                }

                return Json(new { success = false, message = "Xoa that bai: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Xoa that bai: " + ex.Message });
            }
        }
    }
}
