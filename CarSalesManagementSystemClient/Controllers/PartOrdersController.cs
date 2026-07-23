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
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CarSalesManagementSystemClient.Controllers
{
    public class PartOrdersController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _ordersApiUrl = "http://localhost:5084/api/PartOrders";
        private readonly string _partsApiUrl = "http://localhost:5084/api/Parts";

        public PartOrdersController(IHttpClientFactory httpClientFactory)
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

        private List<CartItemViewModel> GetCartFromSession()
        {
            var json = HttpContext.Session.GetString("PartCart");
            if (string.IsNullOrEmpty(json))
            {
                return new List<CartItemViewModel>();
            }
            return JsonSerializer.Deserialize<List<CartItemViewModel>>(json) ?? new List<CartItemViewModel>();
        }

        private void SaveCartToSession(List<CartItemViewModel> cart)
        {
            var json = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString("PartCart", json);
        }

        // GET: PartOrders/Cart
        public IActionResult Cart()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                TempData["Error"] = "Vui lòng đăng nhập để xem giỏ hàng.";
                return RedirectToAction("Index", "Home");
            }
            var cart = GetCartFromSession();
            return View(cart);
        }

        // POST: PartOrders/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int partId, int quantity = 1)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
            }
            try
            {
                // Fetch Part from API to verify details and stock
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{partId}");
                if (part == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phụ tùng yêu cầu." });
                }

                if (part.Quantity <= 0 || part.Status == "Out of Stock")
                {
                    return Json(new { success = false, message = "Sản phẩm đã hết hàng." });
                }

                var cart = GetCartFromSession();
                var item = cart.FirstOrDefault(x => x.PartId == partId);

                int currentQtyInCart = item?.Quantity ?? 0;
                if (currentQtyInCart + quantity > part.Quantity)
                {
                    return Json(new { success = false, message = $"Số lượng yêu cầu vượt quá tồn kho. Hiện kho chỉ còn {part.Quantity} sản phẩm (Giỏ hàng đã có {currentQtyInCart})." });
                }

                if (item != null)
                {
                    item.Quantity += quantity;
                }
                else
                {
                    cart.Add(new CartItemViewModel
                    {
                        PartId = part.PartId,
                        PartName = part.PartName,
                        PartCode = part.PartCode,
                        Price = part.Price,
                        Quantity = quantity,
                        StockQuantity = part.Quantity,
                        ImageUrl = part.ImageUrl
                    });
                }

                SaveCartToSession(cart);
                ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
                return Json(new { success = true, message = $"Đã thêm {quantity} '{part.PartName}' vào giỏ hàng thành công!", cartCount = cart.Sum(x => x.Quantity) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST: PartOrders/UpdateCart
        [HttpPost]
        public async Task<IActionResult> UpdateCart(int partId, int quantity)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }
            if (quantity <= 0)
            {
                return Json(new { success = false, message = "Số lượng phải lớn hơn 0." });
            }

            try
            {
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{_partsApiUrl}/{partId}");
                if (part == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phụ tùng." });
                }

                if (quantity > part.Quantity)
                {
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho. Chỉ còn {part.Quantity} sản phẩm trong kho." });
                }

                var cart = GetCartFromSession();
                var item = cart.FirstOrDefault(x => x.PartId == partId);
                if (item != null)
                {
                    item.Quantity = quantity;
                    SaveCartToSession(cart);
                    ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
                }

                var subTotal = item != null ? (item.Price * item.Quantity).ToString("N0") + " đ" : "0 đ";
                var cartTotal = cart.Sum(x => x.Price * x.Quantity).ToString("N0") + " đ";

                return Json(new { success = true, subTotal, cartTotal });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PartOrders/RemoveFromCart
        [HttpPost]
        public IActionResult RemoveFromCart(int partId)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }
            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(x => x.PartId == partId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCartToSession(cart);
                ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
            }

            var cartTotal = cart.Sum(x => x.Price * x.Quantity).ToString("N0") + " đ";
            return Json(new { success = true, cartTotal, cartCount = cart.Sum(x => x.Quantity) });
        }

        // GET: PartOrders/Checkout
        [Authorize]
        public IActionResult Checkout()
        {
            var cart = GetCartFromSession();
            if (!cart.Any())
            {
                TempData["Error"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Cart));
            }

            // Pre-fill model with logged-in user claims
            var model = new PartOrderCreateViewModel
            {
                CustomerName = User.Identity?.Name ?? "",
                CustomerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? ""
            };

            return View(model);
        }

        // POST: PartOrders/Checkout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(PartOrderCreateViewModel model)
        {
            var cart = GetCartFromSession();
            if (!cart.Any())
            {
                ModelState.AddModelError("", "Giỏ hàng trống.");
                return View(model);
            }

            if (model.DeliveryMethod == "HomeDelivery" && string.IsNullOrWhiteSpace(model.ShippingAddress))
            {
                ModelState.AddModelError("ShippingAddress", "Địa chỉ giao hàng là bắt buộc khi chọn phương thức Giao hàng tận nơi.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Map Cart to PartOrder details
                var orderDetails = cart.Select(c => new
                {
                    PartId = c.PartId,
                    Quantity = c.Quantity,
                    UnitPrice = c.Price,
                    SubTotal = c.Price * c.Quantity
                }).ToList();

                decimal deliveryFee = model.DeliveryMethod switch
                {
                    "HomeDelivery" => 30000m,
                    "GarageInstallation" => 50000m,
                    _ => 0m
                };

                var payload = new
                {
                    CustomerName = model.CustomerName,
                    CustomerPhone = model.CustomerPhone,
                    CustomerEmail = model.CustomerEmail,
                    ShippingAddress = model.ShippingAddress,
                    DeliveryMethod = model.DeliveryMethod,
                    TotalAmount = cart.Sum(c => c.Price * c.Quantity) + deliveryFee,
                    Status = "Pending",
                    PartOrderDetails = orderDetails
                };

                AppendAuthorizationHeader();
                var response = await _httpClient.PostAsJsonAsync(_ordersApiUrl, payload);

                if (response.IsSuccessStatusCode)
                {
                    // Clear cart
                    HttpContext.Session.Remove("PartCart");
                    ActiveCartRegistry.ClearCart(HttpContext.Session.Id);
                    TempData["Success"] = "Đặt mua phụ tùng thành công! Đơn hàng của bạn đang chờ phê duyệt.";
                    return RedirectToAction(nameof(MyOrders));
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Có lỗi khi xử lý đơn hàng.");
                ModelState.AddModelError("", errMsg);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi kết nối máy chủ: " + ex.Message);
                return View(model);
            }
        }

        // GET: PartOrders/MyOrders
        [Authorize]
        public IActionResult MyOrders()
        {
            return RedirectToAction("Index", "Orders");
        }

        // GET: PartOrders/Manage (Admin only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            try
            {
                AppendAuthorizationHeader();
                var requestUri = $"{_ordersApiUrl}?$expand=Customer,PartOrderDetails($expand=Part)&$orderby=CreatedAt desc";
                var response = await _httpClient.GetFromJsonAsync<List<PartOrderViewModel>>(requestUri);

                return View(response ?? new List<PartOrderViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải danh sách đơn hàng quản trị: " + ex.Message;
                return View(new List<PartOrderViewModel>());
            }
        }

        // POST: PartOrders/UpdateStatus (Admin / Customer Cancel)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                AppendAuthorizationHeader();
                var payload = new { OrderId = id, Status = status };
                
                var response = await _httpClient.PutAsJsonAsync($"{_ordersApiUrl}/{id}", payload);
                if (response.IsSuccessStatusCode)
                {
                    string message = status == "Cancelled" ? "Đã hủy đơn hàng thành công!" : $"Cập nhật trạng thái đơn hàng sang '{status}' thành công!";
                    return Json(new { success = true, message });
                }

                string errMsg = await ExtractErrorMessageAsync(response, "Không thể cập nhật đơn hàng.");
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
    }
}
