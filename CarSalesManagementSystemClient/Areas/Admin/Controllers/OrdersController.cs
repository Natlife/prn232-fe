using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using CarSalesManagementSystemClient.Models;
using System.Text;
using System.Linq;
using System;
using System.Net.Http.Json;

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

        // GET: Admin/Orders
        public async Task<IActionResult> Index(string type = "All", string status = "All", int page = 1)
        {
            int pageSize = 10;
            AppendAuthorizationHeader();

            var response = await _httpClient.GetAsync($"{_apiUrl}/admin/orders");
            var ordersList = new List<AdminOrderListItemViewModel>();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                ordersList = JsonSerializer.Deserialize<List<AdminOrderListItemViewModel>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AdminOrderListItemViewModel>();
            }

            // 1. Filter by Order Type
            if (type != "All")
            {
                ordersList = ordersList.Where(o => o.OrderType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 2. Filter by Status (Unified filter)
            if (status != "All")
            {
                if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    // Chờ xử lý: Đơn phụ tùng ở trạng thái Pending
                    ordersList = ordersList.Where(o => o.OrderType == "Part" && o.ProcessingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else if (status.Equals("InProgress", StringComparison.OrdinalIgnoreCase))
                {
                    // Đang thực hiện: Bảo dưỡng ở trạng thái InProgress, Phụ tùng lẻ ở trạng thái Confirmed hoặc Shipping
                    ordersList = ordersList.Where(o => 
                        (o.OrderType == "Maintenance" && o.ProcessingStatus.Equals("InProgress", StringComparison.OrdinalIgnoreCase)) ||
                        (o.OrderType == "Part" && (o.ProcessingStatus.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || o.ProcessingStatus.Equals("Shipping", StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                else if (status.Equals("Unpaid", StringComparison.OrdinalIgnoreCase))
                {
                    // Chờ thanh toán: Đơn bất kỳ có PaymentStatus == "Unpaid"
                    ordersList = ordersList.Where(o => o.PaymentStatus.Equals("Unpaid", StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    // Hoàn thành: Có PaymentStatus == "Paid"
                    ordersList = ordersList.Where(o => o.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    // Đã hủy: ProcessingStatus == "Cancelled"
                    ordersList = ordersList.Where(o => o.ProcessingStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            int totalItems = ordersList.Count;
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var paginatedData = ordersList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SelectedType = type;
            ViewBag.SelectedStatus = status;

            return View(paginatedData);
        }

        // GET: Admin/Orders/GetMaintenanceDetail/{id}
        [HttpGet]
        public async Task<IActionResult> GetMaintenanceDetail(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenanceAppointments/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            return NotFound(new { message = "Không tìm thấy lịch bảo dưỡng." });
        }

        // GET: Admin/Orders/GetPartDetail/{id}
        [HttpGet]
        public async Task<IActionResult> GetPartDetail(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/PartOrders/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            return NotFound(new { message = "Không tìm thấy đơn phụ tùng." });
        }

        // GET: Admin/Orders/GetAllParts
        [HttpGet]
        public async Task<IActionResult> GetAllParts()
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.GetAsync($"{_apiUrl}/Parts");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            return BadRequest(new { message = "Không thể tải danh sách phụ tùng." });
        }

        // POST: Admin/Orders/ConfirmMaintenance/{id}
        [HttpPost]
        public async Task<IActionResult> ConfirmMaintenance(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/maintenance/{id}/confirm", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xác nhận lịch bảo dưỡng thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/StartMaintenance/{id}
        [HttpPost]
        public async Task<IActionResult> StartMaintenance(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/maintenance/{id}/start", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Đã tiếp nhận xe và bắt đầu bảo dưỡng!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/AddConsumedPart/{id}
        [HttpPost]
        public async Task<IActionResult> AddConsumedPart(int id, int partId, int quantity, decimal? unitPrice)
        {
            AppendAuthorizationHeader();
            var payload = new { PartId = partId, Quantity = quantity, UnitPrice = unitPrice };
            var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/admin/orders/maintenance/{id}/consumed-parts", payload);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Thêm phụ tùng phát sinh thành công!" });
            }
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            string msg = error.TryGetProperty("message", out var p) ? p.GetString() ?? "Lỗi" : "Có lỗi xảy ra.";
            return Json(new { success = false, message = msg });
        }

        // POST: Admin/Orders/CompleteMaintenance/{id}
        [HttpPost]
        public async Task<IActionResult> CompleteMaintenance(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/maintenance/{id}/complete", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Hoàn thành bảo dưỡng dịch vụ thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/ConfirmPaymentMaintenance/{id}
        [HttpPost]
        public async Task<IActionResult> ConfirmPaymentMaintenance(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/maintenance/{id}/confirm-payment", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xác nhận thanh toán dịch vụ thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/CancelMaintenance/{id}
        [HttpPost]
        public async Task<IActionResult> CancelMaintenance(int id, string reason)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsJsonAsync($"{_apiUrl}/admin/orders/maintenance/{id}/cancel", new { Reason = reason });
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Hủy lịch bảo dưỡng thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/ConfirmPartOrder/{id}
        [HttpPost]
        public async Task<IActionResult> ConfirmPartOrder(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/part/{id}/confirm", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xác nhận đơn phụ tùng thành công!" });
            }
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            string msg = error.TryGetProperty("message", out var p) ? p.GetString() ?? "Lỗi" : "Có lỗi xảy ra.";
            return Json(new { success = false, message = msg });
        }

        // POST: Admin/Orders/ShippingPartOrder/{id}
        [HttpPost]
        public async Task<IActionResult> ShippingPartOrder(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/part/{id}/shipping", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Đang tiến hành giao hàng!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/CompletePartOrder/{id}
        [HttpPost]
        public async Task<IActionResult> CompletePartOrder(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/part/{id}/complete", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Hoàn thành đơn hàng phụ tùng!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/ConfirmPaymentPartOrder/{id}
        [HttpPost]
        public async Task<IActionResult> ConfirmPaymentPartOrder(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/part/{id}/confirm-payment", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Xác nhận thanh toán đơn phụ tùng thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }

        // POST: Admin/Orders/CancelPartOrder/{id}
        [HttpPost]
        public async Task<IActionResult> CancelPartOrder(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/admin/orders/part/{id}/cancel", null);
            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = true, message = "Hủy đơn hàng phụ tùng thành công!" });
            }
            var error = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = error });
        }
    }
}
