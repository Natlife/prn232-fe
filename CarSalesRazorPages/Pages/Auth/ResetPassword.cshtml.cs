using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Auth;

public class ResetPasswordModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "http://localhost:5084/api/Auth";

    public ResetPasswordModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string email)
    {
        Email = email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string email, string otp, string newPassword)
    {
        Email = email;
        var model = new { Email = email, Otp = otp, NewPassword = newPassword };
        var response = await _httpClient.PostAsync($"{_apiUrl}/ResetPassword",
            new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

        var responseString = await response.Content.ReadAsStringAsync();
        try
        {
            var jsonDoc = JsonDocument.Parse(responseString);
            var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Đã có lỗi xảy ra.";

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = message;
                return RedirectToPage("/Index");
            }
            ErrorMessage = message;
        }
        catch
        {
            ErrorMessage = "Lỗi từ Backend: " + responseString;
        }

        return Page();
    }
}
