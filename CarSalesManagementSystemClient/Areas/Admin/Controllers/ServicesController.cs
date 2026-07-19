using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace CarSalesManagementSystemClient.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ServicesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public ServicesController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

        [HttpGet("/Admin/Services")]
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10;
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync($"{_apiUrl}/Services");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<ServiceSummaryViewModel>>>(content, JsonOptions);
                    if (apiResult?.Success == true)
                    {
                        var allServices = apiResult.Data ?? new List<ServiceSummaryViewModel>();
                        int totalItems = allServices.Count;
                        int totalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);
                        if (totalPages == 0) totalPages = 1;
                        if (page < 1) page = 1;
                        if (page > totalPages) page = totalPages;

                        ViewBag.CurrentPage = page;
                        ViewBag.TotalPages = totalPages;

                        var paginatedData = allServices.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        return View(paginatedData);
                    }
                }

                TempData["ErrorMessage"] = "Không thể tải danh sách dịch vụ: " + await ReadApiErrorAsync(response);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách dịch vụ: " + ex.Message;
            }

            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;
            return View(new List<ServiceSummaryViewModel>());
        }

        [HttpGet("/Admin/Services/Get/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync($"{_apiUrl}/Services/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<ServiceSummaryViewModel>>(content, JsonOptions);
                    if (apiResult?.Success == true && apiResult.Data != null)
                    {
                        return Json(new { success = true, data = apiResult.Data });
                    }
                }

                return Json(new { success = false, message = "Không thể tải dịch vụ: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Không thể tải dịch vụ: " + ex.Message });
            }
        }

        [HttpPost("/Admin/Services/Save")]
        public async Task<IActionResult> Save([FromForm] ServiceSummaryViewModel model)
        {
            try
            {
                AppendAuthorizationHeader();
                var body = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
                var response = model.ServiceId == 0
                    ? await _httpClient.PostAsync($"{_apiUrl}/Services", body)
                    : await _httpClient.PutAsync($"{_apiUrl}/Services/{model.ServiceId}", body);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new
                    {
                        success = true,
                        message = model.ServiceId == 0 ? "Thêm dịch vụ thành công!" : "Cập nhật thành công!"
                    });
                }

                return Json(new { success = false, message = "Lưu thất bại: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Lưu thất bại: " + ex.Message });
            }
        }

        [HttpDelete("/Admin/Services/Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"{_apiUrl}/Services/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Xóa dịch vụ thành công!" });
                }

                return Json(new { success = false, message = "Xóa thất bại: " + await ReadApiErrorAsync(response) });
            }
            catch (System.Exception ex)
            {
                return Json(new { success = false, message = "Xóa thất bại: " + ex.Message });
            }
        }
    }
}
