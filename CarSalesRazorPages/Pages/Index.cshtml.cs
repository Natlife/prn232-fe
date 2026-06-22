using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity!.IsAuthenticated && User.IsInRole("Admin"))
        {
            return Redirect("/Admin/Cars");
        }
        return Page();
    }
}
