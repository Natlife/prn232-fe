using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string CaptchasApiUrl => $"{_apiBaseUrl}/odata/DepositCaptchas";
        private string CarsApiUrl => $"{_apiBaseUrl}/odata/Cars";
        private string BrandsApiUrl => $"{_apiBaseUrl}/odata/CarBrands";

        public AdminController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

        private async Task<string> ReadApiErrorAsync(HttpResponseMessage response, string requestUri)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"({(int)response.StatusCode} {response.StatusCode}) - {requestUri}: {error}";
        }

        // GET: Admin/Cars
        public async Task<IActionResult> Cars()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(
                    $"{CarsApiUrl}?$filter=Status ne 'Inactive'&$expand=Brand&$orderby=CreatedAt desc");
                var cars = response?.Value ?? new List<CarViewModel>();
                return View(cars);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách xe: " + ex.Message;
                return View(new List<CarViewModel>());
            }
        }

        // GET: Admin/CreateCar
        public async Task<IActionResult> CreateCar()
        {
            await LoadBrandsToViewBag();
            return View(new CarFormViewModel());
        }

        // POST: Admin/CreateCar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCar(CarFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                await LoadBrandsToViewBag();
                return View(form);
            }

            try
            {
                if (!AttachJwtToken())
                {
                    TempData["ErrorMessage"] = "PhiÃªn Ä‘Äƒng nháº­p khÃ´ng cÃ³ token. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.";
                    await LoadBrandsToViewBag();
                    return View(form);
                }
                var payload = new
                {
                    BrandId = form.BrandId,
                    CarName = form.CarName,
                    Model = form.Model,
                    Year = form.Year,
                    Color = form.Color,
                    Mileage = form.Mileage,
                    FuelType = form.FuelType,
                    Transmission = form.Transmission,
                    Price = form.Price,
                    Description = form.Description,
                    ImageUrl = form.ImageUrl,
                    Status = form.Status,
                    CreatedAt = DateTime.Now
                };

                var response = await _httpClient.PostAsync(CarsApiUrl,
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Thêm xe thành công!";
                    return RedirectToAction(nameof(Cars));
                }

                var error = await ReadApiErrorAsync(response, CarsApiUrl);
                TempData["ErrorMessage"] = "Thêm xe thất bại: " + error;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(form);
        }

        // GET: Admin/EditCar/5
        public async Task<IActionResult> EditCar(int id)
        {
            try
            {
                var car = await _httpClient.GetFromJsonAsync<CarViewModel>($"{CarsApiUrl}({id})");
                if (car == null) return NotFound();

                await LoadBrandsToViewBag();
                var form = new CarFormViewModel
                {
                    CarId = car.CarId,
                    BrandId = car.BrandId,
                    CarName = car.CarName,
                    Model = car.Model,
                    Year = car.Year,
                    Color = car.Color,
                    Mileage = car.Mileage,
                    FuelType = car.FuelType,
                    Transmission = car.Transmission,
                    Price = car.Price,
                    Description = car.Description,
                    ImageUrl = car.ImageUrl,
                    Status = car.Status
                };
                return View(form);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải thông tin xe: " + ex.Message;
                return RedirectToAction(nameof(Cars));
            }
        }

        // POST: Admin/EditCar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCar(int id, CarFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                await LoadBrandsToViewBag();
                return View(form);
            }

            try
            {
                if (!AttachJwtToken())
                {
                    TempData["ErrorMessage"] = "PhiÃªn Ä‘Äƒng nháº­p khÃ´ng cÃ³ token. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.";
                    await LoadBrandsToViewBag();
                    return View(form);
                }
                var payload = new
                {
                    CarId = id,
                    BrandId = form.BrandId,
                    CarName = form.CarName,
                    Model = form.Model,
                    Year = form.Year,
                    Color = form.Color,
                    Mileage = form.Mileage,
                    FuelType = form.FuelType,
                    Transmission = form.Transmission,
                    Price = form.Price,
                    Description = form.Description,
                    ImageUrl = form.ImageUrl,
                    Status = form.Status,
                    CreatedAt = DateTime.Now
                };

                var response = await _httpClient.PutAsync($"{CarsApiUrl}({id})",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Cập nhật xe thành công!";
                    return RedirectToAction(nameof(Cars));
                }

                var error = await ReadApiErrorAsync(response, $"{CarsApiUrl}({id})");
                TempData["ErrorMessage"] = "Cập nhật thất bại: " + error;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(form);
        }

        // POST: Admin/DeleteCar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCar(int id)
        {
            try
            {
                if (!AttachJwtToken())
                {
                    TempData["ErrorMessage"] = "PhiÃªn Ä‘Äƒng nháº­p khÃ´ng cÃ³ token. Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i.";
                    return RedirectToAction(nameof(Cars));
                }
                var response = await _httpClient.DeleteAsync($"{CarsApiUrl}({id})");

                if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Xóa xe thành công!";
                else
                {
                    var error = await ReadApiErrorAsync(response, $"{CarsApiUrl}({id})");
                    TempData["ErrorMessage"] = "Xóa thất bại: " + error;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Cars));
        }

        // ─── Captcha Management ──────────────────────────────────────────────

        public async Task<IActionResult> Captchas()
        {
            if (!AttachJwtToken())
            {
                TempData["ErrorMessage"] = "Phiên đăng nhập không có token hoặc đã hết hạn. Vui lòng đăng nhập lại.";
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
            }

            try
            {
                var captchaRequestUri = $"{CaptchasApiUrl}?$expand=Car&$orderby=CreatedAt desc";
                var captchaResponse = await _httpClient.GetFromJsonAsync<ODataResponse<DepositCaptchaViewModel>>(captchaRequestUri);
                var captchas = captchaResponse?.Value ?? new List<DepositCaptchaViewModel>();

                var carsRequestUri = $"{CarsApiUrl}?$filter=Status eq 'Available' or Status eq 'Reserved'";
                var carsResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(carsRequestUri);
                var cars = carsResponse?.Value ?? new List<CarViewModel>();

                ViewBag.Cars = cars;
                return View(captchas);
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                TempData["ErrorMessage"] = "Token xác thực đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.";
                ViewBag.Cars = new List<CarViewModel>();
                return View(new List<DepositCaptchaViewModel>());
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
                AttachJwtToken();
                var payload = new { CarId = carId, Code = code };
                var response = await _httpClient.PostAsJsonAsync($"{CaptchasApiUrl}/generate", payload);

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(responseContent);

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

        // ─── Helpers ─────────────────────────────────────────────────────────

        private async Task LoadBrandsToViewBag()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarBrandViewModel>>(BrandsApiUrl);
                ViewBag.Brands = response?.Value ?? new List<CarBrandViewModel>();
            }
            catch
            {
                ViewBag.Brands = new List<CarBrandViewModel>();
            }
        }
    }
}
