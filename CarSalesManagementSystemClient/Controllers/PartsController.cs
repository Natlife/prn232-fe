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

        // GET: Parts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                // Fetch the single part with category expanded
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>(
                    $"http://localhost:5084/odata/Parts({id})?$expand=Category");

                if (part == null)
                    return NotFound();

                // Fetch related parts from same category (exclude current, limit 4)
                var relatedUri = $"http://localhost:5084/odata/Parts?$expand=Category&$filter=CategoryId eq {part.CategoryId} and PartId ne {id} and Status ne 'Inactive'&$top=4&$orderby=CreatedAt desc";
                var relatedResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(relatedUri);
                ViewBag.RelatedParts = relatedResponse?.Value ?? new List<PartViewModel>();

                return View(part);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải thông tin phụ tùng: " + ex.Message;
                return RedirectToAction("Index");
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

                // Fetch Suppliers for tab 3 & dropdowns
                try
                {
                    var suppliers = await _httpClient.GetFromJsonAsync<IEnumerable<SupplierViewModel>>("http://localhost:5084/api/Suppliers");
                    ViewBag.Suppliers = suppliers ?? new List<SupplierViewModel>();
                }
                catch
                {
                    ViewBag.Suppliers = new List<SupplierViewModel>();
                }

                // Get all parts for Admin list (OData expandable)
                var requestUri = "http://localhost:5084/odata/Parts?$expand=Category&$orderby=CreatedAt desc";
                var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(requestUri);
                
                return View(odataResponse?.Value ?? new List<PartViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi tải trang quản trị phụ tùng: " + ex.Message;
                ViewBag.Categories = new List<PartCategoryViewModel>();
                ViewBag.Suppliers = new List<SupplierViewModel>();
                return View(new List<PartViewModel>());
            }
        }

        // POST: Parts/UploadImage (Single image file upload from laptop)
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Vui lòng chọn một tập tin ảnh hợp lệ." });

            try
            {
                var uploadsFolder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "parts");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(file.FileName);
                var filePath = System.IO.Path.Combine(uploadsFolder, fileName);

                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var imageUrl = "/uploads/parts/" + fileName;
                return Json(new { success = true, imageUrl = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải ảnh lên: " + ex.Message });
            }
        }

        // POST: Parts/UploadMultipleImages (Multiple image files upload from laptop)
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> UploadMultipleImages(List<IFormFile> files)
        {
            if (files == null || !files.Any())
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 tập tin ảnh." });

            try
            {
                var uploadsFolder = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "parts");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var imageUrls = new List<string>();

                foreach (var file in files)
                {
                    if (file != null && file.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(file.FileName);
                        var filePath = System.IO.Path.Combine(uploadsFolder, fileName);

                        using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        imageUrls.Add("/uploads/parts/" + fileName);
                    }
                }

                return Json(new { success = true, imageUrls = imageUrls, joinedUrl = string.Join(",", imageUrls) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải các tập tin ảnh lên: " + ex.Message });
            }
        }

        // GET: Parts/GetPartJson/5 (AJAX API helper)
        [HttpGet]
        public async Task<IActionResult> GetPartJson(int id)
        {
            try
            {
                var updateModel = await _httpClient.GetFromJsonAsync<UpdatePartViewModel>($"{_partsApiUrl}/{id}/details-for-edit");
                if (updateModel != null)
                {
                    return Json(updateModel);
                }

                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{id}");
                if (part == null) return NotFound();

                return Json(new UpdatePartViewModel
                {
                    PartId = part.PartId,
                    PartName = part.PartName,
                    PartCode = part.PartCode,
                    CategoryId = part.CategoryId,
                    Brand = part.Brand,
                    Price = part.Price,
                    MinStockLevel = part.MinStockLevel,
                    MaxStockLevel = part.MaxStockLevel,
                    UnitOfMeasure = string.IsNullOrWhiteSpace(part.UnitOfMeasure) ? "Cái" : part.UnitOfMeasure,
                    WarehouseLocation = part.WarehouseLocation,
                    WarrantyMonths = part.WarrantyMonths,
                    Description = part.Description,
                    ImageUrl = part.ImageUrl,
                    Status = part.Status,
                    CurrentQuantity = part.Quantity,
                    CurrentExpiredAt = part.ExpiredAt,
                    CanEditPartCode = true
                });
            }
            catch (Exception)
            {
                try
                {
                    var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{id}");
                    if (part == null) return NotFound();
                    return Json(new UpdatePartViewModel
                    {
                        PartId = part.PartId,
                        PartName = part.PartName,
                        PartCode = part.PartCode,
                        CategoryId = part.CategoryId,
                        Brand = part.Brand,
                        Price = part.Price,
                        MinStockLevel = part.MinStockLevel,
                        MaxStockLevel = part.MaxStockLevel,
                        UnitOfMeasure = string.IsNullOrWhiteSpace(part.UnitOfMeasure) ? "Cái" : part.UnitOfMeasure,
                        WarehouseLocation = part.WarehouseLocation,
                        WarrantyMonths = part.WarrantyMonths,
                        Description = part.Description,
                        ImageUrl = part.ImageUrl,
                        Status = part.Status,
                        CurrentQuantity = part.Quantity,
                        CurrentExpiredAt = part.ExpiredAt,
                        CanEditPartCode = true
                    });
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }
        }

        // POST: Parts/Save (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Save(UpdatePartViewModel model)
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

                if (model.PartId == 0) // Create new
                {
                    var newPart = new PartViewModel
                    {
                        PartName = model.PartName,
                        PartCode = model.PartCode,
                        CategoryId = model.CategoryId,
                        Brand = model.Brand,
                        Price = model.Price,
                        MinStockLevel = model.MinStockLevel,
                        MaxStockLevel = model.MaxStockLevel,
                        UnitOfMeasure = model.UnitOfMeasure,
                        WarehouseLocation = model.WarehouseLocation,
                        WarrantyMonths = model.WarrantyMonths,
                        Description = model.Description,
                        ImageUrl = model.ImageUrl,
                        Status = model.CurrentQuantity > 0 ? "Available" : "OutOfStock",
                        Quantity = model.CurrentQuantity
                    };
                    response = await _httpClient.PostAsJsonAsync(_partsApiUrl, newPart);
                }
                else // Update existing metadata
                {
                    response = await _httpClient.PutAsJsonAsync($"{_partsApiUrl}/{model.PartId}", model);
                }

                if (response.IsSuccessStatusCode)
                {
                    string msg = model.PartId == 0 ? "Thêm phụ tùng mới thành công!" : "Cập nhật thông tin phụ tùng thành công!";
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

        // POST: Parts/AdjustInventory (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> AdjustInventory(InventoryAdjustmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join("<br/>", errors) });
            }

            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/Inventory/adjust", model);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Điều chỉnh tồn kho thành công!" });
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Đã xảy ra lỗi khi điều chỉnh tồn kho.");
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
                // Check if the part is in any customer's cart
                if (ActiveCartRegistry.IsPartInAnyCart(id))
                {
                    return Json(new { success = false, message = "Không thể xóa phụ tùng này vì sản phẩm đang nằm trong giỏ hàng của khách hàng!" });
                }

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

        // POST: Parts/Discontinue/5 (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Discontinue(int id)
        {
            try
            {
                // Check if the part is in any customer's cart
                if (ActiveCartRegistry.IsPartInAnyCart(id))
                {
                    return Json(new { success = false, message = "Không thể ngưng bán phụ tùng này vì sản phẩm đang nằm trong giỏ hàng của khách hàng!" });
                }

                // Fetch existing details
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{id}");
                if (part == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phụ tùng cần cập nhật." });
                }

                // Set status to Inactive (Ngưng bán)
                part.Status = "Inactive";

                AppendAuthorizationHeader();
                var response = await _httpClient.PutAsJsonAsync($"{_partsApiUrl}/{id}", part);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Đã ngưng bán phụ tùng thành công!" });
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Không thể ngưng bán phụ tùng.");
                return Json(new { success = false, message = errMsg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/Resell/5 (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Resell(int id, int quantity)
        {
            try
            {
                if (quantity <= 0)
                {
                    return Json(new { success = false, message = "Số lượng bán lại phải lớn hơn 0." });
                }

                // Fetch existing details
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{id}");
                if (part == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phụ tùng cần cập nhật." });
                }

                // Update quantity and set status to Available (đang bán)
                part.Quantity = quantity;
                part.Status = "Available";

                AppendAuthorizationHeader();
                var response = await _httpClient.PutAsJsonAsync($"{_partsApiUrl}/{id}", part);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Bán lại phụ tùng thành công và chuyển trạng thái sang đang bán!" });
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Không thể bán lại phụ tùng.");
                return Json(new { success = false, message = errMsg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/CheckCompatibility (AJAX POST)
        [HttpPost]
        public async Task<IActionResult> CheckCompatibility(string licensePlate, string partCode)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/Parts/check-compatibility", new { licensePlate, partCode });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(result);
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Kiểm tra tương thích thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/CreateInventoryReceipt (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> CreateInventoryReceipt([FromBody] InventoryReceiptCreateViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { message = string.Join("<br/>", errors) });
                }

                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/inventory/receipt", model);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(result);
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Tạo phiếu nhập kho thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // GET: Parts/GetFilteredParts
        [HttpGet]
        public async Task<IActionResult> GetFilteredParts(int categoryId, int supplierId)
        {
            try
            {
                string url = categoryId > 0 
                    ? $"http://localhost:5084/api/Parts/filter?categoryId={categoryId}&supplierId={supplierId}"
                    : "http://localhost:5084/api/Parts";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(result);
                }
                return BadRequest(new { message = "Lỗi tải danh sách phụ tùng." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/CreateCategory (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory([FromBody] object payload)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/PartCategories", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(new { success = true, data = result });
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Thêm danh mục thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/CreateSupplier (AJAX POST)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateSupplier([FromBody] object payload)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/Suppliers", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(new { success = true, data = result });
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Thêm nhà cung cấp thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/UpdateCategory/5 (AJAX POST wrapper around PUT API)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] object payload)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PutAsJsonAsync($"http://localhost:5084/api/PartCategories/{id}", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(new { success = true, data = result });
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Cập nhật danh mục thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // POST: Parts/UpdateSupplier/5 (AJAX POST wrapper around PUT API)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSupplier(int id, [FromBody] object payload)
        {
            try
            {
                AppendAuthorizationHeader();
                var response = await _httpClient.PutAsJsonAsync($"http://localhost:5084/api/Suppliers/{id}", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Json(new { success = true, data = result });
                }
                var errMsg = await ExtractErrorMessageAsync(response, "Cập nhật nhà cung cấp thất bại.");
                return BadRequest(new { message = errMsg });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối: " + ex.Message });
            }
        }

        // GET: Parts/History/5 (Admin view history)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> History(int id)
        {
            try
            {
                // Fetch Part Details
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"http://localhost:5084/api/Parts/{id}");
                if (part == null)
                {
                    return NotFound();
                }

                // Fetch Categories
                try
                {
                    var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(_categoriesApiUrl);
                    if (categories != null && part.CategoryId > 0)
                    {
                        part.Category = categories.FirstOrDefault(c => c.CategoryId == part.CategoryId);
                    }
                }
                catch { }

                // Fetch Transactions
                AppendAuthorizationHeader();
                var transactionsResponse = await _httpClient.GetAsync($"http://localhost:5084/api/Inventory/transactions/{id}");
                var transactions = new List<InventoryTransactionViewModel>();
                if (transactionsResponse.IsSuccessStatusCode)
                {
                    transactions = await transactionsResponse.Content.ReadFromJsonAsync<List<InventoryTransactionViewModel>>() 
                                   ?? new List<InventoryTransactionViewModel>();
                }

                ViewBag.Transactions = transactions;
                return View(part);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Lỗi khi tải lịch sử phụ tùng: " + ex.Message;
                return View(new PartViewModel());
            }
        }
    }
}
