using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers;

public class ComboOrderController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public ComboOrderController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    private void AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Confirm(string draft, string? type = null)
    {
        if (string.IsNullOrEmpty(draft))
        {
            TempData["ErrorMessage"] = "Khong tim thay thong tin gio hang dat mua.";
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders/draft-preview", draft);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                try
                {
                    var doc = JsonDocument.Parse(errorMsg);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        TempData["ErrorMessage"] = msgProp.GetString();
                        return RedirectToAction("Index", "Home");
                    }
                }
                catch
                {
                }

                TempData["ErrorMessage"] = "Token gio hang khong hop le hoac da het han.";
                return RedirectToAction("Index", "Home");
            }

            var apiResult = await response.Content.ReadFromJsonAsync<ApiResponseWrapper<ComboOrderPreviewViewModel>>();
            if (apiResult == null || !apiResult.Success || apiResult.Data == null)
            {
                TempData["ErrorMessage"] = "Khong the tai thong tin xem truoc don hang.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
            ViewBag.DraftToken = draft;
            ViewBag.PreferredType = string.Equals(type, "deposit", StringComparison.OrdinalIgnoreCase)
                ? "Deposit"
                : "Buyout";

            return View(apiResult.Data);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Co loi xay ra khi xu ly don hang: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] ComboOrderCreateViewModel model)
    {
        if (!User.Identity!.IsAuthenticated)
        {
            return Json(new { success = false, message = "Ban can dang nhap de dat hang." });
        }

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Du lieu nhap vao khong hop le." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders", model);
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Json(new { success = false, message = "Phien dang nhap da het han. Vui long dang nhap lai." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                var emptyMessage = response.IsSuccessStatusCode
                    ? "May chu tra ve du lieu rong khi tao don combo. Vui long thu lai."
                    : $"Khong the tao don combo. May chu khong tra ve du lieu (HTTP {(int)response.StatusCode}).";
                return Json(new { success = false, message = emptyMessage });
            }

            if (!TryParseJsonDocument(content, out var doc))
            {
                var invalidMessage = response.IsSuccessStatusCode
                    ? "May chu tra ve du lieu khong hop le khi tao don combo."
                    : $"Khong the tao don combo. Phan hoi may chu khong hop le (HTTP {(int)response.StatusCode}).";
                return Json(new { success = false, message = invalidMessage });
            }

            using (doc)
            {
                var success = doc.RootElement.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                var message = ExtractJsonMessage(doc.RootElement, "Dat hang that bai.");

                if (response.IsSuccessStatusCode && success)
                {
                    int orderId = 0;
                    string status = "Pending";
                    decimal totalAmount = 0;
                    decimal depositAmount = 0;

                    if (doc.RootElement.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.TryGetProperty("comboOrderId", out var idProp))
                        {
                            orderId = idProp.GetInt32();
                        }

                        if (dataProp.TryGetProperty("status", out var statusProp))
                        {
                            status = statusProp.GetString() ?? "Pending";
                        }

                        if (dataProp.TryGetProperty("totalAmount", out var totalProp))
                        {
                            totalAmount = totalProp.GetDecimal();
                        }

                        if (dataProp.TryGetProperty("depositAmount", out var depositProp) && depositProp.ValueKind != JsonValueKind.Null)
                        {
                            depositAmount = depositProp.GetDecimal();
                        }
                    }

                    return Json(new
                    {
                        success = true,
                        message,
                        orderId,
                        status,
                        totalAmount,
                        depositAmount
                    });
                }

                return Json(new { success = false, message });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Loi ket noi may chu: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        if (!User.Identity!.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        try
        {
            AttachJwtToken();
            var orders = await FetchComboOrdersAsync();
            return View(orders ?? new List<ComboOrderViewModel>());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Loi: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    public async Task<IActionResult> AdminOrders()
    {
        if (!User.Identity!.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            AttachJwtToken();
            var orders = await FetchComboOrdersAsync();
            return View(orders ?? new List<ComboOrderViewModel>());
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Loi: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusModel model)
    {
        if (!User.Identity!.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Json(new { success = false, message = "Ban khong co quyen thuc hien hanh dong nay." });
        }

        try
        {
            AttachJwtToken();
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_apiBaseUrl}/api/combo-orders/{id}/status")
            {
                Content = JsonContent.Create(new { status = model.Status })
            };

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var success = response.IsSuccessStatusCode;
            var message = ExtractMessageFromResponse(content, response, "Cap nhat that bai.");

            return Json(new { success, message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Loi ket noi may chu: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GenerateCaptcha(int id, [FromBody] GenerateComboCaptchaModel? model)
    {
        if (!User.Identity!.IsAuthenticated || !User.IsInRole("Admin"))
        {
            return Json(new { success = false, message = "Ban khong co quyen thuc hien hanh dong nay." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders/{id}/generate-captcha", new { code = model?.Code });
            var content = await response.Content.ReadAsStringAsync();

            if (!TryParseJsonDocument(content, out var doc))
            {
                return Json(new
                {
                    success = false,
                    message = ExtractMessageFromResponse(content, response, "Khong the tao captcha.")
                });
            }

            using (doc)
            {
                var success = response.IsSuccessStatusCode &&
                              doc.RootElement.TryGetProperty("success", out var sProp) &&
                              sProp.GetBoolean();
                var message = ExtractJsonMessage(doc.RootElement, "Khong the tao captcha.");

                string? captchaCode = null;
                string? stage = null;
                string? generatedAt = null;
                string? purchaseType = null;
                string? status = null;
                if (TryGetJsonProperty(doc.RootElement, "data", "Data", out var dataProp))
                {
                    captchaCode = ReadJsonString(dataProp, "captchaCode", "CaptchaCode");
                    stage = ReadJsonString(dataProp, "stage", "Stage");
                    generatedAt = ReadJsonString(dataProp, "generatedAt", "GeneratedAt");
                }

                if (success)
                {
                    var refreshedOrder = await FetchComboOrderByIdAsync(id);
                    if (refreshedOrder != null)
                    {
                        purchaseType = refreshedOrder.PurchaseType;
                        status = refreshedOrder.Status;

                        var usesFinalCaptcha = string.Equals(refreshedOrder.PurchaseType, "Deposit", StringComparison.OrdinalIgnoreCase) &&
                                               string.Equals(refreshedOrder.Status, "Deposited", StringComparison.OrdinalIgnoreCase);

                        captchaCode = usesFinalCaptcha
                            ? refreshedOrder.FinalCaptchaCode
                            : refreshedOrder.CaptchaCode;

                        generatedAt = (usesFinalCaptcha
                                ? refreshedOrder.FinalCaptchaGeneratedAt
                                : refreshedOrder.CaptchaGeneratedAt)
                            ?.ToString("O");

                        stage = ResolveStageForClient(refreshedOrder);
                    }
                }

                return Json(new
                {
                    success,
                    message,
                    captchaCode,
                    stage,
                    generatedAt,
                    purchaseType,
                    status,
                    order = success ? await FetchComboOrderByIdAsync(id) : null
                });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Loi ket noi may chu: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> VerifyCaptcha(int id, [FromBody] VerifyComboCaptchaModel model)
    {
        if (!User.Identity!.IsAuthenticated)
        {
            return Json(new { success = false, message = "Ban can dang nhap de xac thuc don hang." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/combo-orders/{id}/verify-captcha", new { captchaCode = model.CaptchaCode });
            var content = await response.Content.ReadAsStringAsync();

            if (!TryParseJsonDocument(content, out var doc))
            {
                return Json(new
                {
                    success = false,
                    message = ExtractMessageFromResponse(content, response, "Khong the xac thuc captcha.")
                });
            }

            using (doc)
            {
                var success = response.IsSuccessStatusCode &&
                              doc.RootElement.TryGetProperty("success", out var sProp) &&
                              sProp.GetBoolean();
                var message = ExtractJsonMessage(doc.RootElement, "Khong the xac thuc captcha.");

                string? status = null;
                string? depositExpiresAt = null;
                decimal? depositAmount = null;
                decimal? totalAmount = null;

                if (doc.RootElement.TryGetProperty("data", out var dataProp))
                {
                    if (dataProp.TryGetProperty("status", out var statusProp))
                    {
                        status = statusProp.GetString();
                    }

                    if (dataProp.TryGetProperty("depositExpiresAt", out var expiryProp) && expiryProp.ValueKind != JsonValueKind.Null)
                    {
                        depositExpiresAt = expiryProp.GetString();
                    }

                    if (dataProp.TryGetProperty("depositAmount", out var depositProp) && depositProp.ValueKind != JsonValueKind.Null)
                    {
                        depositAmount = depositProp.GetDecimal();
                    }

                    if (dataProp.TryGetProperty("totalAmount", out var totalProp) && totalProp.ValueKind != JsonValueKind.Null)
                    {
                        totalAmount = totalProp.GetDecimal();
                    }
                }

                return Json(new { success, message, status, depositExpiresAt, depositAmount, totalAmount });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Loi ket noi may chu: " + ex.Message });
        }
    }

    private async Task<List<ComboOrderViewModel>> FetchComboOrdersAsync()
    {
        var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/combo-orders");
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Khong the tai danh sach don combo.");
        }

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<ComboOrderViewModel>();
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return DeserializeOrders(root);
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var dataProp) &&
            dataProp.ValueKind == JsonValueKind.Array)
        {
            return DeserializeOrders(dataProp);
        }

        return new List<ComboOrderViewModel>();
    }

    private async Task<ComboOrderViewModel?> FetchComboOrderByIdAsync(int id)
    {
        AttachJwtToken();
        var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/combo-orders/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        JsonElement orderElement;

        if (root.ValueKind == JsonValueKind.Object &&
            TryGetJsonProperty(root, "data", "Data", out var dataProp) &&
            dataProp.ValueKind == JsonValueKind.Object)
        {
            orderElement = dataProp;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            orderElement = root;
        }
        else
        {
            return null;
        }

        var order = JsonSerializer.Deserialize<ComboOrderViewModel>(
            orderElement.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (order != null)
        {
            order.Items ??= new List<ComboOrderItemViewModel>();
        }

        return order;
    }

    private static List<ComboOrderViewModel> DeserializeOrders(JsonElement element)
    {
        var orders = JsonSerializer.Deserialize<List<ComboOrderViewModel>>(
            element.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<ComboOrderViewModel>();

        foreach (var order in orders)
        {
            order.Items ??= new List<ComboOrderItemViewModel>();
        }

        return orders;
    }

    private static bool TryParseJsonDocument(string content, out JsonDocument? doc)
    {
        doc = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            doc = JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [HttpGet]
    public async Task<IActionResult> OrderDetail(int id)
    {
        if (!User.Identity!.IsAuthenticated)
        {
            return Json(new { success = false, message = "Ban can dang nhap de xem chi tiet don combo." });
        }

        try
        {
            AttachJwtToken();
            var order = await FetchComboOrderByIdAsync(id);
            if (order == null)
            {
                return Json(new { success = false, message = "Khong tim thay don combo." });
            }

            var stage = ResolveStageForClient(order);
            var useFinalCaptcha = string.Equals(order.PurchaseType, "Deposit", StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(order.Status, "Deposited", StringComparison.OrdinalIgnoreCase);

            return Json(new
            {
                success = true,
                order,
                stage,
                currentCaptchaCode = useFinalCaptcha ? order.FinalCaptchaCode : order.CaptchaCode,
                currentGeneratedAt = (useFinalCaptcha ? order.FinalCaptchaGeneratedAt : order.CaptchaGeneratedAt)?.ToString("O")
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Loi ket noi may chu: " + ex.Message });
        }
    }

    private static string ExtractJsonMessage(JsonElement root, string fallbackMessage)
    {
        if (root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
        {
            return messageProp.GetString() ?? fallbackMessage;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("errors", out var errorsProp) &&
            errorsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in errorsProp.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            return item.GetString() ?? fallbackMessage;
                        }
                    }
                }
            }
        }

        return fallbackMessage;
    }

    private static string ExtractMessageFromResponse(string content, HttpResponseMessage response, string fallbackMessage)
    {
        if (TryParseJsonDocument(content, out var doc))
        {
            using (doc)
            {
                return ExtractJsonMessage(doc.RootElement, fallbackMessage);
            }
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Phien dang nhap da het han. Vui long dang nhap lai."
                : fallbackMessage;
        }

        return fallbackMessage;
    }

    private static string? ReadJsonString(JsonElement element, string camelCaseName, string pascalCaseName)
    {
        if (element.TryGetProperty(camelCaseName, out var camelProp) && camelProp.ValueKind == JsonValueKind.String)
        {
            return camelProp.GetString();
        }

        if (element.TryGetProperty(pascalCaseName, out var pascalProp) && pascalProp.ValueKind == JsonValueKind.String)
        {
            return pascalProp.GetString();
        }

        return null;
    }

    private static bool TryGetJsonProperty(JsonElement element, string camelCaseName, string pascalCaseName, out JsonElement property)
    {
        if (element.TryGetProperty(camelCaseName, out property))
        {
            return true;
        }

        if (element.TryGetProperty(pascalCaseName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static string ResolveStageForClient(ComboOrderViewModel order)
    {
        if (string.Equals(order.PurchaseType, "Deposit", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(order.Status, "Deposited", StringComparison.OrdinalIgnoreCase)
                ? "buyout"
                : "deposit";
        }

        return "buyout";
    }
}

public class UpdateStatusModel
{
    public string Status { get; set; } = null!;
}

public class GenerateComboCaptchaModel
{
    public string? Code { get; set; }
}

public class VerifyComboCaptchaModel
{
    public string CaptchaCode { get; set; } = null!;
}

public class ApiResponseWrapper<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public T? Data { get; set; }
}
