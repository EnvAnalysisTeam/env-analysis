using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using env_analysis_project.Services;

namespace env_analysis_project.Controllers
{
    public class SourceManagementController : Controller
    {
        private readonly ISourceManagementService _sourceManagementService;

        public SourceManagementController(ISourceManagementService sourceManagementService)
        {
            _sourceManagementService = sourceManagementService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Manage));

        // Serve Manage view with emission sources model and source types (used by the sidebar)
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            var data = await _sourceManagementService.GetManageDataAsync();
            ViewBag.SourceTypes = data.SourceTypes;
            return View("Manage", data.Sources);
        }
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var exportResult = await _sourceManagementService.ExportCsvAsync();
            return File(exportResult.Bytes, exportResult.ContentType, exportResult.FileName);
        }
    }
}
