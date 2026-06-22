using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Parts;

public class DetailsModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string PartsOdataUrl = "http://localhost:5084/odata/Parts";

    public DetailsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public PartViewModel? Part { get; set; }
    public List<PartViewModel> RelatedParts { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Part = await _httpClient.GetFromJsonAsync<PartViewModel>($"{PartsOdataUrl}({id})?$expand=Category");
            if (Part == null) return NotFound();

            var relatedUri = $"{PartsOdataUrl}?$expand=Category&$filter=CategoryId eq {Part.CategoryId} and PartId ne {id} and Status ne 'Inactive'&$top=4&$orderby=CreatedAt desc";
            var relatedResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(relatedUri);
            RelatedParts = relatedResponse?.Value ?? new List<PartViewModel>();

            return Page();
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Không thể tải thông tin phụ tùng: " + ex.Message;
            return RedirectToPage("/Parts/Index");
        }
    }
}
