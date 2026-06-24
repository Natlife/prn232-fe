using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers;

public class ComboOrderController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public ComboOrderController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    private void AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    // GET: /ComboOrder/Confirm?draft=xxx
    [HttpGet]
    public async Task<IActionResult> Confirm(string draft, string? type = null)
    {
        if (string.IsNullOrEmpty(draft))
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin giỏ hàng đặt mua.";
            return RedirectToAction("Index", "Home");
        }

        try
        {
            // Call API backend to preview and validate the items
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders/draft-preview", draft);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                try
                {
                    var doc = JsonDocument.Parse(errorMsg);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        TempData["ErrorMessage"] = msgProp.GetString();
                        return RedirectToAction("Index", "Home");
                    }
                }
                catch { }

                TempData["ErrorMessage"] = "Token giỏ hàng không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("Index", "Home");
            }

            var apiResult = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<ComboOrderPreviewViewModel>>();
            if (apiResult == null || !apiResult.Success || apiResult.Data == null)
            {
                TempData["ErrorMessage"] = "Không thể tải thông tin xem trước đơn hàng.";
                return RedirectToAction("Index", "Home");
            }

            // If user is authenticated, we prefill name/phone if possible
            ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            ViewBag.DraftToken = draft;
            ViewBag.PurchaseType = string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase)
                ? "Deposit"
                : "Buyout";

            return View(apiResult.Data);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xử lý đơn hàng: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    // POST: /ComboOrder/PlaceOrder
    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] ComboOrderCreateViewModel model)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Json(new { success = false, message = "Bạn cần đăng nhập để đặt hàng. Vui lòng đăng nhập từ góc trên màn hình và thử lại." });
        }

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Dữ liệu nhập vào không hợp lệ." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders", model);
            var content = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(content);
            var success = doc.RootElement.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            var message = doc.RootElement.TryGetProperty("message", out var mProp) ? mProp.GetString() : "Đặt hàng thất bại.";

            if (response.IsSuccessStatusCode && success)
            {
                int orderId = 0;
                try
                {
                    if (doc.RootElement.TryGetProperty("data", out var dataProp) &&
                        dataProp.TryGetProperty("comboOrderId", out var idProp))
                    {
                        orderId = idProp.GetInt32();
                    }
                }
                catch { }
                return Json(new { success = true, message, orderId });
            }
            else
            {
                return Json(new { success = false, message });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
        }
    }

    // GET: /ComboOrder/History
    [HttpGet]
    public async Task<IActionResult> History()
    {
        if (!User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/combo-orders");
            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Không thể tải lịch sử đơn hàng.";
                return RedirectToAction("Index", "Home");
            }

            var orders = await response.Content.ReadFromJsonAsync<List<ComboOrderViewModel>>();
            return View(orders ?? new List<ComboOrderViewModel>());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    // GET: /ComboOrder/AdminOrders (For Admin Dashboard)
    [HttpGet]
    public async Task<IActionResult> AdminOrders()
    {
        if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/combo-orders");
            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách đơn hàng toàn hệ thống.";
                return RedirectToAction("Index", "Home");
            }

            var orders = await response.Content.ReadFromJsonAsync<List<ComboOrderViewModel>>();
            return View(orders ?? new List<ComboOrderViewModel>());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    // PATCH: /ComboOrder/UpdateStatus?id=xxx
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusModel model)
    {
        if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Json(new { success = false, message = "Bạn không có quyền thực hiện hành động này." });
        }

        try
        {
            AttachJwtToken();
            // Use PATCH as specified in our API
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_apiBaseUrl}/api/combo-orders/{id}/status")
            {
                Content = JsonContent.Create(new { status = model.Status })
            };

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(content);
            var success = response.IsSuccessStatusCode;
            var message = doc.RootElement.TryGetProperty("message", out var mProp) ? mProp.GetString() : "Cập nhật thất bại.";

            return Json(new { success, message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> GenerateCaptcha(int id, [FromBody] GenerateComboCaptchaModel? model)
    {
        if (!User.Identity.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Json(new { success = false, message = "Báº¡n khÃ´ng cÃ³ quyá»n thá»±c hiá»‡n hÃ nh Ä‘á»™ng nÃ y." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/combo-orders/{id}/generate-captcha",
                new { code = model?.Code });

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            var success = response.IsSuccessStatusCode &&
                          doc.RootElement.TryGetProperty("success", out var sProp) &&
                          sProp.GetBoolean();
            var message = doc.RootElement.TryGetProperty("message", out var mProp)
                ? mProp.GetString()
                : "KhÃ´ng thá»ƒ táº¡o captcha.";

            string? captchaCode = null;
            if (doc.RootElement.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("captchaCode", out var codeProp))
            {
                captchaCode = codeProp.GetString();
            }

            return Json(new { success, message, captchaCode });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lá»—i káº¿t ná»‘i mÃ¡y chá»§: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> VerifyCaptcha(int id, [FromBody] VerifyComboCaptchaModel model)
    {
        if (!User.Identity.IsAuthenticated)
        {
            return Json(new { success = false, message = "Báº¡n cáº§n Ä‘Äƒng nháº­p Ä‘á»ƒ xÃ¡c thá»±c Ä‘Æ¡n hÃ ng." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/combo-orders/{id}/verify-captcha",
                new { captchaCode = model.CaptchaCode });

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            var success = response.IsSuccessStatusCode &&
                          doc.RootElement.TryGetProperty("success", out var sProp) &&
                          sProp.GetBoolean();
            var message = doc.RootElement.TryGetProperty("message", out var mProp)
                ? mProp.GetString()
                : "KhÃ´ng thá»ƒ xÃ¡c thá»±c captcha.";

            string? status = null;
            if (doc.RootElement.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("status", out var statusProp))
            {
                status = statusProp.GetString();
            }

            return Json(new { success, message, status });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Lá»—i káº¿t ná»‘i mÃ¡y chá»§: " + ex.Message });
        }
    }
}

public class UpdateStatusModel
{
    public string Status { get; set; } = null!;
}

public class GenerateComboCaptchaModel
{
    public string? Code { get; set; }
}

public class VerifyComboCaptchaModel
{
    public string CaptchaCode { get; set; } = null!;
}

// API generic wrapper helper
public class ApiResponseWrapper<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public T? Data { get; set; }
}
