using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
        public async Task<IActionResult> Index(MaintenancePackageSearchViewModel filter)
        {
            var viewModel = new MaintenanceIndexViewModel 
            { 
                CurrentPage = filter.PageNumber,
                Filter = filter 
            };

            try
            {
                var requestUri = $"{_apiUrl}/maintenancepackages/available";
                var apiResponse = await _httpClient.GetFromJsonAsync<ApiResponse<List<MaintenancePackageViewModel>>>(requestUri);

                var allPackages = apiResponse?.Data ?? new List<MaintenancePackageViewModel>();

                // Apply filters
                if (filter.MinPrice.HasValue)
                    allPackages = allPackages.Where(p => p.PackagePrice >= filter.MinPrice.Value).ToList();
                if (filter.MaxPrice.HasValue)
                    allPackages = allPackages.Where(p => p.PackagePrice <= filter.MaxPrice.Value).ToList();
                if (filter.MaxDuration.HasValue)
                    allPackages = allPackages.Where(p => p.TotalDurationMinutes <= filter.MaxDuration.Value).ToList();
                if (!string.IsNullOrEmpty(filter.SearchTerm))
                    allPackages = allPackages.Where(p => p.PackageName?.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) == true).ToList();

                // Apply sorting
                if (!string.IsNullOrEmpty(filter.SortBy))
                {
                    allPackages = filter.SortBy.ToLower() switch
                    {
                        "priceasc" => allPackages.OrderBy(p => p.PackagePrice).ToList(),
                        "pricedesc" => allPackages.OrderByDescending(p => p.PackagePrice).ToList(),
                        "durationasc" => allPackages.OrderBy(p => p.TotalDurationMinutes).ToList(),
                        _ => allPackages
                    };
                }

                int totalItems = allPackages.Count;
                var skip = (filter.PageNumber - 1) * filter.PageSize;
                viewModel.Packages = allPackages.Skip(skip).Take(filter.PageSize).ToList();
                int totalItemsForPagination = totalItems;
                viewModel.TotalPages = (int)Math.Ceiling((double)totalItemsForPagination / filter.PageSize);
                if (viewModel.TotalPages == 0) viewModel.TotalPages = 1;
                if (viewModel.CurrentPage > viewModel.TotalPages) viewModel.CurrentPage = viewModel.TotalPages;
                if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;
            }
            catch
            {
                viewModel.Packages = new List<MaintenancePackageViewModel>();
            }

            return View(viewModel);
        }

        // GET: /Maintenance/History
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> History(int pageNumber = 1)
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
                        int pageSize = 10;
                        int totalItems = allAppointments.Count;
                        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                        if (totalPages == 0) totalPages = 1;
                        if (pageNumber > totalPages) pageNumber = totalPages;
                        if (pageNumber < 1) pageNumber = 1;

                        var pagedModel = new PagedResultViewModel<AppointmentHistoryViewModel>
                        {
                            Items = allAppointments.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                            PageNumber = pageNumber,
                            PageSize = pageSize,
                            TotalItems = totalItems,
                            TotalPages = totalPages
                        };
                        return View(pagedModel);
                    }
                }
            }
            return View(new PagedResultViewModel<AppointmentHistoryViewModel> { Items = new List<AppointmentHistoryViewModel>(), TotalPages = 1, PageNumber = 1 });
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
            if (package.Status != "Available")
            {
                TempData["Error"] = "Gói bảo dưỡng này hiện đã ngừng cung cấp. Vui lòng chọn gói khác.";
                return RedirectToAction("Index");
            }

            ViewBag.PackageName = package.PackageName;

            var model = new BookingViewModel 
            { 
                PackageIds = new List<int> { package.PackageId },
                CustomerName = User.Identity?.Name ?? "",
                CustomerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
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
            
            var payload = new
            {
                CustomerCarId = model.CustomerCarId,
                CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? (User.Identity?.Name ?? "Khách hàng") : model.CustomerName,
                CustomerPhone = model.CustomerPhone,
                CustomerEmail = model.CustomerEmail ?? User.FindFirst(ClaimTypes.Email)?.Value,
                AppointmentDate = model.AppointmentDate,
                AppointmentTime = model.AppointmentTime,
                Note = model.Note,
                PackageIds = model.PackageIds,
                ServiceIds = model.ServiceIds,
                CarName = model.CarName,
                LicensePlate = model.LicensePlate
            };

            AppendAuthorizationHeader();
            var response = await _httpClient.PostAsync($"{_apiUrl}/MaintenanceAppointments/{customerId}",
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

        // GET: /Maintenance/Cancel/1
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            AppendAuthorizationHeader();
            var reqObj = new { Status = "Cancelled", Reason = "Khách hàng tự hủy" };
            var response = await _httpClient.PutAsync($"{_apiUrl}/MaintenanceAppointments/{id}/status",
                new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Bạn đã hủy lịch hẹn thành công!";
            }
            else
            {
                TempData["Error"] = "Đã xảy ra lỗi khi hủy lịch hẹn.";
            }

            return RedirectToAction("History");
        }

        // GET: /Maintenance/Services
        public async Task<IActionResult> Services(string? searchTerm, decimal? minPrice, decimal? maxPrice, int pageNumber = 1)
        {
            int pageSize = 12;
            var viewModel = new ServicesIndexViewModel
            {
                SearchTerm = searchTerm,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            try
            {
                var response = await _httpClient.GetAsync($"{_apiUrl}/Services/available");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<ApiResponse<List<ServiceSummaryViewModel>>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (apiResult?.Success == true && apiResult.Data != null)
                    {
                        var allServices = apiResult.Data;

                        // Filter
                        if (!string.IsNullOrEmpty(searchTerm))
                            allServices = allServices.Where(s => s.ServiceName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (minPrice.HasValue)
                            allServices = allServices.Where(s => s.BasePrice >= minPrice.Value).ToList();
                        if (maxPrice.HasValue)
                            allServices = allServices.Where(s => s.BasePrice <= maxPrice.Value).ToList();

                        int totalItems = allServices.Count;
                        viewModel.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                        if (viewModel.TotalPages == 0) viewModel.TotalPages = 1;
                        if (pageNumber > viewModel.TotalPages) pageNumber = viewModel.TotalPages;
                        if (pageNumber < 1) pageNumber = 1;
                        viewModel.PageNumber = pageNumber;

                        viewModel.Services = allServices.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                    }
                }
            }
            catch
            {
                viewModel.Services = new List<ServiceSummaryViewModel>();
            }

            return View(viewModel);
        }

        // GET: /Maintenance/BookingService/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> BookingService(int id)
        {
            var response = await _httpClient.GetAsync($"{_apiUrl}/Services/{id}");
            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Không tìm thấy dịch vụ.";
                return RedirectToAction("Services");
            }

            var content = await response.Content.ReadAsStringAsync();
            var apiResult = JsonSerializer.Deserialize<ApiResponse<ServiceSummaryViewModel>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResult == null || !apiResult.Success || apiResult.Data == null)
            {
                TempData["Error"] = "Không tìm thấy dịch vụ.";
                return RedirectToAction("Services");
            }

            var service = apiResult.Data;
            ViewBag.ServiceName = service.ServiceName;
            ViewBag.ServicePrice = service.BasePrice;
            ViewBag.ServiceDuration = service.EstimatedDurationMinutes;

            var model = new ServiceBookingViewModel
            {
                ServiceId = service.ServiceId,
                ServiceName = service.ServiceName,
                ServiceIds = new List<int> { service.ServiceId },
                PackageIds = new List<int>(),
                CustomerName = User.Identity?.Name ?? "",
                CustomerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
                AppointmentDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
                AppointmentTime = new TimeOnly(9, 0)
            };
            return View(model);
        }

        // POST: /Maintenance/BookingService
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BookingService(ServiceBookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.ServiceName = model.ServiceName;
                return View(model);
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int customerId))
            {
                ViewBag.Error = "Lỗi xác thực. Vui lòng đăng nhập lại.";
                return View(model);
            }

            var payload = new
            {
                CustomerCarId = model.CustomerCarId,
                CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? (User.Identity?.Name ?? "Khách hàng") : model.CustomerName,
                CustomerPhone = model.CustomerPhone,
                CustomerEmail = model.CustomerEmail ?? User.FindFirst(ClaimTypes.Email)?.Value,
                AppointmentDate = model.AppointmentDate,
                AppointmentTime = model.AppointmentTime,
                Note = model.Note,
                PackageIds = new List<int>(),
                ServiceIds = new List<int> { model.ServiceId },
                CarName = model.CarName,
                LicensePlate = model.LicensePlate
            };

            AppendAuthorizationHeader();
            var response = await _httpClient.PostAsync($"{_apiUrl}/MaintenanceAppointments/{customerId}",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = $"Đặt lịch dịch vụ '{model.ServiceName}' thành công! Chúng tôi sẽ liên hệ lại với bạn sớm nhất.";
                return RedirectToAction("History");
            }

            var errorDetail = await response.Content.ReadAsStringAsync();
            try
            {
                var apiError = JsonSerializer.Deserialize<ApiResponse<object>>(errorDetail, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                ViewBag.Error = $"Lỗi từ hệ thống (API): {apiError?.Message ?? errorDetail}";
            }
            catch
            {
                ViewBag.Error = $"Lỗi từ hệ thống (API): {response.StatusCode} - {errorDetail}";
            }

            ViewBag.ServiceName = model.ServiceName;
            return View(model);
        }
    }
}
