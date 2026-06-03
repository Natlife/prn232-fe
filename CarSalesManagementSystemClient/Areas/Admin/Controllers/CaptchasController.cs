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
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CaptchasController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string CaptchasApiUrl => $"{_apiBaseUrl}/odata/DepositCaptchas";
        private string CarsApiUrl => $"{_apiBaseUrl}/odata/Cars";

        public CaptchasController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
        }

        private bool AttachJwtToken()
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        public async Task<IActionResult> Index()
        {
            if (!AttachJwtToken())
            {
                TempData["ErrorMessage"] = "Phien dang nhap khong co token hoac da het han. Vui long dang nhap lai.";
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
            }

            try
            {
                var captchaRequestUri = $"{CaptchasApiUrl}?$expand=Car&$orderby=CreatedAt desc";
                var captchaResponse = await _httpClient.GetFromJsonAsync<ODataResponse<DepositCaptchaViewModel>>(captchaRequestUri);

                var carsRequestUri = $"{CarsApiUrl}?$filter=Status eq 'Available' or Status eq 'Reserved'";
                var carsResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(carsRequestUri);

                ViewBag.Cars = carsResponse?.Value ?? new List<CarViewModel>();
                return View(captchaResponse?.Value ?? new List<DepositCaptchaViewModel>());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                TempData["ErrorMessage"] = "Token xac thuc da het han hoac khong hop le. Vui long dang nhap lai.";
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Khong the tai danh sach captcha: " + ex.Message;
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Generate(int carId, string? code)
        {
            try
            {
                if (!AttachJwtToken())
                {
                    TempData["ErrorMessage"] = "Phien dang nhap khong co token. Vui long dang nhap lai.";
                    return RedirectToAction(nameof(Index));
                }

                var response = await _httpClient.PostAsJsonAsync($"{CaptchasApiUrl}/generate", new { CarId = carId, Code = code });
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = jsonDoc.RootElement.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Tao ma thanh cong.";
                }
                else
                {
                    TempData["ErrorMessage"] = jsonDoc.RootElement.TryGetProperty("message", out var msgProp)
                        ? msgProp.GetString()
                        : "Loi khong xac dinh.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Co loi xay ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
