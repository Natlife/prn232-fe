using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authorization;

namespace CarSalesManagementSystemClient.Controllers
{
    public class PartsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _partsApiUrl = "http://localhost:5084/api/Parts";
        private readonly string _categoriesApiUrl = "http://localhost:5084/api/PartCategories";

        public PartsController(IHttpClientFactory httpClientFactory)
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

        // GET: Parts (Showroom/Shopping catalog)
        public async Task<IActionResult> Index(PartSearchViewModel filter)
        {
            try
            {
                // Fetch Categories for filters
                var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(_categoriesApiUrl);
                ViewBag.Categories = categories ?? new List<PartCategoryViewModel>();

                // Build OData parameters
                var odataParams = new List<string>();
                var filters = new List<string>();

                // Exclude Inactive parts for public catalog
                filters.Add("Status ne 'Inactive'");

                if (filter.CategoryId.HasValue)
                {
                    filters.Add($"CategoryId eq {filter.CategoryId.Value}");
                }
                if (filter.MinPrice.HasValue)
                {
                    filters.Add($"Price ge {filter.MinPrice.Value}");
                }
                if (filter.MaxPrice.HasValue)
                {
                    filters.Add($"Price le {filter.MaxPrice.Value}");
                }
                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var term = Uri.EscapeDataString(filter.SearchTerm.ToLower());
                    filters.Add($"(contains(tolower(PartName), '{term}') or contains(tolower(PartCode), '{term}') or contains(tolower(Brand), '{term}'))");
                }

                odataParams.Add($"$filter={string.Join(" and ", filters)}");

                if (!string.IsNullOrEmpty(filter.SortBy))
                {
                    var sortExpr = filter.SortBy.ToLower() switch
                    {
                        "priceasc" => "Price asc",
                        "pricedesc" => "Price desc",
                        "nameasc" => "PartName asc",
                        _ => "CreatedAt desc"
                    };
                    odataParams.Add($"$orderby={sortExpr}");
                }
                else
                {
                    odataParams.Add("$orderby=CreatedAt desc");
                }

                var skip = (filter.PageNumber - 1) * filter.PageSize;
                odataParams.Add($"$skip={skip}");
                odataParams.Add($"$top={filter.PageSize}");
                odataParams.Add("$count=true");
                odataParams.Add("$expand=Category");

                var requestUri = "http://localhost:5084/odata/Parts";
                if (odataParams.Any())
                {
                    requestUri += "?" + string.Join("&", odataParams);
                }

                var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(requestUri);

                var pagedParts = new PagedResultViewModel<PartViewModel>
                {
                    Items = odataResponse?.Value ?? new List<PartViewModel>(),
                    TotalItems = odataResponse?.Count ?? 0,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling((double)(odataResponse?.Count ?? 0) / filter.PageSize)
                };

                ViewBag.Filter = filter;
                return View(pagedParts);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải danh sách phụ tùng: " + ex.Message;
                ViewBag.Categories = new List<PartCategoryViewModel>();
                return View(new PagedResultViewModel<PartViewModel>());
            }
        }

        // GET: Parts/Manage (Admin CRUD view)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            try
            {
                // Fetch Categories for dropdown
                var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(_categoriesApiUrl);
                ViewBag.Categories = categories ?? new List<PartCategoryViewModel>();

                // Get all parts for Admin list (OData expandable)
                var requestUri = "http://localhost:5084/odata/Parts?$expand=Category&$orderby=CreatedAt desc";
                var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(requestUri);
                
                return View(odataResponse?.Value ?? new List<PartViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi tải trang quản trị phụ tùng: " + ex.Message;
                ViewBag.Categories = new List<PartCategoryViewModel>();
                return View(new List<PartViewModel>());
            }
        }

        // GET: Parts/GetPartJson/5 (AJAX API helper)
        [HttpGet]
        public async Task<IActionResult> GetPartJson(int id)
        {
            try
            {
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{id}");
                if (part == null) return NotFound();
                return Json(part);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: Parts/Save (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Save(PartViewModel part)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join("<br/>", errors) });
            }

            try
            {
                AppendAuthorizationHeader();
                HttpResponseMessage response;

                if (part.PartId == 0) // Create new
                {
                    response = await _httpClient.PostAsJsonAsync(_partsApiUrl, part);
                }
                else // Update existing
                {
                    response = await _httpClient.PutAsJsonAsync($"{_partsApiUrl}/{part.PartId}", part);
                }

                if (response.IsSuccessStatusCode)
                {
                    string msg = part.PartId == 0 ? "Thêm phụ tùng mới thành công!" : "Cập nhật phụ tùng thành công!";
                    return Json(new { success = true, message = msg });
                }
                
                string errMsg = await ExtractErrorMessageAsync(response, "Đã xảy ra lỗi trên Server.");
                return Json(new { success = false, message = errMsg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }

        private async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, string defaultMessage)
        {
            try
            {
                var errContent = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (errContent.TryGetProperty("message", out var msgProp))
                {
                    return msgProp.GetString()!;
                }
                if (errContent.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errorsList = new List<string>();
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        foreach (var err in prop.Value.EnumerateArray())
                        {
                            errorsList.Add(err.GetString()!);
                        }
                    }
                    if (errorsList.Any())
                    {
                        return string.Join("<br/>", errorsList);
                    }
                }
            }
            catch
            {
                try
                {
                    var rawStr = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(rawStr) && rawStr.Length < 200)
                    {
                        return rawStr;
                    }
                }
                catch { }
            }
            return defaultMessage;
        }

        // POST: Parts/Delete/5 (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.DeleteAsync($"{_partsApiUrl}/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Xóa phụ tùng thành công!" });
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Không thể xóa phụ tùng.");
                return Json(new { success = false, message = errMsg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }
    }
}
