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
        
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                return RedirectToAction("Index");
            }
            
            // Build initial BookingViewModel
            var model = new BookingViewModel
            {
                AppointmentDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
                AppointmentTime = new TimeOnly(9, 0)
            };
            
            ViewBag.Cart = cart;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(BookingViewModel model, string? shippingAddress)
        {
            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                return RedirectToAction("Index");
            }

            bool hasOnlyParts = cart.Items.All(i => i.ItemType == "Part");
            if (hasOnlyParts)
            {
                ModelState.Remove("AppointmentDate");
                ModelState.Remove("AppointmentTime");
                ModelState.Remove("CarName");
                ModelState.Remove("LicensePlate");

                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0";
                model.AppointmentDate = DateOnly.FromDateTime(DateTime.Now);
                model.AppointmentTime = TimeOnly.FromDateTime(DateTime.Now);
                model.CarName = "N/A - Đơn phụ tùng";
                model.LicensePlate = "PART-CUST-" + userId;
                model.Note = (model.Note ?? "") + $"\n[Đơn Phụ Tùng] Địa chỉ giao hàng: {shippingAddress}";
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cart = cart;
                return View(model);
            }

            // Populate items from Cart to Model
            foreach (var item in cart.Items)
            {
                if (item.ItemType == "Package")
                {
                    model.PackageIds.Add(item.ItemId);
                }
                else if (item.ItemType == "Service")
                {
                    model.ServiceIds.Add(item.ItemId);
                }
                else if (item.ItemType == "Part")
                {
                    model.PartItems.Add(new UnifiedPartItemViewModel
                    {
                        PartId = item.ItemId,
                        Quantity = item.Quantity
                    });
                }
            }

            // Post to backend
            var client = _httpClientFactory.CreateClient("CarShowroomApi");
            client.BaseAddress = new Uri("http://localhost:5084");
            
            // Add auth header if user is logged in
            var token = User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int customerId))
                {
                    model.CustomerId = customerId;
                }
            }

            var response = await client.PostAsJsonAsync("/api/maintenanceappointments/create-with-details", model);

            if (response.IsSuccessStatusCode)
            {
                // Clear cart
                SaveCart(new UnifiedCart());
                TempData["SuccessMessage"] = "Đơn hàng và Đặt lịch của bạn đã được ghi nhận thành công!";
                return RedirectToAction("Index", "Home"); // Or some success page
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Lỗi từ server: {response.StatusCode} - {error}");
                ViewBag.Cart = cart;
                return View(model);
            }
        }
    }
}
