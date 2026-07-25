using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers
{
    /// <summary>
    /// Trang "Hóa đơn của tôi": khách xem hóa đơn tổng và nhập mã captcha (do nhân viên cấp)
    /// để xác nhận đặt cọc rồi tất toán (mua đứt). Cọc giữ chỗ tối đa 2 tuần.
    /// </summary>
    public class InvoicesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public InvoicesController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        // GET: /Invoices
        public async Task<IActionResult> Index()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth", new { returnUrl = "/Invoices" });
            }

            try
            {
                AttachJwtToken();
                var resp = await _httpClient.GetAsync($"{_apiBaseUrl}/api/invoices");
                if (resp.IsSuccessStatusCode)
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var content = await resp.Content.ReadAsStringAsync();
                    var list = JsonSerializer.Deserialize<List<InvoiceListItemViewModel>>(content, opts)
                               ?? new List<InvoiceListItemViewModel>();
                    return View(list);
                }
                ViewBag.ErrorMessage = "Không tải được danh sách hóa đơn (mã " + (int)resp.StatusCode + ").";
                return View(new List<InvoiceListItemViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi kết nối máy chủ: " + ex.Message;
                return View(new List<InvoiceListItemViewModel>());
            }
        }

        // GET: /Invoices/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth", new { returnUrl = "/Invoices/Details/" + id });
            }

            try
            {
                AttachJwtToken();
                var resp = await _httpClient.GetAsync($"{_apiBaseUrl}/api/invoices/{id}");
                if (resp.IsSuccessStatusCode)
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var content = await resp.Content.ReadAsStringAsync();
                    var inv = JsonSerializer.Deserialize<InvoiceListItemViewModel>(content, opts);
                    if (inv == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy hóa đơn.";
                        return RedirectToAction(nameof(Index));
                    }
                    return View(inv);
                }
                TempData["ErrorMessage"] = "Không xem được hóa đơn (mã " + (int)resp.StatusCode + ").";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi kết nối máy chủ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Invoices/VerifyDeposit
        [HttpPost]
        public Task<IActionResult> VerifyDeposit(int masterInvoiceId, string captchaCode)
            => VerifyAsync("deposit", masterInvoiceId, captchaCode);

        // POST: /Invoices/VerifyFinal
        [HttpPost]
        public Task<IActionResult> VerifyFinal(int masterInvoiceId, string captchaCode)
            => VerifyAsync("final", masterInvoiceId, captchaCode);

        private async Task<IActionResult> VerifyAsync(string stage, int masterInvoiceId, string captchaCode)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            if (string.IsNullOrWhiteSpace(captchaCode))
                return Json(new { success = false, message = "Vui lòng nhập mã xác thực." });

            try
            {
                AttachJwtToken();
                var payload = new { MasterInvoiceId = masterInvoiceId, CaptchaCode = captchaCode.Trim() };
                var resp = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/invoices/verify/{stage}", payload);
                var content = await resp.Content.ReadAsStringAsync();

                bool success = false;
                string message = "Xác thực thất bại.";
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("success", out var s)) success = s.GetBoolean();
                    if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
                }
                catch { /* giữ message mặc định */ }

                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
            }
        }
    }
}
