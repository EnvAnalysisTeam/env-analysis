using env_analysis_project.Models;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    public class PollutionController : Controller
    {
        private readonly IPollutionWorkflowService _pollutionWorkflowService;

        public PollutionController(IPollutionWorkflowService pollutionWorkflowService)
        {
            _pollutionWorkflowService = pollutionWorkflowService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction("Index", "Home");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCsv(IFormFile? csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(ApiResponse.Fail<object?>("Vui long chon mot file CSV."));
            }

            var result = await _pollutionWorkflowService.ProcessCsvAsync(csvFile.OpenReadStream());
            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<object?>(
                    result.Message ?? "Loi xu ly file hoac du doan.",
                    result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }
    }
}
