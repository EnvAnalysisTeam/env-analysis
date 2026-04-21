using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.MeasurementResults;
using env_analysis_project.Models;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    public class MeasurementResultsController : Controller
    {
        private readonly IMeasurementImportService _measurementImportService;
        private readonly IMeasurementResultsService _measurementResultsService;

        public MeasurementResultsController(
            IMeasurementImportService measurementImportService,
            IMeasurementResultsService measurementResultsService)
        {
            _measurementImportService = measurementImportService;
            _measurementResultsService = measurementResultsService;
        }

        public async Task<IActionResult> Manage()
        {
            var data = await _measurementResultsService.GetManageDataAsync();
            ViewBag.EmissionSources = data.EmissionSources;
            ViewBag.Parameters = data.Parameters;
            return View("Manage");
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Manage));

        [HttpGet]
        public async Task<IActionResult> ListData(
            string? type,
            int page = 1,
            int pageSize = 10,
            bool paged = false,
            string? search = null,
            int? sourceId = null,
            string? parameterCode = null,
            string? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var response = await _measurementResultsService.ListDataAsync(new MeasurementResultListQuery
            {
                Type = type,
                Page = page,
                PageSize = pageSize,
                Paged = paged,
                Search = search,
                SourceId = sourceId,
                ParameterCode = parameterCode,
                Status = status,
                StartDate = startDate,
                EndDate = endDate
            });

            return Ok(ApiResponse.Success(response));
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? type,
            string? search = null,
            int? sourceId = null,
            string? parameterCode = null,
            string? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var export = await _measurementResultsService.ExportCsvAsync(new MeasurementResultListQuery
            {
                Type = type,
                Search = search,
                SourceId = sourceId,
                ParameterCode = parameterCode,
                Status = status,
                StartDate = startDate,
                EndDate = endDate
            });

            return File(export.Bytes, export.ContentType, export.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPreview(IFormFile? file, [FromForm] int? emissionSourceId)
        {
            var result = await _measurementImportService.PreviewAsync(file, emissionSourceId);
            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<MeasurementImportPreviewResponse>(
                    result.Message ?? "Unable to preview the import file.",
                    result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportConfirm([FromBody] MeasurementImportConfirmRequest request)
        {
            var result = await _measurementImportService.ConfirmAsync(request);
            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<MeasurementImportConfirmResponse>(
                    result.Message ?? "Unable to import measurement results.",
                    result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpGet]
        public async Task<IActionResult> ParameterTrends(
            [FromQuery] string? code,
            [FromQuery(Name = "codes")] string[]? codes,
            [FromQuery] string? startMonth,
            [FromQuery] string? endMonth,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] int? sourceId = null)
        {
            var result = await _measurementResultsService.GetParameterTrendsAsync(new ParameterTrendsQuery
            {
                Code = code,
                Codes = codes,
                StartMonth = startMonth,
                EndMonth = endMonth,
                SourceId = sourceId
            });

            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<ParameterTrendResponse>(result.Message ?? "Unable to load parameter trends.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpGet]
        public async Task<IActionResult> ParameterTrendPredictions(
            [FromQuery] string? code,
            [FromQuery(Name = "codes")] string[]? codes,
            [FromQuery] string? startMonth,
            [FromQuery] string? endMonth,
            [FromQuery] int? sourceId = null)
        {
            var result = await _measurementResultsService.GetParameterTrendPredictionsAsync(new ParameterTrendsQuery
            {
                Code = code,
                Codes = codes,
                StartMonth = startMonth,
                EndMonth = endMonth,
                SourceId = sourceId
            });

            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<ParameterTrendResponse>(result.Message ?? "Unable to load parameter predictions.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpGet]
        public async Task<IActionResult> DetailData(int id)
        {
            var result = await _measurementResultsService.GetDetailAsync(id);
            if (!result.Success || result.Data == null)
            {
                return NotFound(ApiResponse.Fail<MeasurementResultDto>(result.Message ?? "Measurement result not found.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAjax([FromBody] MeasurementResultRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse.Fail<MeasurementResultDto>("Invalid measurement result payload."));
            }

            var result = await _measurementResultsService.CreateAsync(
                request,
                !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());

            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<MeasurementResultDto>(
                    result.Message ?? "Invalid measurement result payload.",
                    result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpPut]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAjax(int id, [FromBody] MeasurementResultRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse.Fail<MeasurementResultDto>("Invalid measurement result payload."));
            }

            var result = await _measurementResultsService.UpdateAsync(
                id,
                request,
                !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());

            if (!result.Success)
            {
                if (string.Equals(result.Message, "Measurement result not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<MeasurementResultDto>(result.Message ?? "Measurement result not found.", result.Errors));
                }

                return BadRequest(ApiResponse.Fail<MeasurementResultDto>(
                    result.Message ?? "Invalid measurement result payload.",
                    result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var result = await _measurementResultsService.DeleteAsync(id);
            if (!result.Success)
            {
                return NotFound(ApiResponse.Fail<object?>(result.Message ?? "Measurement result not found.", result.Errors));
            }

            return Ok(ApiResponse.Success<object?>(null, result.Message));
        }

        private IReadOnlyCollection<string> GetModelErrors()
        {
            return ModelState
                .Where(entry => entry.Value?.Errors?.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? $"Invalid value for {entry.Key}"
                        : error.ErrorMessage))
                .ToArray();
        }
    }
}
