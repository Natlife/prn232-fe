using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarSalesRazorPages.Pages.Parts;

public class IndexModel : PageModel
{
    private readonly HttpClient _httpClient;
    private const string PartsApiUrl = "http://localhost:5084/api/Parts";
    private const string CategoriesApiUrl = "http://localhost:5084/api/PartCategories";

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public PagedResultViewModel<PartViewModel> PagedParts { get; set; } = new();
    public IEnumerable<PartCategoryViewModel> Categories { get; set; } = new List<PartCategoryViewModel>();
    public PartSearchViewModel Filter { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(PartSearchViewModel filter)
    {
        Filter = filter;
        try
        {
            var categories = await _httpClient.GetFromJsonAsync<IEnumerable<PartCategoryViewModel>>(CategoriesApiUrl);
            Categories = categories ?? new List<PartCategoryViewModel>();

            var odataParams = new List<string>();
            var filters = new List<string> { "Status ne 'Inactive'" };

            if (filter.CategoryId.HasValue) filters.Add($"CategoryId eq {filter.CategoryId.Value}");
            if (filter.MinPrice.HasValue) filters.Add($"Price ge {filter.MinPrice.Value}");
            if (filter.MaxPrice.HasValue) filters.Add($"Price le {filter.MaxPrice.Value}");
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var term = Uri.EscapeDataString(filter.SearchTerm.ToLower());
                filters.Add($"(contains(tolower(PartName), '{term}') or contains(tolower(PartCode), '{term}') or contains(tolower(Brand), '{term}'))");
            }

            odataParams.Add($"$filter={string.Join(" and ", filters)}");

            var sortExpr = filter.SortBy?.ToLower() switch
            {
                "priceasc" => "Price asc",
                "pricedesc" => "Price desc",
                "nameasc" => "PartName asc",
                _ => "CreatedAt desc"
            };
            odataParams.Add($"$orderby={sortExpr}");

            var skip = (filter.PageNumber - 1) * filter.PageSize;
            odataParams.Add($"$skip={skip}");
            odataParams.Add($"$top={filter.PageSize}");
            odataParams.Add("$count=true");
            odataParams.Add("$expand=Category");

            var requestUri = PartsApiUrl + "?" + string.Join("&", odataParams);
            var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<PartViewModel>>(requestUri);

            PagedParts = new PagedResultViewModel<PartViewModel>
            {
                Items = odataResponse?.Value ?? new List<PartViewModel>(),
                TotalItems = odataResponse?.Count ?? 0,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling((double)(odataResponse?.Count ?? 0) / filter.PageSize)
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể tải danh sách phụ tùng: " + ex.Message;
            Categories = new List<PartCategoryViewModel>();
        }
    }
}
