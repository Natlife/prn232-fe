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
        private readonly string _apiUrl = "http://localhost:5084/odata/MaintenancePackages";

        public MaintenancePackagesController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        // GET: MaintenancePackages
        public async Task<IActionResult> Index()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ODataResponse<MaintenancePackage>>(_apiUrl);
                var packages = response?.Value ?? new List<MaintenancePackage>();
                return View(packages);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách gói bảo dưỡng: " + ex.Message;
                return View(new List<MaintenancePackage>());
            }
        }

        // GET: MaintenancePackages/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}({id})");
                if (package == null) return NotFound();
                return View(package);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Không tìm thấy gói bảo dưỡng: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
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
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(_apiUrl, package);
                    if (response.IsSuccessStatusCode)
                    {
                        return RedirectToAction(nameof(Index));
                    }
                    TempData["ErrorMessage"] = "Thêm gói thất bại: " + await response.Content.ReadAsStringAsync();
                }
                catch (System.Exception ex)
                {
                    TempData["ErrorMessage"] = "Lỗi kết nối: " + ex.Message;
                }
            }
            return View(package);
        }

        // GET: MaintenancePackages/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}({id})");
                if (package == null) return NotFound();
                return View(package);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: MaintenancePackages/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaintenancePackage package)
        {
            if (id != package.PackageId) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var response = await _httpClient.PutAsJsonAsync($"{_apiUrl}({id})", package);
                    if (response.IsSuccessStatusCode)
                    {
                        return RedirectToAction(nameof(Index));
                    }
                    TempData["ErrorMessage"] = "Cập nhật thất bại: " + await response.Content.ReadAsStringAsync();
                }
                catch (System.Exception ex)
                {
                    TempData["ErrorMessage"] = "Lỗi kết nối: " + ex.Message;
                }
            }
            return View(package);
        }

        // GET: MaintenancePackages/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var package = await _httpClient.GetFromJsonAsync<MaintenancePackage>($"{_apiUrl}({id})");
                if (package == null) return NotFound();
                return View(package);
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: MaintenancePackages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_apiUrl}({id})");
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }
                TempData["ErrorMessage"] = "Xóa thất bại: " + await response.Content.ReadAsStringAsync();
            }
            catch (System.Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi kết nối: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
