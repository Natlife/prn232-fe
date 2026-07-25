using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CarSalesRazorPages.Models;

namespace CarSalesRazorPages.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "http://localhost:5084/api/Auth";

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public IActionResult OnGet()
    {
        if (User.Identity!.IsAuthenticated)
            return RedirectToPage("/Index");
        return Page();
    }

    private async Task<(bool IsSuccess, string Message, string? Token)> ProcessResponse(HttpResponseMessage response)
    {
        var responseString = await response.Content.ReadAsStringAsync();
        try
        {
            var jsonDoc = JsonDocument.Parse(responseString);
            var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Đã có lỗi xảy ra.";
            var token = jsonDoc.RootElement.TryGetProperty("token", out var tokenProp) ? tokenProp.GetString() : null;
            return (response.IsSuccessStatusCode, message ?? "Đã có lỗi xảy ra.", token);
        }
        catch (JsonException)
        {
            return (false, "Lỗi từ Backend: " + responseString, null);
        }
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        var email = Request.Form["Email"].ToString();
        var password = Request.Form["Password"].ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." });

        var payload = new { Email = email, Password = password };
        var response = await _httpClient.PostAsync($"{_apiUrl}/Login",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var result = await ProcessResponse(response);

        if (result.IsSuccess)
        {
            var token = result.Token ?? string.Empty;
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")?.Value;
            var email2 = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
            var name = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name" || c.Type == "name")?.Value;
            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;

            var claimsIdentity = new ClaimsIdentity(
                jwtToken.Claims,
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role
            );
            claimsIdentity.AddClaim(new Claim("jwt_token", token));

            Response.Cookies.Append("jwt_token", token, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            bool isAdmin = role == "Admin";
            var redirectUrl = isAdmin ? "/Admin/Cars" : "/";

            return new JsonResult(new { success = true, message = result.Message, redirectUrl, isAdmin });
        }

        return new JsonResult(new { success = false, message = result.Message });
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        var fullName = Request.Form["FullName"].ToString();
        var email = Request.Form["Email"].ToString();
        var password = Request.Form["Password"].ToString();

        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            return new JsonResult(new { success = false, message = "Dữ liệu không hợp lệ." });

        var payload = new { FullName = fullName, Email = email, Password = password };
        var response = await _httpClient.PostAsync($"{_apiUrl}/Register",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var result = await ProcessResponse(response);

        if (result.IsSuccess)
            return new JsonResult(new { success = true, message = result.Message, email });

        return new JsonResult(new { success = false, message = result.Message });
    }
}
