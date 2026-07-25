using System.Net.Http.Json;
using CarSalesRazorPages.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace CarSalesRazorPages.Pages.Cars;

public class IndexModel : PageModel
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public IndexModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public PagedResultViewModel<CarViewModel> PagedCars { get; set; } = new();
    public IEnumerable<CarBrandViewModel> Brands { get; set; } = new List<CarBrandViewModel>();
    public CarSearchViewModel Filter { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CarSearchViewModel filter)
    {
        Filter = filter;
        try
        {
            var brandResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarBrandViewModel>>($"{_apiBaseUrl}/odata/CarBrands");
            Brands = brandResponse?.Value ?? new List<CarBrandViewModel>();

            var odataParams = new List<string>();
            var filters = new List<string> { "Status ne 'Inactive'" };

            if (filter.BrandId.HasValue) filters.Add($"BrandId eq {filter.BrandId.Value}");
            if (filter.MinPrice.HasValue) filters.Add($"Price ge {filter.MinPrice.Value}");
            if (filter.MaxPrice.HasValue) filters.Add($"Price le {filter.MaxPrice.Value}");
            if (!string.IsNullOrEmpty(filter.Transmission)) filters.Add($"Transmission eq '{filter.Transmission}'");
            if (!string.IsNullOrEmpty(filter.FuelType)) filters.Add($"FuelType eq '{filter.FuelType}'");
            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var term = Uri.EscapeDataString(filter.SearchTerm.ToLower());
                filters.Add($"(contains(tolower(CarName), '{term}') or contains(tolower(Model), '{term}'))");
            }

            odataParams.Add($"$filter={string.Join(" and ", filters)}");

            var sortExpr = filter.SortBy?.ToLower() switch
            {
                "priceasc" => "Price asc",
                "pricedesc" => "Price desc",
                "yeardesc" => "Year desc",
                "mileageasc" => "Mileage asc",
                _ => "CreatedAt desc"
            };
            odataParams.Add($"$orderby={sortExpr}");

            var skip = (filter.PageNumber - 1) * filter.PageSize;
            odataParams.Add($"$skip={skip}");
            odataParams.Add($"$top={filter.PageSize}");
            odataParams.Add("$count=true");
            odataParams.Add("$expand=Brand");

            var requestUri = $"{_apiBaseUrl}/odata/Cars?" + string.Join("&", odataParams);
            var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(requestUri);

            PagedCars = new PagedResultViewModel<CarViewModel>
            {
                Items = odataResponse?.Value ?? new List<CarViewModel>(),
                TotalItems = odataResponse?.Count ?? 0,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                TotalPages = (int)Math.Ceiling((double)(odataResponse?.Count ?? 0) / filter.PageSize)
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể tải danh sách xe: " + ex.Message;
            Brands = new List<CarBrandViewModel>();
        }
    }
}
