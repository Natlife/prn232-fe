using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using CarSalesManagementSystemClient.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace CarSalesManagementSystemClient.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "UnifiedCartSession";
        private readonly IHttpClientFactory _httpClientFactory;

        public CartController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private UnifiedCart GetCart()
        {
            var sessionString = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(sessionString))
            {
                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var env = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
                        if (env != null)
                        {
                            var filePath = CarSalesManagementSystemClient.Helpers.CartHelper.GetCartFilePath(userId, env);
                            if (System.IO.File.Exists(filePath))
                            {
                                var savedCartJson = System.IO.File.ReadAllText(filePath);
                                if (!string.IsNullOrEmpty(savedCartJson) && savedCartJson != "{\"Items\":[]}")
                                {
                                    HttpContext.Session.SetString(CartSessionKey, savedCartJson);
                                    return JsonSerializer.Deserialize<UnifiedCart>(savedCartJson) ?? new UnifiedCart();
                                }
                            }
                        }
                    }
                }
                return new UnifiedCart();
            }
            return JsonSerializer.Deserialize<UnifiedCart>(sessionString) ?? new UnifiedCart();
        }

        private void SaveCart(UnifiedCart cart)
        {
            var sessionString = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CartSessionKey, sessionString);

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var env = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
                    if (env != null)
                    {
                        CarSalesManagementSystemClient.Helpers.CartHelper.SaveCartToFile(userId, env, sessionString);
                    }
                }
            }
        }

        public IActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(string itemType, int itemId, int quantity = 1)
        {
            // Validate against Backend APIs before adding
            var client = _httpClientFactory.CreateClient("CarShowroomApi");
            client.BaseAddress = new Uri("http://localhost:5084");
            UnifiedCartItem? item = null;

            if (itemType == "Part")
            {
                var response = await client.GetAsync($"/api/parts/{itemId}");
                if (response.IsSuccessStatusCode)
                {
                    var part = await response.Content.ReadFromJsonAsync<JsonElement>();
                    item = new UnifiedCartItem
                    {
                        ItemType = "Part",
                        ItemId = part.GetProperty("partId").GetInt32(),
                        Name = part.GetProperty("partName").GetString() ?? "",
                        Price = part.GetProperty("price").GetDecimal(),
                        Quantity = quantity,
                        ImageUrl = part.TryGetProperty("imageUrl", out var img) ? img.GetString() : null
                    };
                }
            }
            else if (itemType == "Service")
            {
                var response = await client.GetAsync($"/api/services/{itemId}");
                if (response.IsSuccessStatusCode)
                {
                    var root = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (root.TryGetProperty("data", out var svc))
                    {
                        item = new UnifiedCartItem
                        {
                            ItemType = "Service",
                            ItemId = svc.GetProperty("serviceId").GetInt32(),
                            Name = svc.GetProperty("serviceName").GetString() ?? "",
                            Price = svc.GetProperty("basePrice").GetDecimal(),
                            Quantity = 1
                        };
                    }
                }
            }
            else if (itemType == "Package")
            {
                var response = await client.GetAsync($"/api/maintenancepackages/{itemId}");
                if (response.IsSuccessStatusCode)
                {
                    var root = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (root.TryGetProperty("data", out var pkg))
                    {
                        item = new UnifiedCartItem
                        {
                            ItemType = "Package",
                            ItemId = pkg.GetProperty("packageId").GetInt32(),
                            Name = pkg.GetProperty("packageName").GetString() ?? "",
                            Price = pkg.GetProperty("packagePrice").GetDecimal(),
                            Quantity = 1
                        };
                    }
                }
            }
            else if (itemType == "Car")
            {
                // Xe lấy từ OData (property có thể là PascalCase 'CarId' hoặc camelCase 'carId') — đọc không phân biệt hoa/thường.
                var response = await client.GetAsync($"/odata/Cars({itemId})");
                if (response.IsSuccessStatusCode)
                {
                    var car = await response.Content.ReadFromJsonAsync<JsonElement>();

                    JsonElement? Prop(string name)
                    {
                        foreach (var p in car.EnumerateObject())
                        {
                            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                                return p.Value;
                        }
                        return null;
                    }

                    var status = Prop("Status")?.GetString() ?? "Available";
                    if (status == "Sold" || status == "Inactive")
                    {
                        return Json(new { success = false, message = "Xe này hiện không còn được bán." });
                    }

                    var idProp = Prop("CarId");
                    if (idProp != null)
                    {
                        item = new UnifiedCartItem
                        {
                            ItemType = "Car",
                            ItemId = idProp.Value.GetInt32(),
                            Name = Prop("CarName")?.GetString() ?? "",
                            Price = Prop("Price")?.GetDecimal() ?? 0m,
                            Quantity = 1,
                            ImageUrl = Prop("ImageUrl")?.GetString()
                        };
                    }
                }
            }

            if (item == null)
            {
                return Json(new { success = false, message = "Item not found." });
            }

            var cart = GetCart();
            
            // Check Package-Service Conflict Logic
            if (itemType == "Package")
            {
                try
                {
                    var response = await client.GetAsync($"/api/MaintenancePackages/{itemId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("data", out var dataEl))
                        {
                            if (dataEl.TryGetProperty("serviceIds", out var serviceIdsEl) && serviceIdsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var srvIdProp in serviceIdsEl.EnumerateArray())
                                {
                                    int srvId = srvIdProp.GetInt32();
                                    var existingService = cart.Items.FirstOrDefault(i => i.ItemType == "Service" && i.ItemId == srvId);
                                    if (existingService != null)
                                    {
                                        cart.RemoveItem("Service", existingService.ItemId);
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore package conflict check failure if package detail endpoint fails
                }
            }

            cart.AddItem(item);
            SaveCart(cart);

            return Json(new { success = true, cartCount = cart.Items.Sum(i => i.Quantity) });
        }

        // GET: /Cart/QuickOrder?draft=<base64>&type=deposit|buyout
        // Chatbot tạo link này: điền sẵn giỏ hàng từ token rồi chuyển tới trang xác nhận thanh toán.
        [HttpGet]
        public async Task<IActionResult> QuickOrder(string draft, string? type = null)
        {
            if (string.IsNullOrWhiteSpace(draft))
            {
                return RedirectToAction("Index");
            }

            List<QuickOrderItem>? items = null;
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(draft));
                items = JsonSerializer.Deserialize<List<QuickOrderItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                TempData["ErrorMessage"] = "Liên kết đặt hàng không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("Index");
            }

            if (items == null || items.Count == 0)
            {
                return RedirectToAction("Index");
            }

            var client = _httpClientFactory.CreateClient("CarShowroomApi");
            client.BaseAddress = new Uri("http://localhost:5084");

            var cart = GetCart();
            foreach (var it in items)
            {
                var resolved = await ResolveCartItemAsync(client, it.ItemType, it.ReferenceId, it.Quantity <= 0 ? 1 : it.Quantity);
                if (resolved != null) cart.AddItem(resolved);
            }
            SaveCart(cart);

            if (cart.Items.Count == 0)
            {
                TempData["ErrorMessage"] = "Không thêm được sản phẩm nào (có thể đã hết hàng).";
                return RedirectToAction("Index");
            }

            // Chuyển thẳng tới trang xác nhận thanh toán.
            return RedirectToAction("Checkout");
        }

        /// <summary>Lấy thông tin 1 sản phẩm từ API và tạo UnifiedCartItem. Chatbot "Service" = gói bảo dưỡng (Package).</summary>
        private async Task<UnifiedCartItem?> ResolveCartItemAsync(HttpClient client, string itemType, int itemId, int quantity)
        {
            try
            {
                if (string.Equals(itemType, "Part", StringComparison.OrdinalIgnoreCase))
                {
                    var resp = await client.GetAsync($"/api/parts/{itemId}");
                    if (!resp.IsSuccessStatusCode) return null;
                    var part = await resp.Content.ReadFromJsonAsync<JsonElement>();
                    return new UnifiedCartItem
                    {
                        ItemType = "Part",
                        ItemId = part.GetProperty("partId").GetInt32(),
                        Name = part.GetProperty("partName").GetString() ?? "",
                        Price = part.GetProperty("price").GetDecimal(),
                        Quantity = quantity,
                        ImageUrl = part.TryGetProperty("imageUrl", out var img) ? img.GetString() : null
                    };
                }
                if (string.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(itemType, "Package", StringComparison.OrdinalIgnoreCase))
                {
                    // Chatbot index MaintenancePackages dưới nhãn "Service" -> thêm vào giỏ dạng Package.
                    var resp = await client.GetAsync($"/api/maintenancepackages/{itemId}");
                    if (!resp.IsSuccessStatusCode) return null;
                    var root = await resp.Content.ReadFromJsonAsync<JsonElement>();
                    if (!root.TryGetProperty("data", out var pkg)) return null;
                    return new UnifiedCartItem
                    {
                        ItemType = "Package",
                        ItemId = pkg.GetProperty("packageId").GetInt32(),
                        Name = pkg.GetProperty("packageName").GetString() ?? "",
                        Price = pkg.GetProperty("packagePrice").GetDecimal(),
                        Quantity = 1
                    };
                }
                if (string.Equals(itemType, "Car", StringComparison.OrdinalIgnoreCase))
                {
                    var resp = await client.GetAsync($"/odata/Cars({itemId})");
                    if (!resp.IsSuccessStatusCode) return null;
                    var car = await resp.Content.ReadFromJsonAsync<JsonElement>();
                    JsonElement? Prop(string name)
                    {
                        foreach (var p in car.EnumerateObject())
                            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
                        return null;
                    }
                    var status = Prop("Status")?.GetString() ?? "Available";
                    if (status == "Sold" || status == "Inactive") return null;
                    var idProp = Prop("CarId");
                    if (idProp == null) return null;
                    return new UnifiedCartItem
                    {
                        ItemType = "Car",
                        ItemId = idProp.Value.GetInt32(),
                        Name = Prop("CarName")?.GetString() ?? "",
                        Price = Prop("Price")?.GetDecimal() ?? 0m,
                        Quantity = 1,
                        ImageUrl = Prop("ImageUrl")?.GetString()
                    };
                }
            }
            catch { /* bỏ qua món lỗi */ }
            return null;
        }

        public class QuickOrderItem
        {
            public string ItemType { get; set; } = "";
            public int ReferenceId { get; set; }
            public int Quantity { get; set; } = 1;
        }

        [HttpPost]
        public IActionResult RemoveFromCart(string itemType, int itemId)
        {
            var cart = GetCart();
            cart.RemoveItem(itemType, itemId);
            SaveCart(cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateQuantity(string itemType, int itemId, int quantity)
        {
            var cart = GetCart();
            cart.UpdateQuantity(itemType, itemId, quantity);
            SaveCart(cart);
            return RedirectToAction("Index");
        }
        
        public IActionResult GetCartCount()
        {
            var cart = GetCart();
            return Json(new { count = cart.Items.Sum(i => i.Quantity) });
        }
        
        [HttpPost]
        public IActionResult UpdatePurpose(string itemType, int itemId, string purpose)
        {
            var cart = GetCart();
            var item = cart.Items.FirstOrDefault(i => i.ItemType == itemType && i.ItemId == itemId);
            if (item != null)
            {
                item.Purpose = purpose;
                SaveCart(cart);
            }
            return Json(new { success = true, purpose = item?.Purpose });
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Auth", new { returnUrl = "/Cart/Checkout" });
            }

            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                return RedirectToAction("Index");
            }

            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value ?? "";

            var model = new UnifiedCheckoutPostModel
            {
                CustomerName = User.Identity.Name ?? "",
                CustomerPhone = phone,
                CustomerEmail = email,
                AppointmentDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
                AppointmentTime = "09:00",
                DeliveryMethod = "Pickup"
            };

            ViewBag.Cart = cart;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] UnifiedCheckoutPostModel model)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để tiếp tục." });
            }

            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                return Json(new { success = false, message = "Giỏ hàng của bạn đang trống." });
            }

            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(model.CustomerName))
            {
                return Json(new { success = false, message = "Vui lòng nhập họ và tên." });
            }

            if (string.IsNullOrWhiteSpace(model.CustomerPhone))
            {
                return Json(new { success = false, message = "Vui lòng nhập số điện thoại." });
            }

            var standaloneParts = cart.Items.Where(i => i.ItemType == "Part" && i.Purpose == "Standalone").ToList();
            var maintenanceParts = cart.Items.Where(i => i.ItemType == "Part" && i.Purpose == "Maintenance").ToList();
            var maintenancePackages = cart.Items.Where(i => i.ItemType == "Package").ToList();
            var maintenanceServices = cart.Items.Where(i => i.ItemType == "Service").ToList();
            var carItems = cart.Items.Where(i => i.ItemType == "Car").ToList();

            bool hasStandalone = standaloneParts.Any();
            bool hasMaintenance = maintenanceParts.Any() || maintenancePackages.Any() || maintenanceServices.Any();
            bool hasCar = carItems.Any();

            if (!hasStandalone && !hasMaintenance && !hasCar)
            {
                return Json(new { success = false, message = "Giỏ hàng không có sản phẩm nào hợp lệ." });
            }

            // Validations for Standalone Parts
            if (hasStandalone)
            {
                if (model.DeliveryMethod == "Shipping" && string.IsNullOrWhiteSpace(model.ShippingAddress))
                {
                    return Json(new { success = false, message = "Vui lòng nhập địa chỉ giao hàng." });
                }
            }

            // Validations for Maintenance
            DateOnly? parsedDate = null;
            TimeOnly? parsedTime = null;
            if (hasMaintenance)
            {
                if (string.IsNullOrWhiteSpace(model.CarName) || string.IsNullOrWhiteSpace(model.LicensePlate))
                {
                    return Json(new { success = false, message = "Vui lòng nhập tên xe và biển số xe để đặt lịch bảo dưỡng." });
                }

                if (string.IsNullOrEmpty(model.AppointmentDate))
                {
                    return Json(new { success = false, message = "Vui lòng chọn ngày bảo dưỡng." });
                }

                if (string.IsNullOrEmpty(model.AppointmentTime))
                {
                    return Json(new { success = false, message = "Vui lòng chọn giờ bảo dưỡng." });
                }

                if (!DateOnly.TryParse(model.AppointmentDate, out var dateVal))
                {
                    return Json(new { success = false, message = "Định dạng ngày bảo dưỡng không hợp lệ." });
                }
                parsedDate = dateVal;

                if (!TimeOnly.TryParse(model.AppointmentTime, out var timeVal))
                {
                    return Json(new { success = false, message = "Định dạng giờ bảo dưỡng không hợp lệ." });
                }
                parsedTime = timeVal;

                var today = DateOnly.FromDateTime(DateTime.Now.Date);
                var nowTime = TimeOnly.FromDateTime(DateTime.Now);

                if (parsedDate.Value < today)
                {
                    return Json(new { success = false, message = "Ngày bảo dưỡng không được ở trong quá khứ." });
                }
                else if (parsedDate.Value == today && parsedTime.Value <= nowTime)
                {
                    return Json(new { success = false, message = "Giờ đặt lịch bảo dưỡng phải ở trong tương lai." });
                }

                if (!maintenancePackages.Any() && !maintenanceServices.Any())
                {
                    return Json(new { success = false, message = "Đặt lịch bảo dưỡng yêu cầu ít nhất một gói bảo dưỡng hoặc một dịch vụ lẻ." });
                }
            }

            // Get API client from factory
            var client = _httpClientFactory.CreateClient("CarShowroomApi");
            client.BaseAddress = new Uri("http://localhost:5084");

            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            string? partOrderIdStr = null;
            string? appointmentIdStr = null;
            int? partOrderIdNum = null;
            int? appointmentIdNum = null;
            var carRequestNums = new List<int>();

            try
            {
                // 1. Submit Standalone Parts order
                if (hasStandalone)
                {
                    var partOrderPayload = new
                    {
                        CustomerName = model.CustomerName,
                        CustomerPhone = model.CustomerPhone,
                        CustomerEmail = model.CustomerEmail,
                        ShippingAddress = model.DeliveryMethod == "Shipping" ? model.ShippingAddress : "Nhận tại showroom",
                        DeliveryMethod = model.DeliveryMethod == "Shipping" ? "Shipping" : "Pickup",
                        PaymentMethod = model.DeliveryMethod == "Shipping" ? "COD" : "BankTransfer",
                        TotalAmount = standaloneParts.Sum(p => p.SubTotal),
                        PartOrderDetails = standaloneParts.Select(p => new
                        {
                            PartId = p.ItemId,
                            Quantity = p.Quantity,
                            UnitPrice = p.Price,
                            SubTotal = p.SubTotal
                        }).ToList()
                    };

                    var partResponse = await client.PostAsJsonAsync("/api/PartOrders", partOrderPayload);
                    if (!partResponse.IsSuccessStatusCode)
                    {
                        var rawError = await partResponse.Content.ReadAsStringAsync();
                        string errorMsg = rawError;
                        try {
                            using var doc = JsonDocument.Parse(rawError);
                            if (doc.RootElement.TryGetProperty("message", out var mProp)) errorMsg = mProp.GetString() ?? rawError;
                        } catch {}
                        return Json(new { success = false, message = $"Lỗi đặt hàng phụ tùng: {errorMsg}" });
                    }

                    var createdOrder = await partResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (createdOrder.TryGetProperty("orderId", out var idProp))
                    {
                        partOrderIdNum = idProp.GetInt32();
                        partOrderIdStr = "#PO" + partOrderIdNum.Value.ToString("D4");
                    }
                }

                // 2. Submit Maintenance booking
                if (hasMaintenance)
                {
                    var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    int? customerId = null;
                    if (int.TryParse(userIdStr, out int parsedId))
                    {
                        customerId = parsedId;
                    }

                    var maintenancePayload = new
                    {
                        CustomerId = customerId,
                        CustomerCarId = 0,
                        CustomerName = model.CustomerName,
                        CustomerPhone = model.CustomerPhone,
                        CustomerEmail = model.CustomerEmail,
                        CarName = model.CarName,
                        LicensePlate = model.LicensePlate,
                        AppointmentDate = parsedDate,
                        AppointmentTime = parsedTime,
                        Note = model.Note,
                        PackageIds = maintenancePackages.Select(p => p.ItemId).ToList(),
                        ServiceIds = maintenanceServices.Select(s => s.ItemId).ToList(),
                        PartItems = maintenanceParts.Select(p => new
                        {
                            PartId = p.ItemId,
                            Quantity = p.Quantity
                        }).ToList()
                    };

                    var maintResponse = await client.PostAsJsonAsync("/api/maintenanceappointments/create-with-details", maintenancePayload);
                    if (!maintResponse.IsSuccessStatusCode)
                    {
                        var rawError = await maintResponse.Content.ReadAsStringAsync();
                        string errorMsg = rawError;
                        try {
                            using var doc = JsonDocument.Parse(rawError);
                            if (doc.RootElement.TryGetProperty("message", out var mProp)) errorMsg = mProp.GetString() ?? rawError;
                        } catch {}
                        return Json(new { success = false, message = $"Lỗi đặt lịch bảo dưỡng: {errorMsg}" });
                    }

                    var createdAppointment = await maintResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (createdAppointment.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("appointmentId", out var apptIdProp))
                    {
                        appointmentIdNum = apptIdProp.GetInt32();
                        appointmentIdStr = "#MA" + appointmentIdNum.Value.ToString("D4");
                    }
                }

                // 3. Gửi yêu cầu mua xe cho từng xe trong giỏ (nhân viên sẽ lập hóa đơn tổng + mã captcha).
                var carRequestIds = new List<string>();
                if (hasCar)
                {
                    foreach (var car in carItems)
                    {
                        var carPayload = new
                        {
                            CarId = car.ItemId,
                            CustomerName = model.CustomerName,
                            CustomerPhone = model.CustomerPhone,
                            CustomerEmail = model.CustomerEmail,
                            Message = $"Yêu cầu mua xe '{car.Name}' từ giỏ hàng."
                        };

                        var carResponse = await client.PostAsJsonAsync("/api/car-sales/requests", carPayload);
                        if (!carResponse.IsSuccessStatusCode)
                        {
                            if (carResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại rồi đặt lại đơn.", requireLogin = true });
                            var rawError = await carResponse.Content.ReadAsStringAsync();
                            string errorMsg = rawError;
                            try {
                                using var doc = JsonDocument.Parse(rawError);
                                if (doc.RootElement.TryGetProperty("message", out var mProp)) errorMsg = mProp.GetString() ?? rawError;
                            } catch {}
                            if (string.IsNullOrWhiteSpace(errorMsg)) errorMsg = "HTTP " + (int)carResponse.StatusCode;
                            return Json(new { success = false, message = $"Lỗi gửi yêu cầu mua xe '{car.Name}': {errorMsg}" });
                        }

                        var createdCar = await carResponse.Content.ReadFromJsonAsync<JsonElement>();
                        int reqId = 0;
                        if (createdCar.TryGetProperty("data", out var carData))
                        {
                            if (carData.TryGetProperty("requestId", out var rProp) || carData.TryGetProperty("RequestId", out rProp))
                                reqId = rProp.GetInt32();
                        }
                        if (reqId > 0)
                        {
                            carRequestNums.Add(reqId);
                            carRequestIds.Add("#CR" + reqId.ToString("D4"));
                        }
                    }
                }

                // 4. Gộp tất cả thành MỘT hóa đơn tổng (MasterInvoice) với loại đặt cọc/mua đứt đã chọn.
                string purchaseType = (model.PurchaseType == "Deposit") ? "Deposit" : "Buyout";
                string? invoiceNumber = null;
                int? masterInvoiceId = null;

                var checkoutUserIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(checkoutUserIdStr, out var checkoutCustomerId);

                var checkoutPayload = new
                {
                    CustomerId = checkoutCustomerId, // server sẽ ép lại theo JWT, gửi kèm để qua validation
                    PurchaseType = purchaseType,
                    Cars = carRequestNums.Select(rid => new { PurchaseRequestId = rid, RegistrationFee = 0, PlateFee = 0, InsuranceFee = 0 }).ToList(),
                    PartOrderIds = partOrderIdNum.HasValue ? new List<int> { partOrderIdNum.Value } : new List<int>(),
                    AppointmentIds = appointmentIdNum.HasValue ? new List<int> { appointmentIdNum.Value } : new List<int>(),
                    DiscountAmount = 0,
                    TaxAmount = 0,
                    DepositExpiresInDays = 14
                };

                var invoiceResponse = await client.PostAsJsonAsync("/api/checkout/customer", checkoutPayload);
                if (!invoiceResponse.IsSuccessStatusCode)
                {
                    var errorMsg = await invoiceResponse.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = $"Đã tạo đơn nhưng lập hóa đơn tổng thất bại: {errorMsg}" });
                }
                var invoiceResult = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (invoiceResult.TryGetProperty("data", out var invData))
                {
                    if (invData.TryGetProperty("masterInvoiceId", out var miId)) masterInvoiceId = miId.GetInt32();
                    if (invData.TryGetProperty("invoiceNumber", out var inNo)) invoiceNumber = inNo.GetString();
                }

                // Successfully created all required requests, clear the cart session!
                SaveCart(new UnifiedCart());

                return Json(new
                {
                    success = true,
                    partOrderId = partOrderIdStr,
                    appointmentId = appointmentIdStr,
                    carRequestIds,
                    masterInvoiceId,
                    invoiceNumber,
                    purchaseType
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi kết nối tới máy chủ: " + ex.Message });
            }
        }
    }
}
