using Microsoft.AspNetCore.Mvc;
using env_analysis_project.Services;

namespace env_analysis_project.Controllers
{
    public class AlertsForecastsController : Controller
    {
        private readonly IDashboardLookupService _dashboardLookupService;

        public AlertsForecastsController(IDashboardLookupService dashboardLookupService)
        {
            _dashboardLookupService = dashboardLookupService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Alerts & Forecasts";
            var lookupData = await _dashboardLookupService.GetLookupDataAsync();
            ViewBag.EmissionSources = lookupData.EmissionSources;
            ViewBag.Parameters = lookupData.Parameters;

            return View();
        }
    }
}
