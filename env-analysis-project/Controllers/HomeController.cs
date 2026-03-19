using System.Diagnostics;
using System.Threading.Tasks;
using env_analysis_project.Models;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardLookupService _dashboardLookupService;

        public HomeController(IDashboardLookupService dashboardLookupService)
        {
            _dashboardLookupService = dashboardLookupService;
        }

        public async Task<IActionResult> Index()
        {
            var lookupData = await _dashboardLookupService.GetLookupDataAsync();
            ViewBag.EmissionSources = lookupData.EmissionSources;
            ViewBag.Parameters = lookupData.Parameters;

            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
