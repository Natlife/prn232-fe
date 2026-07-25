using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CarSalesManagementSystemClient.Controllers
{
    public class ComboOrderController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api/ComboOrders";

        public ComboOrderController(IHttpClientFactory httpClientFactory)
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

        // GET: ComboOrder/Confirm
        public IActionResult Confirm(string draft, string type)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                var returnUrl = Url.Action("Confirm", "ComboOrder", new { draft = draft, type = type });
                return RedirectToAction("Login", "Auth", new { returnUrl = returnUrl });
            }

            if (string.IsNullOrEmpty(draft))
            {
                TempData["ErrorMessage"] = "Không tìm thấy dữ liệu đơn hàng combo nháp.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                byte[] data = Convert.FromBase64String(draft);
                string json = Encoding.UTF8.GetString(data);
                var items = JsonSerializer.Deserialize<List<ComboOrderItemViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ComboOrderItemViewModel>();

                if (!items.Any())
                {
                    TempData["ErrorMessage"] = "Đơn hàng combo nháp không có sản phẩm nào.";
                    return RedirectToAction("Index", "Home");
                }

                decimal total = items.Sum(i => i.SubTotal);
                ViewBag.SuggestedItems = items;
                ViewBag.TotalAmount = total;
                ViewBag.PurchaseType = type ?? "Buyout";

                var name = User.Identity.Name ?? "";
                var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value ?? "";
                var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

                var model = new ComboOrderConfirmViewModel
                {
                    Draft = draft,
                    PurchaseType = type ?? "Buyout",
                    CustomerName = name,
                    CustomerPhone = phone,
                    CustomerEmail = email
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xử lý đơn hàng combo nháp: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: ComboOrder/Confirm
        [HttpPost]
        public async Task<IActionResult> Confirm(ComboOrderConfirmViewModel model)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                byte[] data = Convert.FromBase64String(model.Draft);
                string json = Encoding.UTF8.GetString(data);
                var items = JsonSerializer.Deserialize<List<ComboOrderItemViewModel>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ComboOrderItemViewModel>();

                if (!items.Any())
                {
                    ModelState.AddModelError("", "Đơn hàng combo không hợp lệ hoặc không có sản phẩm.");
                    ViewBag.SuggestedItems = items;
                    ViewBag.TotalAmount = 0m;
                    return View(model);
                }

                decimal total = items.Sum(i => i.SubTotal);

                if (!ModelState.IsValid)
                {
                    ViewBag.SuggestedItems = items;
                    ViewBag.TotalAmount = total;
                    return View(model);
                }

                var payload = new
                {
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    CustomerEmail = model.CustomerEmail,
                    ShippingAddress = model.ShippingAddress,
                    Note = model.Note,
                    TotalAmount = total,
                    Source = "Chatbot",
                    PurchaseType = model.PurchaseType,
                    Items = items.Select(i => new
                    {
                        ItemType = i.ItemType,
                        ReferenceId = i.ReferenceId,
                        ItemName = i.ItemName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        SubTotal = i.SubTotal
                    }).ToList()
                };

                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync(_apiUrl, payload);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Đã đặt hàng combo thành công! Vui lòng kiểm tra trạng thái đơn hàng.";
                    return RedirectToAction("History");
                }

                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Lỗi từ máy chủ: {error}");
                ViewBag.SuggestedItems = items;
                ViewBag.TotalAmount = total;
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Đã xảy ra lỗi kết nối: " + ex.Message);
                ViewBag.SuggestedItems = new List<ComboOrderItemViewModel>();
                ViewBag.TotalAmount = 0m;
                return View(model);
            }
        }

        // GET: ComboOrder/History
        public async Task<IActionResult> History()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync(_apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<ComboOrderViewModel>>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResult != null && apiResult.Success && apiResult.Data != null)
                    {
                        var sorted = apiResult.Data.OrderByDescending(o => o.CreatedAt).ToList();
                        return View(sorted);
                    }
                }

                ViewBag.ErrorMessage = "Không thể lấy lịch sử đơn hàng combo.";
                return View(new List<ComboOrderViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi kết nối: " + ex.Message;
                return View(new List<ComboOrderViewModel>());
            }
        }

        // GET: ComboOrder/AdminOrders
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminOrders()
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.GetAsync(_apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<ComboOrderViewModel>>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResult != null && apiResult.Success && apiResult.Data != null)
                    {
                        var sorted = apiResult.Data.OrderByDescending(o => o.CreatedAt).ToList();
                        return View(sorted);
                    }
                }

                ViewBag.ErrorMessage = "Không thể lấy danh sách quản trị đơn combo.";
                return View(new List<ComboOrderViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi kết nối: " + ex.Message;
                return View(new List<ComboOrderViewModel>());
            }
        }

        // POST: ComboOrder/GenerateCaptcha
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateCaptcha(int id, string? code)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/{id}/generate-captcha", new { Code = code });
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && apiResult != null && apiResult.Success)
                {
                    var captchaVal = apiResult.Data.GetProperty("captcha").GetString();
                    return Json(new { success = true, message = apiResult.Message, captcha = captchaVal });
                }

                return Json(new { success = false, message = apiResult?.Message ?? "Không thể tạo captcha." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: ComboOrder/VerifyCaptcha
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> VerifyCaptcha(int id, string captchaCode)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/{id}/verify-captcha", new { CaptchaCode = captchaCode });
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<object>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && apiResult != null && apiResult.Success)
                {
                    return Json(new { success = true, message = apiResult.Message });
                }

                return Json(new { success = false, message = apiResult?.Message ?? "Xác nhận captcha thất bại." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: ComboOrder/CancelOrder
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsync($"{_apiUrl}/{id}/cancel", null);
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<object>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && apiResult != null && apiResult.Success)
                {
                    return Json(new { success = true, message = apiResult.Message });
                }

                return Json(new { success = false, message = apiResult?.Message ?? "Hủy đơn hàng thất bại." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }
    }
}
