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

namespace CarSalesManagementSystemClient.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _captchasApiUrl = "http://localhost:5084/odata/DepositCaptchas";
        private readonly string _carsApiUrl = "http://localhost:5084/odata/Cars";
        private readonly string _carsRestApiUrl = "http://localhost:5084/odata/Cars";
        private readonly string _brandsApiUrl = "http://localhost:5084/odata/CarBrands";

        public AdminController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        private void AttachJwtToken()
        {
            var token = User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        // GET: Admin/Cars
        public async Task<IActionResult> Cars()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(
                    $"{_carsApiUrl}?$expand=Brand&$orderby=CreatedAt desc");
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
        public async Task<IActionResult> CreateCar(CarFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadBrandsToViewBag();
                return View(model);
            }

            try
            {
                AttachJwtToken();
                var payload = new
                {
                    BrandId = model.BrandId,
                    CarName = model.CarName,
                    Model = model.Model,
                    Year = model.Year,
                    Color = model.Color,
                    Mileage = model.Mileage,
                    FuelType = model.FuelType,
                    Transmission = model.Transmission,
                    Price = model.Price,
                    Description = model.Description,
                    ImageUrl = model.ImageUrl,
                    Status = model.Status,
                    CreatedAt = DateTime.Now
                };

                var response = await _httpClient.PostAsync(_carsRestApiUrl,
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Thêm xe thành công!";
                    return RedirectToAction(nameof(Cars));
                }

                var error = await response.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = "Thêm xe thất bại: " + error;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(model);
        }

        // GET: Admin/EditCar/5
        public async Task<IActionResult> EditCar(int id)
        {
            try
            {
                var car = await _httpClient.GetFromJsonAsync<CarViewModel>($"{_carsApiUrl}({id})");
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
        public async Task<IActionResult> EditCar(int id, CarFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadBrandsToViewBag();
                return View(model);
            }

            try
            {
                AttachJwtToken();
                var payload = new
                {
                    CarId = id,
                    BrandId = model.BrandId,
                    CarName = model.CarName,
                    Model = model.Model,
                    Year = model.Year,
                    Color = model.Color,
                    Mileage = model.Mileage,
                    FuelType = model.FuelType,
                    Transmission = model.Transmission,
                    Price = model.Price,
                    Description = model.Description,
                    ImageUrl = model.ImageUrl,
                    Status = model.Status,
                    CreatedAt = DateTime.Now
                };

                var response = await _httpClient.PutAsync($"{_carsRestApiUrl}({id})",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Cập nhật xe thành công!";
                    return RedirectToAction(nameof(Cars));
                }

                var error = await response.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = "Cập nhật thất bại: " + error;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(model);
        }

        // POST: Admin/DeleteCar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCar(int id)
        {
            try
            {
                AttachJwtToken();
                var response = await _httpClient.DeleteAsync($"{_carsRestApiUrl}({id})");

                if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Xóa xe thành công!";
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
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
                AttachJwtToken();
                var payload = new { CarId = carId, Code = code };
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/odata/DepositCaptchas/generate", payload);

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
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarBrandViewModel>>(_brandsApiUrl);
                ViewBag.Brands = response?.Value ?? new List<CarBrandViewModel>();
            }
            catch
            {
                ViewBag.Brands = new List<CarBrandViewModel>();
            }
        }
    }
}
