using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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
    public class CarsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string CarsApiUrl => $"{_apiBaseUrl}/odata/Cars";
        private string BrandsApiUrl => $"{_apiBaseUrl}/odata/CarBrands";

        public CarsController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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

        public async Task<IActionResult> Index()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(
                    $"{CarsApiUrl}?$filter=Status ne 'Inactive'&$expand=Brand&$orderby=CreatedAt desc");
                return View(response?.Value ?? new List<CarViewModel>());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Khong the tai danh sach xe: " + ex.Message;
                return View(new List<CarViewModel>());
            }
        }

        public async Task<IActionResult> Create()
        {
            await LoadBrandsToViewBag();
            return View(new CarFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CarFormViewModel form)
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
                    TempData["ErrorMessage"] = "Phien dang nhap khong co token. Vui long dang nhap lai.";
                    await LoadBrandsToViewBag();
                    return View(form);
                }

                var payload = BuildCarPayload(form);
                var response = await _httpClient.PostAsync(CarsApiUrl,
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Them xe thanh cong!";
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = "Them xe that bai: " + await ReadApiErrorAsync(response, CarsApiUrl);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Co loi xay ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(form);
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var car = await _httpClient.GetFromJsonAsync<CarViewModel>($"{CarsApiUrl}({id})");
                if (car == null)
                {
                    return NotFound();
                }

                await LoadBrandsToViewBag();
                return View(new CarFormViewModel
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
                });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Khong the tai thong tin xe: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CarFormViewModel form)
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
                    TempData["ErrorMessage"] = "Phien dang nhap khong co token. Vui long dang nhap lai.";
                    await LoadBrandsToViewBag();
                    return View(form);
                }

                var payload = BuildCarPayload(form, id);
                var requestUri = $"{CarsApiUrl}({id})";
                var response = await _httpClient.PutAsync(requestUri,
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Cap nhat xe thanh cong!";
                    return RedirectToAction(nameof(Index));
                }

                TempData["ErrorMessage"] = "Cap nhat that bai: " + await ReadApiErrorAsync(response, requestUri);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Co loi xay ra: " + ex.Message;
            }

            await LoadBrandsToViewBag();
            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (!AttachJwtToken())
                {
                    TempData["ErrorMessage"] = "Phien dang nhap khong co token. Vui long dang nhap lai.";
                    return RedirectToAction(nameof(Index));
                }

                var requestUri = $"{CarsApiUrl}({id})";
                var response = await _httpClient.DeleteAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Xoa xe thanh cong!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Xoa that bai: " + await ReadApiErrorAsync(response, requestUri);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Co loi xay ra: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private static object BuildCarPayload(CarFormViewModel form, int? carId = null)
        {
            return new
            {
                CarId = carId ?? form.CarId,
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
        }

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
