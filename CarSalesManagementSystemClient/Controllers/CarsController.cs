using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CarSalesManagementSystemClient.Controllers
{
    public class CarsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _brandsApiUrl = "http://localhost:5084/api/CarBrands";

        public CarsController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        // GET: Cars (Showroom)
        public async Task<IActionResult> Index(CarSearchViewModel filter)
        {
            try
            {
                // Fetch Brands for the Left Filter Sidebar
                var brands = await _httpClient.GetFromJsonAsync<IEnumerable<CarBrandViewModel>>(_brandsApiUrl);
                ViewBag.Brands = brands ?? new List<CarBrandViewModel>();

                // Build OData query parameters
                var odataParams = new List<string>();
                var filters = new List<string>();

                if (filter.BrandId.HasValue) 
                    filters.Add($"BrandId eq {filter.BrandId.Value}");
                if (filter.MinPrice.HasValue) 
                    filters.Add($"Price ge {filter.MinPrice.Value}");
                if (filter.MaxPrice.HasValue) 
                    filters.Add($"Price le {filter.MaxPrice.Value}");
                if (!string.IsNullOrEmpty(filter.Transmission)) 
                    filters.Add($"Transmission eq '{filter.Transmission}'");
                if (!string.IsNullOrEmpty(filter.FuelType)) 
                    filters.Add($"FuelType eq '{filter.FuelType}'");
                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var term = Uri.EscapeDataString(filter.SearchTerm.ToLower());
                    filters.Add($"(contains(tolower(CarName), '{term}') or contains(tolower(Model), '{term}'))");
                }

                if (filters.Any())
                {
                    odataParams.Add($"$filter={string.Join(" and ", filters)}");
                }

                // Sorting mapping
                if (!string.IsNullOrEmpty(filter.SortBy))
                {
                    var sortExpr = filter.SortBy.ToLower() switch
                    {
                        "priceasc" => "Price asc",
                        "pricedesc" => "Price desc",
                        "yeardesc" => "Year desc",
                        "mileageasc" => "Mileage asc",
                        _ => "CreatedAt desc"
                    };
                    odataParams.Add($"$orderby={sortExpr}");
                }
                else
                {
                    odataParams.Add("$orderby=CreatedAt desc");
                }

                // Paging & Count & Expand
                var skip = (filter.PageNumber - 1) * filter.PageSize;
                odataParams.Add($"$skip={skip}");
                odataParams.Add($"$top={filter.PageSize}");
                odataParams.Add("$count=true");
                odataParams.Add("$expand=Brand");

                var requestUri = "http://localhost:5084/odata/Cars";
                if (odataParams.Any())
                {
                    requestUri += "?" + string.Join("&", odataParams);
                }

                // Fetch from OData API
                var odataResponse = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(requestUri);

                var pagedCars = new PagedResultViewModel<CarViewModel>
                {
                    Items = odataResponse?.Value ?? new List<CarViewModel>(),
                    TotalItems = odataResponse?.Count ?? 0,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling((double)(odataResponse?.Count ?? 0) / filter.PageSize)
                };

                ViewBag.Filter = filter;
                return View(pagedCars);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải danh sách xe: " + ex.Message;
                ViewBag.Brands = new List<CarBrandViewModel>();
                return View(new PagedResultViewModel<CarViewModel>());
            }
        }
    }

    public class ODataResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = new();

        [JsonPropertyName("@odata.count")]
        public int? Count { get; set; }
    }
}
