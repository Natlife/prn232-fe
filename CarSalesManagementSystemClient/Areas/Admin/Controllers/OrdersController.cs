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
    public class OrdersController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api";

        public OrdersController(IHttpClientFactory httpClientFactory)
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
            public string? Message { get; set; }
            public T? Data { get; set; }
        }

        // GET: Admin/Orders
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10;
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

                    int totalItems = sortedData.Count;
                    int totalPages = (int)System.Math.Ceiling(totalItems / (double)pageSize);
                    if (totalPages == 0) totalPages = 1;
                    if (page < 1) page = 1;
                    if (page > totalPages) page = totalPages;

                    ViewBag.CurrentPage = page;
                    ViewBag.TotalPages = totalPages;

                    var paginatedData = sortedData.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                    return View(paginatedData);
                }
            }

            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = 1;
            return View(new List<AppointmentHistoryViewModel>());
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            AppendAuthorizationHeader();

            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/pay", null);

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Thanh toán thành công!" });
            }

            return Json(new { success = false, message = "Có lỗi xảy ra khi xác nhận thanh toán." });
        }
        
        [HttpPost]
        public async Task<IActionResult> SaveExtraFee(int id, decimal fee)
        {
            AppendAuthorizationHeader();
            
            var reqObj = new { ExtraFee = fee };
            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/extrafee",
                new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Lưu phí phát sinh thành công!" });
            }
            return Json(new { success = false, message = "Không thể lưu phí phát sinh." });
        }
        
        [HttpPost]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            AppendAuthorizationHeader();

            var reqObj = new { Status = "Confirmed" };

            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/status",
                new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xác nhận đơn hàng thành công!" });
            }

            return Json(new { success = false, message = "Có lỗi xảy ra khi xác nhận đơn." });
        }
    }
}
