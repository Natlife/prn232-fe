using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Areas.Admin.Controllers
{
    /// <summary>
    /// Quản lý hóa đơn tổng &amp; sinh mã captcha (đặt cọc / mua đứt) cho nhân viên.
    /// Nhân viên sinh mã ở đây rồi cung cấp cho khách để khách nhập xác thực ở trang "Hóa đơn của tôi".
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin,Staff")]
    public class CaptchasController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public CaptchasController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
        }

        private bool AttachJwtToken()
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (string.IsNullOrEmpty(token)) return false;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        // GET: /Admin/Captchas
        public async Task<IActionResult> Index()
        {
            if (!AttachJwtToken())
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return View(new List<InvoiceListItemViewModel>());
            }

            try
            {
                var resp = await _httpClient.GetAsync($"{_apiBaseUrl}/api/invoices");
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Không tải được danh sách hóa đơn (mã " + (int)resp.StatusCode + ").";
                    return View(new List<InvoiceListItemViewModel>());
                }

                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var content = await resp.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<InvoiceListItemViewModel>>(content, opts)
                           ?? new List<InvoiceListItemViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách hóa đơn: " + ex.Message;
                return View(new List<InvoiceListItemViewModel>());
            }
        }

        // GET: /Admin/Captchas/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (!AttachJwtToken())
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var resp = await _httpClient.GetAsync($"{_apiBaseUrl}/api/invoices/{id}");
                if (!resp.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Không xem được hóa đơn (mã " + (int)resp.StatusCode + ").";
                    return RedirectToAction(nameof(Index));
                }
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
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi kết nối máy chủ: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Admin/Captchas/GenerateDeposit
        [HttpPost]
        public Task<IActionResult> GenerateDeposit(int masterInvoiceId)
            => GenerateAsync("deposit", masterInvoiceId);

        // POST: /Admin/Captchas/GenerateFinal
        [HttpPost]
        public Task<IActionResult> GenerateFinal(int masterInvoiceId)
            => GenerateAsync("final", masterInvoiceId);

        private async Task<IActionResult> GenerateAsync(string stage, int masterInvoiceId)
        {
            if (!AttachJwtToken())
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                HttpResponseMessage response;
                if (stage == "deposit")
                {
                    response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/invoices/deposit-captcha",
                        new { MasterInvoiceId = masterInvoiceId, DepositExpiresInDays = 14 });
                }
                else
                {
                    response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/invoices/{masterInvoiceId}/final-captcha", null);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                string? message = null, captcha = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("message", out var m)) message = m.GetString();
                    if (doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("captchaCode", out var c))
                        captcha = c.GetString();
                }
                catch { /* giữ nguyên */ }

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = (message ?? "Đã sinh mã thành công.")
                        + (string.IsNullOrEmpty(captcha) ? "" : $" — MÃ: {captcha} (cung cấp cho khách).");
                }
                else
                {
                    TempData["ErrorMessage"] = message ?? "Sinh mã thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
