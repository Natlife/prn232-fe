using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.PartOrders;

public class CheckoutModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string OrdersApiUrl = "http://localhost:5084/api/PartOrders";
    private const string PartsApiUrl = "http://localhost:5084/api/Parts";

    public CheckoutModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [BindProperty]
    public PartOrderCreateViewModel OrderModel { get; set; } = new();

    public List<CartItemViewModel> Cart { get; set; } = new();

    private List<CartItemViewModel> GetCartFromSession()
    {
        var json = HttpContext.Session.GetString("PartCart");
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<CartItemViewModel>>(json) ?? new();
    }

    private void AppendAuthorizationHeader()
    {
        var token = User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Authorize]
    public IActionResult OnGet()
    {
        Cart = GetCartFromSession();
        if (!Cart.Any())
        {
            TempData["Error"] = "Giỏ hàng của bạn đang trống.";
            return RedirectToPage("/PartOrders/Cart");
        }
        OrderModel = new PartOrderCreateViewModel
        {
            CustomerName = User.Identity?.Name ?? "",
            CustomerEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? ""
        };
        return Page();
    }

    [Authorize]
    public async Task<IActionResult> OnPostAsync()
    {
        Cart = GetCartFromSession();
        if (!Cart.Any()) { ModelState.AddModelError("", "Giỏ hàng trống."); return Page(); }

        if (OrderModel.DeliveryMethod == "HomeDelivery" && string.IsNullOrWhiteSpace(OrderModel.ShippingAddress))
            ModelState.AddModelError("OrderModel.ShippingAddress", "Địa chỉ giao hàng là bắt buộc khi chọn phương thức Giao hàng tận nơi.");

        if (!ModelState.IsValid) return Page();

        try
        {
            decimal deliveryFee = OrderModel.DeliveryMethod switch
            {
                "HomeDelivery" => 30000m,
                "GarageInstallation" => 50000m,
                _ => 0m
            };

            var payload = new
            {
                CustomerName = OrderModel.CustomerName,
                CustomerPhone = OrderModel.CustomerPhone,
                CustomerEmail = OrderModel.CustomerEmail,
                ShippingAddress = OrderModel.ShippingAddress,
                DeliveryMethod = OrderModel.DeliveryMethod,
                TotalAmount = Cart.Sum(c => c.Price * c.Quantity) + deliveryFee,
                Status = "Pending",
                PartOrderDetails = Cart.Select(c => new { PartId = c.PartId, Quantity = c.Quantity, UnitPrice = c.Price, SubTotal = c.Price * c.Quantity }).ToList()
            };

            AppendAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(OrdersApiUrl, payload);

            if (response.IsSuccessStatusCode)
            {
                HttpContext.Session.Remove("PartCart");
                ActiveCartRegistry.ClearCart(HttpContext.Session.Id);
                TempData["Success"] = "Đặt mua phụ tùng thành công! Đơn hàng của bạn đang chờ phê duyệt.";
                return RedirectToPage("/PartOrders/MyOrders");
            }

            ModelState.AddModelError("", "Có lỗi khi xử lý đơn hàng.");
            return Page();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Lỗi kết nối máy chủ: " + ex.Message);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int partId, int quantity = 1)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
            return new JsonResult(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });

        try
        {
            var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{partId}");
            if (part == null) return new JsonResult(new { success = false, message = "Không tìm thấy phụ tùng yêu cầu." });
            if (part.Quantity <= 0 || part.Status == "Out of Stock") return new JsonResult(new { success = false, message = "Sản phẩm đã hết hàng." });

            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(x => x.PartId == partId);
            int currentQtyInCart = item?.Quantity ?? 0;

            if (currentQtyInCart + quantity > part.Quantity)
                return new JsonResult(new { success = false, message = $"Số lượng yêu cầu vượt quá tồn kho. Hiện kho chỉ còn {part.Quantity} sản phẩm (Giỏ hàng đã có {currentQtyInCart})." });

            if (item != null) item.Quantity += quantity;
            else cart.Add(new CartItemViewModel { PartId = part.PartId, PartName = part.PartName, PartCode = part.PartCode, Price = part.Price, Quantity = quantity, StockQuantity = part.Quantity, ImageUrl = part.ImageUrl });

            HttpContext.Session.SetString("PartCart", JsonSerializer.Serialize(cart));
            ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
            return new JsonResult(new { success = true, message = $"Đã thêm {quantity} '{part.PartName}' vào giỏ hàng thành công!", cartCount = cart.Sum(x => x.Quantity) });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = "Lỗi: " + ex.Message }); }
    }

    public async Task<IActionResult> OnPostUpdateCartAsync(int partId, int quantity)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated) return new JsonResult(new { success = false, message = "Vui lòng đăng nhập." });
        if (quantity <= 0) return new JsonResult(new { success = false, message = "Số lượng phải lớn hơn 0." });

        try
        {
            var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{partId}");
            if (part == null) return new JsonResult(new { success = false, message = "Không tìm thấy phụ tùng." });
            if (quantity > part.Quantity) return new JsonResult(new { success = false, message = $"Số lượng vượt quá tồn kho. Chỉ còn {part.Quantity} sản phẩm trong kho." });

            var cart = GetCartFromSession();
            var item = cart.FirstOrDefault(x => x.PartId == partId);
            if (item != null)
            {
                item.Quantity = quantity;
                HttpContext.Session.SetString("PartCart", JsonSerializer.Serialize(cart));
                ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
            }

            var subTotal = item != null ? (item.Price * item.Quantity).ToString("N0") + " đ" : "0 đ";
            var cartTotal = cart.Sum(x => x.Price * x.Quantity).ToString("N0") + " đ";
            return new JsonResult(new { success = true, subTotal, cartTotal });
        }
        catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }

    public IActionResult OnPostRemoveFromCartAsync(int partId)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated) return new JsonResult(new { success = false, message = "Vui lòng đăng nhập." });

        var cart = GetCartFromSession();
        var item = cart.FirstOrDefault(x => x.PartId == partId);
        if (item != null)
        {
            cart.Remove(item);
            HttpContext.Session.SetString("PartCart", JsonSerializer.Serialize(cart));
            ActiveCartRegistry.UpdateCart(HttpContext.Session.Id, cart.Select(c => c.PartId));
        }

        var cartTotal = cart.Sum(x => x.Price * x.Quantity).ToString("N0") + " đ";
        return new JsonResult(new { success = true, cartTotal, cartCount = cart.Sum(x => x.Quantity) });
    }
}
