using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using CarSalesManagementSystemClient.Models;

namespace CarSalesManagementSystemClient.Controllers
{
    public class MaintenancePackagesController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl = "http://localhost:5084/api/MaintenancePackages";

        public MaintenancePackagesController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        // GET: MaintenancePackages
        public async Task<IActionResult> Index()
        {
            var packages = await _httpClient.GetFromJsonAsync<IEnumerable<MaintenancePackage>>(_apiUrl);
            return View(packages);
        }

        // GET: MaintenancePackages/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}/{id}");
            if (package == null) return NotFound();
            return View(package);
        }

        // GET: MaintenancePackages/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MaintenancePackages/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenancePackage package)
        {
            if (ModelState.IsValid)
            {
                var response = await _httpClient.PostAsJsonAsync(_apiUrl, package);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(package);
        }

        // GET: MaintenancePackages/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}/{id}");
            if (package == null) return NotFound();
            return View(package);
        }

        // POST: MaintenancePackages/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaintenancePackage package)
        {
            if (id != package.PackageId) return BadRequest();

            if (ModelState.IsValid)
            {
                var response = await _httpClient.PutAsJsonAsync($"{_apiUrl}/{id}", package);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }
            }
            return View(package);
        }

        // GET: MaintenancePackages/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}/{id}");
            if (package == null) return NotFound();
            return View(package);
        }

        // POST: MaintenancePackages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var response = await _httpClient.DeleteAsync($"{_apiUrl}/{id}");
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return View();
        }
    }
}
