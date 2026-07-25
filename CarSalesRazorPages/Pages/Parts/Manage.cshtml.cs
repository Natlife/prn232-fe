using System.Net.Http.Json;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Parts;

[Authorize(Roles = "Admin")]
public class ManageModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string PartsApiUrl = "http://localhost:5084/api/Parts";
    private const string CategoriesApiUrl = "http://localhost:5084/api/PartCategories";

    public ManageModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public List<PartViewModel> Parts { get; set; } = new();
    public IEnumerable<PartCategoryViewModel> Categories { get; set; } = new List<PartCategoryViewModel>();
    public IEnumerable<SupplierViewModel> Suppliers { get; set; } = new List<SupplierViewModel>();
    public string? ErrorMessage { get; set; }

    private void AppendAuthorizationHeader()
    {
        var token = User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task OnGetAsync()
    {
        try
        {
            var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(CategoriesApiUrl);
            Categories = categories ?? new List<PartCategoryViewModel>();

            try
            {
                var suppliers = await _httpClient.GetFromJsonAsync<IEnumerable<SupplierViewModel>>("http://localhost:5084/api/Suppliers");
                Suppliers = suppliers ?? new List<SupplierViewModel>();
            }
            catch
            {
                Suppliers = new List<SupplierViewModel>();
            }

            var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>($"{PartsApiUrl}?$expand=Category&$orderby=CreatedAt desc");
            Parts = odataResponse?.Value ?? new List<PartViewModel>();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Lỗi khi tải trang quản trị phụ tùng: " + ex.Message;
        }
    }

    public async Task<IActionResult> OnPostUploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return new JsonResult(new { success = false, message = "Vui lòng chọn tập tin ảnh." });

        try
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "parts");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return new JsonResult(new { success = true, imageUrl = "/uploads/parts/" + fileName });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = "Lỗi khi tải ảnh: " + ex.Message });
        }
    }

    public async Task<IActionResult> OnPostUploadMultipleImagesAsync(List<IFormFile> files)
    {
        if (files == null || !files.Any())
            return new JsonResult(new { success = false, message = "Vui lòng chọn tập tin ảnh." });

        try
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "parts");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var imageUrls = new List<string>();
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    imageUrls.Add("/uploads/parts/" + fileName);
                }
            }
            return new JsonResult(new { success = true, imageUrls = imageUrls });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = "Lỗi khi tải các tập tin ảnh: " + ex.Message });
        }
    }

    public async Task<IActionResult> OnGetGetPartJsonAsync(int id)
    {
        try
        {
            var updateModel = await _httpClient.GetFromJsonAsync<UpdatePartViewModel>($"{PartsApiUrl}/{id}/details-for-edit");
            if (updateModel != null)
            {
                return new JsonResult(updateModel);
            }

            var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{id}");
            if (part == null) return NotFound();

            return new JsonResult(new UpdatePartViewModel
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
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{id}");
                if (part == null) return NotFound();
                return new JsonResult(new UpdatePartViewModel
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

    public async Task<IActionResult> OnPostSaveAsync([FromBody] UpdatePartViewModel model)
    {
        AppendAuthorizationHeader();
        try
        {
            if (model.PartId == 0)
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
                var response = await _httpClient.PostAsJsonAsync(PartsApiUrl, newPart);
                if (response.IsSuccessStatusCode)
                    return new JsonResult(new { success = true, message = "Thêm phụ tùng mới thành công!" });
                var err = await ExtractErrorMessageAsync(response, "Đã xảy ra lỗi trên Server.");
                return new JsonResult(new { success = false, message = err });
            }
            else
            {
                var response = await _httpClient.PutAsJsonAsync($"{PartsApiUrl}/{model.PartId}", model);
                if (response.IsSuccessStatusCode)
                    return new JsonResult(new { success = true, message = "Cập nhật thông tin phụ tùng thành công!" });
                var err = await ExtractErrorMessageAsync(response, "Đã xảy ra lỗi trên Server.");
                return new JsonResult(new { success = false, message = err });
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message });
        }
    }

    public async Task<IActionResult> OnPostAdjustInventoryAsync([FromBody] InventoryAdjustmentViewModel model)
    {
        AppendAuthorizationHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5084/api/Inventory/adjust", model);
            if (response.IsSuccessStatusCode)
                return new JsonResult(new { success = true, message = "Điều chỉnh tồn kho thành công!" });
            var err = await ExtractErrorMessageAsync(response, "Không thể điều chỉnh tồn kho.");
            return new JsonResult(new { success = false, message = err });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message });
        }
    }


    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (ActiveCartRegistry.IsPartInAnyCart(id))
            return new JsonResult(new { success = false, message = "Không thể xóa phụ tùng này vì sản phẩm đang nằm trong giỏ hàng của khách hàng!" });
        AppendAuthorizationHeader();
        try
        {
            var response = await _httpClient.DeleteAsync($"{PartsApiUrl}/{id}");
            if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Xóa phụ tùng thành công!" });
            var err = await ExtractErrorMessageAsync(response, "Không thể xóa phụ tùng.");
            return new JsonResult(new { success = false, message = err });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi kết nối: " + ex.Message }); }
    }

    public async Task<IActionResult> OnPostDiscontinueAsync(int id)
    {
        if (ActiveCartRegistry.IsPartInAnyCart(id))
            return new JsonResult(new { success = false, message = "Không thể ngưng bán phụ tùng này vì sản phẩm đang nằm trong giỏ hàng của khách hàng!" });
        AppendAuthorizationHeader();
        var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{id}");
        if (part == null) return new JsonResult(new { success = false, message = "Không tìm thấy phụ tùng cần cập nhật." });
        part.Status = "Inactive";
        var response = await _httpClient.PutAsJsonAsync($"{PartsApiUrl}/{id}", part);
        if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Đã ngưng bán phụ tùng thành công!" });
        var err = await ExtractErrorMessageAsync(response, "Không thể ngưng bán phụ tùng.");
        return new JsonResult(new { success = false, message = err });
    }

    public async Task<IActionResult> OnPostResellAsync(int id, int quantity)
    {
        if (quantity <= 0) return new JsonResult(new { success = false, message = "Số lượng bán lại phải lớn hơn 0." });
        AppendAuthorizationHeader();
        var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{id}");
        if (part == null) return new JsonResult(new { success = false, message = "Không tìm thấy phụ tùng cần cập nhật." });
        part.Quantity = quantity;
        part.Status = "Available";
        var response = await _httpClient.PutAsJsonAsync($"{PartsApiUrl}/{id}", part);
        if (response.IsSuccessStatusCode) return new JsonResult(new { success = true, message = "Bán lại phụ tùng thành công và chuyển trạng thái sang đang bán!" });
        var err = await ExtractErrorMessageAsync(response, "Không thể bán lại phụ tùng.");
        return new JsonResult(new { success = false, message = err });
    }

    private async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response, string defaultMessage)
    {
        try
        {
            var errContent = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (errContent.TryGetProperty("message", out var msgProp)) return msgProp.GetString()!;
            if (errContent.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
            {
                var errorsList = new List<string>();
                foreach (var prop in errorsProp.EnumerateObject())
                    foreach (var err in prop.Value.EnumerateArray())
                        errorsList.Add(err.GetString()!);
                if (errorsList.Any()) return string.Join("<br/>", errorsList);
            }
        }
        catch
        {
            try { var rawStr = await response.Content.ReadAsStringAsync(); if (!string.IsNullOrWhiteSpace(rawStr) && rawStr.Length < 200) return rawStr; } catch { }
        }
        return defaultMessage;
    }
}
