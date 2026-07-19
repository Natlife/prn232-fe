using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

        private void AttachJwtToken()
        {
            var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task<(bool IsSuccess, string Message, string? Token)> ProcessResponse(HttpResponseMessage response)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            try
            {
                var jsonDoc = JsonDocument.Parse(responseString);
                var message = jsonDoc.RootElement.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString()
                    : "Da co loi xay ra.";
                var token = jsonDoc.RootElement.TryGetProperty("token", out var tokenProp)
                    ? tokenProp.GetString()
                    : null;
                return (response.IsSuccessStatusCode, message ?? "Da co loi xay ra.", token);
            }
            catch (JsonException)
            {
                return (false, "Loi tu Backend: " + responseString, null);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Du lieu khong hop le." });

            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/Login",
                new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
            {
                var token = result.Token ?? string.Empty;

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var userId = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Name || c.Type == "unique_name" || c.Type == "name")?.Value;
                var role = jwtToken.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.Role || c.Type == "role")?.Value;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId ?? string.Empty),
                    new Claim(ClaimTypes.Email, email ?? string.Empty),
                    new Claim(ClaimTypes.Name, name ?? string.Empty),
                    new Claim(ClaimTypes.Role, role ?? "Customer"),
                    new Claim("jwt_token", token)
                };

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    ClaimTypes.Name,
                    ClaimTypes.Role);

                Response.Cookies.Append("jwt_token", token, new Microsoft.AspNetCore.Http.CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(2)
                });

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                // Sync Cart
                if (!string.IsNullOrEmpty(userId))
                {
                    var env = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) as Microsoft.AspNetCore.Hosting.IWebHostEnvironment;
                    if (env != null)
                    {
                        CarSalesManagementSystemClient.Helpers.CartHelper.SyncCartOnLogin(userId, env, HttpContext.Session);
                    }
                }

                bool isAdmin = role == "Admin";
                var redirectUrl = isAdmin ? "/Admin/Cars" : Url.Action("Index", "Home");
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    redirectUrl = returnUrl;
                }

                return Json(new { success = true, message = result.Message, redirectUrl, isAdmin });
            }

            return Json(new { success = false, message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Du lieu khong hop le." });

            var payload = new { FullName = model.FullName, Email = model.Email, Password = model.Password };
            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/Register",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            var result = await ProcessResponse(response);

            if (result.IsSuccess)
                return Json(new { success = true, message = result.Message, email = model.Email });

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
            if (!ModelState.IsValid)
                return View(model);

            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/VerifyEmail",
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
            if (!ModelState.IsValid)
                return View(model);

            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/ForgotPassword",
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
            if (!ModelState.IsValid)
                return View(model);

            var response = await _httpClient.PostAsync(
                $"{_apiUrl}/ResetPassword",
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
            Response.Cookies.Delete("jwt_token");
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                AttachJwtToken();
                var response = await _httpClient.GetAsync($"{_apiUrl}/Me");
                var content = await response.Content.ReadAsStringAsync();

                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Loi ket noi may chu: " + ex.Message });
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdatePhone([FromBody] UpdatePhoneInput model)
        {
            try
            {
                AttachJwtToken();
                var response = await _httpClient.PutAsync(
                    $"{_apiUrl}/Me/Phone",
                    new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();

                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Loi ket noi may chu: " + ex.Message });
            }
        }
    }

    public class UpdatePhoneInput
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
