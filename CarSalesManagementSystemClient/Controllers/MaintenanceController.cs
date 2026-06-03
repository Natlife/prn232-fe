using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarSalesManagementSystemClient.Models;

namespace CarSalesManagementSystemClient.Controllers
{
    public class MaintenanceController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api"; // Same as AuthController

        public MaintenanceController(IHttpClientFactory httpClientFactory)
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

        // Helper classes to deserialize API wrapper
        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public T Data { get; set; }
        }

        // GET: /Maintenance/
        public async Task<IActionResult> Index(int page = 1)
        {
            var viewModel = new MaintenanceIndexViewModel { CurrentPage = page };

            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages/available");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResult = JsonSerializer.Deserialize<ApiResponse<List<MaintenancePackageViewModel>>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (apiResult != null && apiResult.Success)
                {
                    viewModel.Packages = apiResult.Data ?? new List<MaintenancePackageViewModel>();
                }
            }

            if (User.Identity.IsAuthenticated)
            {
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int customerId))
                {
                    AppendAuthorizationHeader();
                    var historyResponse = await _httpClient.GetAsync($"{_apiUrl}/MaintenanceAppointments/customer/{customerId}");
                    if (historyResponse.IsSuccessStatusCode)
                    {
                        var historyContent = await historyResponse.Content.ReadAsStringAsync();
                        var apiResult = JsonSerializer.Deserialize<ApiResponse<List<AppointmentHistoryViewModel>>>(historyContent, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            
                        if (apiResult != null && apiResult.Success && apiResult.Data != null)
                        {
                            var allAppointments = apiResult.Data.OrderByDescending(x => x.CreatedAt).ToList();

                            int pageSize = 5;
                            viewModel.TotalPages = (int)Math.Ceiling(allAppointments.Count / (double)pageSize);
                            if (viewModel.TotalPages == 0) viewModel.TotalPages = 1;
                            if (viewModel.CurrentPage > viewModel.TotalPages) viewModel.CurrentPage = viewModel.TotalPages;
                            if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;

                            viewModel.Appointments = allAppointments
                                .Skip((viewModel.CurrentPage - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();
                        }
                    }
                }
            }

            return View(viewModel);
        }

        // GET: /Maintenance/Booking/1
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Booking(int id)
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/MaintenancePackages/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse<MaintenancePackageViewModel>>(content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResult == null || !apiResult.Success || apiResult.Data == null)
            {
                return RedirectToAction("Index");
            }

            var package = apiResult.Data;
            var model = new BookingViewModel 
            { 
                PackageId = package.PackageId,
                PackageName = package.PackageName,
                AppointmentDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
                AppointmentTime = new TimeOnly(9, 0)
            };
            return View(model);
        }

        // POST: /Maintenance/Booking
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Booking(BookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int customerId))
            {
                ViewBag.Error = "Lỗi xác thực. Vui lòng đăng nhập lại.";
                return View(model);
            }

            var customerName = User.Identity?.Name ?? "Khách hàng";
            var customerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
            
            var payload = new
            {
                CustomerId = customerId,
                PackageId = model.PackageId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                CustomerPhone = model.CustomerPhone,
                CarName = model.CarName,
                LicensePlate = model.LicensePlate,
                AppointmentDate = model.AppointmentDate,
                AppointmentTime = model.AppointmentTime,
                Note = model.Note ?? "",
                Status = "Pending"
            };

            AppendAuthorizationHeader();
            var response = await _httpClient.PostAsync($"{_apiUrl}/MaintenanceAppointments",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Đặt lịch thành công! Chúng tôi sẽ liên hệ lại với bạn sớm nhất.";
                return RedirectToAction("Index");
            }

            var errorDetail = await response.Content.ReadAsStringAsync();
            try {
                var apiError = JsonSerializer.Deserialize<ApiResponse<object>>(errorDetail, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ViewBag.Error = $"Lỗi từ hệ thống (API): {apiError?.Message ?? errorDetail}";
            } catch {
                ViewBag.Error = $"Lỗi từ hệ thống (API): {response.StatusCode} - {errorDetail}";
            }
            
            return View(model);
        }

        // POST: /Maintenance/Cancel/1
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            AppendAuthorizationHeader();
            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/status",
                new StringContent(JsonSerializer.Serialize("Cancelled"), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Hủy lịch hẹn thành công.";
            }
            else
            {
                TempData["Error"] = "Đã xảy ra lỗi khi hủy lịch hẹn.";
            }

            return RedirectToAction("Index");
        }
    }
}
