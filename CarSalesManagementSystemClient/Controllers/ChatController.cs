using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CarSalesManagementSystemClient.Controllers;

public class ChatController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public ChatController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    private void AttachJwtToken()
    {
        var token = Request.Cookies["jwt_token"] ?? User.FindFirst("jwt_token")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Message([FromBody] ChatRequestInput model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Message))
        {
            return BadRequest(new { message = "Tin nhắn không được trống." });
        }

        try
        {
            AttachJwtToken();
            var payload = new
            {
                sessionId = model.SessionId,
                message = model.Message
            };

            var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/api/Chat/message", payload);
            var content = await response.Content.ReadAsStringAsync();

            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> History(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return BadRequest(new { message = "SessionId không được trống." });
        }

        try
        {
            AttachJwtToken();
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/Chat/history/{sessionId}");
            var content = await response.Content.ReadAsStringAsync();

            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Lỗi kết nối máy chủ: " + ex.Message });
        }
    }
}

public class ChatRequestInput
{
    public string SessionId { get; set; } = null!;
    public string Message { get; set; } = null!;
}
