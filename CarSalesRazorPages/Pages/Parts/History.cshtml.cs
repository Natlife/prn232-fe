using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Parts
{
    [Authorize(Roles = "Admin")]
    public class HistoryModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private const string PartsApiUrl = "http://localhost:5084/api/Parts";
        private const string CategoriesApiUrl = "http://localhost:5084/api/PartCategories";

        public HistoryModel(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public PartViewModel Part { get; set; } = new();
        public List<InventoryTransactionViewModel> Transactions { get; set; } = new();
        public string? ErrorMessage { get; set; }

        private void AppendAuthorizationHeader()
        {
            var token = User.FindFirst("jwt_token")?.Value;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                // Fetch Part Details
                var part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsApiUrl}/{id}");
                if (part == null)
                {
                    return NotFound();
                }
                Part = part;

                // Fetch Categories
                try
                {
                    var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(CategoriesApiUrl);
                    if (categories != null && Part.CategoryId > 0)
                    {
                        Part.Category = categories.FirstOrDefault(c => c.CategoryId == Part.CategoryId);
                    }
                }
                catch { }

                // Fetch Transactions
                AppendAuthorizationHeader();
                var transactionsResponse = await _httpClient.GetAsync($"http://localhost:5084/api/Inventory/transactions/{id}");
                if (transactionsResponse.IsSuccessStatusCode)
                {
                    Transactions = await transactionsResponse.Content.ReadFromJsonAsync<List<InventoryTransactionViewModel>>() 
                                   ?? new List<InventoryTransactionViewModel>();
                }

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi khi tải lịch sử phụ tùng: " + ex.Message;
                return Page();
            }
        }
    }
}
