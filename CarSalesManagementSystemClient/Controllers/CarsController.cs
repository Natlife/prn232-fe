using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using CarSalesManagementSystemClient.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace CarSalesManagementSystemClient.Controllers
{
    public class CarsController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _carsApiUrl = "http://localhost:5084/api/Cars";
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

                // Build query parameters for Cars API
                var queryParams = new Dictionary<string, string>();
                if (filter.BrandId.HasValue) queryParams.Add("BrandId", filter.BrandId.Value.ToString());
                if (filter.MinPrice.HasValue) queryParams.Add("MinPrice", filter.MinPrice.Value.ToString());
                if (filter.MaxPrice.HasValue) queryParams.Add("MaxPrice", filter.MaxPrice.Value.ToString());
                if (!string.IsNullOrEmpty(filter.Transmission)) queryParams.Add("Transmission", filter.Transmission);
                if (!string.IsNullOrEmpty(filter.FuelType)) queryParams.Add("FuelType", filter.FuelType);
                if (!string.IsNullOrEmpty(filter.SearchTerm)) queryParams.Add("SearchTerm", filter.SearchTerm);
                if (!string.IsNullOrEmpty(filter.SortBy)) queryParams.Add("SortBy", filter.SortBy);
                
                queryParams.Add("PageNumber", filter.PageNumber.ToString());
                queryParams.Add("PageSize", filter.PageSize.ToString());

                var requestUri = QueryHelpers.AddQueryString(_carsApiUrl, queryParams);

                // Fetch paginated cars from API
                var pagedCars = await _httpClient.GetFromJsonAsync<PagedResultViewModel<CarViewModel>>(requestUri);

                ViewBag.Filter = filter;
                return View(pagedCars ?? new PagedResultViewModel<CarViewModel>());
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Không thể tải danh sách xe: " + ex.Message;
                ViewBag.Brands = new List<CarBrandViewModel>();
                return View(new PagedResultViewModel<CarViewModel>());
            }
        }
    }
}
