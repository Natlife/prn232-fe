using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using CarSalesManagementSystemClient.Models;

namespace CarSalesManagementSystemClient.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;

    public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5084").TrimEnd('/');
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))
        {
            return Redirect("/Admin/Cars");
        }

        try
        {
            // Fetch top 6 newest cars that are active
            var response = await _httpClient.GetFromJsonAsync<ODataResponse<CarViewModel>>(
                $"{_apiBaseUrl}/odata/Cars?$top=6&$orderby=CarId desc&$filter=Status ne 'Inactive'&$expand=Brand");
            var featuredCars = response?.Value ?? new List<CarViewModel>();
            return View(featuredCars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching featured cars for home page.");
            return View(new List<CarViewModel>());
        }
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Warranty()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
