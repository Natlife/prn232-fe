using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;

namespace CarSalesManagementSystemClient.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _captchasApiUrl = "http://localhost:5084/odata/DepositCaptchas";
        private readonly string _carsApiUrl = "http://localhost:5084/odata/Cars";

        public AdminController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<IActionResult> Captchas()
        {
            try
            {
                var captchaRequestUri = $"{_captchasApiUrl}?$expand=Car&$orderby=CreatedAt desc";
                var captchaResponse = await _httpClient.GetFromJsonAsync<ODataResponse<DepositCaptchaViewModel>>(captchaRequestUri);
                var captchas = captchaResponse?.Value ?? new List<DepositCaptchaViewModel>();

                var carsRequestUri = $"{_carsApiUrl}?$filter=Status eq 'Available' or Status eq 'Reserved'";
                var carsResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(carsRequestUri);
                var cars = carsResponse?.Value ?? new List<CarViewModel>();

                ViewBag.Cars = cars;
                return View(captchas);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách Captcha: " + ex.Message;
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateCaptcha(int carId, string? code)
        {
            try
            {
                var payload = new { CarId = carId, Code = code };
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/DepositCaptchas/generate", payload);

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var msg = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Tạo mã thành công.";
                    TempData["SuccessMessage"] = msg;
                }
                else
                {
                    var errorMsg = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Lỗi không xác định.";
                    TempData["ErrorMessage"] = errorMsg;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Captchas));
        }
    }
}
