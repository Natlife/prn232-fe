using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers
{
    public class CarsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private string BrandsApiUrl => $"{_apiBaseUrl}/odata/CarBrands";
        private string CarsApiUrl => $"{_apiBaseUrl}/odata/Cars";
        private string PurchaseRequestsApiUrl => $"{_apiBaseUrl}/odata/PurchaseRequests";

        public CarsController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
        }

        // GET: Cars (Showroom)
        public async Task<IActionResult> Index(CarSearchViewModel filter)
        {
            try
            {
                // Fetch Brands for the Left Filter Sidebar
                var brandResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarBrandViewModel>>(BrandsApiUrl);
                var brands = brandResponse?.Value ?? new List<CarBrandViewModel>();
                ViewBag.Brands = brands;

                // Build OData query parameters
                var odataParams = new List<string>();
                var filters = new List<string>
                {
                    "Status ne 'Inactive'"
                };

                if (filter.BrandId.HasValue) 
                    filters.Add($"BrandId eq {filter.BrandId.Value}");
                if (filter.MinPrice.HasValue) 
                    filters.Add($"Price ge {filter.MinPrice.Value}");
                if (filter.MaxPrice.HasValue) 
                    filters.Add($"Price le {filter.MaxPrice.Value}");
                if (!string.IsNullOrEmpty(filter.Transmission)) 
                    filters.Add($"Transmission eq '{filter.Transmission}'");
                if (!string.IsNullOrEmpty(filter.FuelType)) 
                    filters.Add($"FuelType eq '{filter.FuelType}'");
                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var term = Uri.EscapeDataString(filter.SearchTerm.ToLower());
                    filters.Add($"(contains(tolower(CarName), '{term}') or contains(tolower(Model), '{term}'))");
                }

                if (filters.Any())
                {
                    odataParams.Add($"$filter={string.Join(" and ", filters)}");
                }

                // Sorting mapping
                if (!string.IsNullOrEmpty(filter.SortBy))
                {
                    var sortExpr = filter.SortBy.ToLower() switch
                    {
                        "priceasc" => "Price asc",
                        "pricedesc" => "Price desc",
                        "yeardesc" => "Year desc",
                        "mileageasc" => "Mileage asc",
                        _ => "CreatedAt desc"
                    };
                    odataParams.Add($"$orderby={sortExpr}");
                }
                else
                {
                    odataParams.Add("$orderby=CreatedAt desc");
                }

                // Paging & Count & Expand
                var skip = (filter.PageNumber - 1) * filter.PageSize;
                odataParams.Add($"$skip={skip}");
                odataParams.Add($"$top={filter.PageSize}");
                odataParams.Add("$count=true");
                odataParams.Add("$expand=Brand");

                var requestUri = CarsApiUrl;
                if (odataParams.Any())
                {
                    requestUri += "?" + string.Join("&", odataParams);
                }

                // Fetch from OData API
                var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(requestUri);

                var pagedCars = new PagedResultViewModel<CarViewModel>
                {
                    Items = odataResponse?.Value ?? new List<CarViewModel>(),
                    TotalItems = odataResponse?.Count ?? 0,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling((double)(odataResponse?.Count ?? 0) / filter.PageSize)
                };

                ViewBag.Filter = filter;
                return View(pagedCars);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải danh sách xe: " + ex.Message;
                ViewBag.Brands = new List<CarBrandViewModel>();
                return View(new PagedResultViewModel<CarViewModel>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var requestUri = $"{CarsApiUrl}({id})?$expand=Brand";
                var car = await _httpClient.GetFromJsonAsync<CarViewModel>(requestUri);
                if (car == null)
                {
                    return NotFound();
                }
                return View(car);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải thông tin chi tiết xe: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
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

        // GET: Cars/History
        [HttpGet]
        public async Task<IActionResult> History()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                AttachJwtToken();
                var requestUri = $"{_apiBaseUrl}/api/car-sales/requests";
                var response = await _httpClient.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var historyList = System.Text.Json.JsonSerializer.Deserialize<List<PurchaseRequestHistoryViewModel>>(content, options) ?? new List<PurchaseRequestHistoryViewModel>();
                    return View(historyList);
                }
                return View(new List<PurchaseRequestHistoryViewModel>());
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải lịch sử: " + ex.Message;
                return View(new List<PurchaseRequestHistoryViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitPurchaseRequest(string endpoint, [FromBody] System.Text.Json.JsonElement payload)
        {
            try
            {
                AttachJwtToken();
                var requestUri = $"{_apiBaseUrl}/api/car-sales/requests";
                
                int carId = payload.TryGetProperty("carId", out var carProp) ? carProp.GetInt32() : 0;
                string name = User.Identity?.Name ?? "Khách hàng";
                string phone = payload.TryGetProperty("customerPhone", out var phoneProp) ? phoneProp.GetString() ?? "0900000000" : "0900000000";
                string email = payload.TryGetProperty("customerEmail", out var emailProp) ? emailProp.GetString() ?? "" : "";
                string message = payload.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? endpoint : endpoint;

                var dto = new
                {
                    CarId = carId,
                    CustomerName = name,
                    CustomerPhone = phone,
                    CustomerEmail = email,
                    Message = message
                };

                var response = await _httpClient.PostAsJsonAsync(requestUri, dto);
                var content = await response.Content.ReadAsStringAsync();
                
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DetectImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn một file ảnh hợp lệ." });
            }

            try
            {
                AttachJwtToken();
                using var form = new MultipartFormDataContent();
                using var fileStream = file.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                form.Add(streamContent, "file", file.FileName);

                var detectApiUrl = $"{_apiBaseUrl}/api/CarDetection/detect";
                var response = await _httpClient.PostAsync(detectApiUrl, form);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                    return Json(new { success = true, data = result });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi nhận diện ảnh từ API: " + content });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xử lý nhận diện ảnh: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SyncVectorDb()
        {
            try
            {
                AttachJwtToken();
                var syncApiUrl = $"{_apiBaseUrl}/api/CarDetection/sync";
                var response = await _httpClient.PostAsync(syncApiUrl, null);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Đồng bộ Vector DB thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Lỗi đồng bộ từ API: " + content });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối khi đồng bộ: " + ex.Message });
            }
        }
    }
}
