using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using CarSalesManagementSystemClient.Models;

namespace CarSalesManagementSystemClient.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "http://localhost:5084";

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

        // GET: /Orders/Index
        public async Task<IActionResult> Index(string? type)
        {
            try
            {
                AppendAuthorizationHeader();
                string url = $"{_apiBaseUrl}/api/customer/orders";
                if (!string.IsNullOrEmpty(type) && !type.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    url += $"?type={type}";
                }
                
                var response = await _httpClient.GetFromJsonAsync<List<CustomerOrderViewModel>>(url);
                ViewBag.CurrentType = type ?? "All";
                return View(response ?? new List<CustomerOrderViewModel>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Không thể tải danh sách đơn hàng: " + ex.Message;
                ViewBag.CurrentType = type ?? "All";
                return View(new List<CustomerOrderViewModel>());
            }
        }

        // GET: /Orders/Details
        public async Task<IActionResult> Details(string type, int id)
        {
            try
            {
                AppendAuthorizationHeader();
                string url = $"{_apiBaseUrl}/api/customer/orders/{type}/{id}";
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Không thể tìm thấy thông tin đơn hàng.";
                    return RedirectToAction("Index");
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // Switch rendering model based on type
                if (type.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
                {
                    var detail = JsonSerializer.Deserialize<MaintenanceOrderDetailsWrapper>(content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ViewBag.OrderType = "Maintenance";
                    return View("MaintenanceDetails", detail);
                }
                else
                {
                    var detail = JsonSerializer.Deserialize<PartOrderDetailsWrapper>(content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    ViewBag.OrderType = "Part";
                    return View("PartDetails", detail);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: /Orders/Pay
        [HttpPost]
        public async Task<IActionResult> Pay(string type, int id)
        {
            try
            {
                AppendAuthorizationHeader();
                string url = $"{_apiBaseUrl}/api/customer/orders/pay/{type}/{id}";
                
                var response = await _httpClient.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Thanh toán đơn hàng thành công!";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = "Thanh toán thất bại: " + error;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối: " + ex.Message;
            }
            return RedirectToAction("Details", new { type = type, id = id });
        }

        // POST: /Orders/CancelPartOrder
        [HttpPost]
        public async Task<IActionResult> CancelPartOrder(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                string url = $"{_apiBaseUrl}/api/customer/orders/cancel/Part/{id}";
                
                var response = await _httpClient.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Hủy đơn hàng thành công!";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = "Hủy đơn hàng thất bại: " + error;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối: " + ex.Message;
            }
            return RedirectToAction("Details", new { type = "Part", id = id });
        }

        // POST: /Orders/ReceivePartOrder
        [HttpPost]
        public async Task<IActionResult> ReceivePartOrder(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                string url = $"{_apiBaseUrl}/api/customer/orders/receive/Part/{id}";
                
                var response = await _httpClient.PutAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    TempData["Success"] = "Đã xác nhận nhận hàng thành công!";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = "Xác nhận thất bại: " + error;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối: " + ex.Message;
            }
            return RedirectToAction("Details", new { type = "Part", id = id });
        }
    }
}
