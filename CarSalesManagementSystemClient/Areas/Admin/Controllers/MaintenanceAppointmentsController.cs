using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using CarSalesManagementSystemClient.Models;
using System.Text;
using System.Linq;

namespace CarSalesManagementSystemClient.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MaintenanceAppointmentsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api";

        public MaintenanceAppointmentsController(IHttpClientFactory httpClientFactory)
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

        // GET: Admin/MaintenanceAppointments
        public async Task<IActionResult> Index()
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenanceAppointments");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<List<AppointmentHistoryViewModel>>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (apiResult != null && apiResult.Success && apiResult.Data != null)
                {
                    // Sort descending by date
                    var sortedData = apiResult.Data.OrderByDescending(a => a.CreatedAt).ToList();
                    return View(sortedData);
                }
            }
            return View(new List<AppointmentHistoryViewModel>());
        }

        // POST: Admin/MaintenanceAppointments/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status, string reason = null)
        {
            AppendAuthorizationHeader();

            var reqObj = new { Status = status, Reason = reason };

            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/status",
                new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }

            return Json(new { success = false, message = "Cập nhật thất bại." });
        }
    }
}
