using System.Text.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.PartOrders;

public class CartModel : PageModel
{
    public List<CartItemViewModel> Cart { get; set; } = new();

    public IActionResult OnGet()
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            TempData["Error"] = "Vui lòng đăng nhập để xem giỏ hàng.";
            return RedirectToPage("/Index");
        }
        var json = HttpContext.Session.GetString("PartCart");
        Cart = string.IsNullOrEmpty(json) ? new List<CartItemViewModel>() : JsonSerializer.Deserialize<List<CartItemViewModel>>(json) ?? new();
        return Page();
    }
}
