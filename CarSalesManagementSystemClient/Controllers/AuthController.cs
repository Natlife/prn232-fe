using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CarSalesManagementSystemClient.Controllers
{
    public class AuthController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api/Auth";

        public AuthController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public IActionResult Login() => View();

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
                // If backend returns plain text/HTML error (like a 500 Server Error stack trace)
                return (false, "Lỗi từ Backend: " + responseString, null);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var response = await _httpClient.PostAsync($"{_apiUrl}/Login",
                new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                var token = result.Token ?? string.Empty;
                
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Trích xuất các giá trị từ JWT claim một cách an toàn
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name" || c.Type == "name")?.Value;
                var role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId ?? ""),
                    new Claim(ClaimTypes.Email, email ?? ""),
                    new Claim(ClaimTypes.Name, name ?? ""),
                    new Claim(ClaimTypes.Role, role ?? "Customer"),
                    new Claim("jwt_token", token)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                bool isAdmin = role == "Admin";
                var redirectUrl = isAdmin ? Url.Action("Manage", "Parts") : Url.Action("Index", "Home");

                return Json(new { success = true, message = result.Message, redirectUrl = redirectUrl, isAdmin = isAdmin });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            var payload = new { FullName = model.FullName, Email = model.Email, Password = model.Password };
            var response = await _httpClient.PostAsync($"{_apiUrl}/Register",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                return Json(new { success = true, message = result.Message, email = model.Email });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            return View(new VerifyViewModel { Email = email });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyEmail(VerifyViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var response = await _httpClient.PostAsync($"{_apiUrl}/VerifyEmail",
                new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = result.Message;
            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var response = await _httpClient.PostAsync($"{_apiUrl}/ForgotPassword",
                new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction("ResetPassword", new { email = model.Email });
            }

            ViewBag.Error = result.Message;
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            return View(new ResetPasswordViewModel { Email = email });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var response = await _httpClient.PostAsync($"{_apiUrl}/ResetPassword",
                new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = result.Message;
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
