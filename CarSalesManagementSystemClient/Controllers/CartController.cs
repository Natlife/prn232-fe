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

            if (item == null)
            {
                return Json(new { success = false, message = "Item not found." });
            }

            var cart = GetCart();
            
            // Check Package-Service Conflict Logic
            if (itemType == "Service")
            {
                var response = await client.GetAsync($"/api/packageservices/service/{itemId}");
                if (response.IsSuccessStatusCode)
                {
                    var packageServices = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                    if (packageServices != null && packageServices.Length > 0)
                    {
                        foreach (var ps in packageServices)
                        {
                            int pkgId = ps.GetProperty("packageId").GetInt32();
                            if (cart.Items.Any(i => i.ItemType == "Package" && i.ItemId == pkgId))
                            {
                                return Json(new { success = false, message = "This service is already included in a Maintenance Package you have in your cart." });
                            }
                        }
                    }
                }
            }
            else if (itemType == "Package")
            {
                var response = await client.GetAsync($"/api/packageservices/package/{itemId}");
                if (response.IsSuccessStatusCode)
                {
                    var packageServices = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                    if (packageServices != null)
                    {
                        foreach (var ps in packageServices)
                        {
                            int srvId = ps.GetProperty("serviceId").GetInt32();
                            var existingService = cart.Items.FirstOrDefault(i => i.ItemType == "Service" && i.ItemId == srvId);
                            if (existingService != null)
                            {
                                // Remove standalone service if package is added
                                cart.RemoveItem("Service", existingService.ItemId);
                            }
                        }
                    }
                }
            }

            cart.AddItem(item);
            SaveCart(cart);

            return Json(new { success = true, cartCount = cart.Items.Sum(i => i.Quantity) });
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

            bool hasStandalone = standaloneParts.Any();
            bool hasMaintenance = maintenanceParts.Any() || maintenancePackages.Any() || maintenanceServices.Any();

            if (!hasStandalone && !hasMaintenance)
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

            var token = User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            string? partOrderIdStr = null;
            string? appointmentIdStr = null;

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
                        var errorMsg = await partResponse.Content.ReadAsStringAsync();
                        return Json(new { success = false, message = $"Lỗi đặt hàng phụ tùng: {errorMsg}" });
                    }

                    var createdOrder = await partResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (createdOrder.TryGetProperty("orderId", out var idProp))
                    {
                        partOrderIdStr = "#PO" + idProp.GetInt32().ToString("D4");
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
                        var errorMsg = await maintResponse.Content.ReadAsStringAsync();
                        return Json(new { success = false, message = $"Lỗi đặt lịch bảo dưỡng: {errorMsg}" });
                    }

                    var createdAppointment = await maintResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (createdAppointment.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("appointmentId", out var apptIdProp))
                    {
                        appointmentIdStr = "#MA" + apptIdProp.GetInt32().ToString("D4");
                    }
                }

                // Successfully created all required requests, clear the cart session!
                SaveCart(new UnifiedCart());

                return Json(new { success = true, partOrderId = partOrderIdStr, appointmentId = appointmentIdStr });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi kết nối tới máy chủ: " + ex.Message });
            }
        }
    }
}
