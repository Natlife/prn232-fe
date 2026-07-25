using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "http://localhost:5084/api/Auth";

    public ForgotPasswordModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string email)
    {
        var model = new { Email = email };
        var response = await _httpClient.PostAsync($"{_apiUrl}/ForgotPassword",
            new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

        var responseString = await response.Content.ReadAsStringAsync();
        try
        {
            var jsonDoc = JsonDocument.Parse(responseString);
            var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Đã có lỗi xảy ra.";

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = message;
                return RedirectToPage("/Auth/ResetPassword", new { email });
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
