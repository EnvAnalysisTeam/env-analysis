using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Parameters;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    public class ParametersController : Controller
    {
        private readonly IParametersService _parametersService;

        public ParametersController(IParametersService parametersService)
        {
            _parametersService = parametersService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Manage));

        [HttpGet]
        public IActionResult Manage() => View();

        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var export = await _parametersService.ExportCsvAsync();
            return File(export.Bytes, export.ContentType, export.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> ListData()
        {
            var parameters = await _parametersService.ListDataAsync();
            return Ok(ApiResponse.Success(parameters));
        }

        [HttpGet]
        public async Task<IActionResult> LatestMeasurementValues()
        {
            var latestMeasurements = await _parametersService.LatestMeasurementValuesAsync();
            return Ok(ApiResponse.Success(latestMeasurements));
        }

        [HttpGet]
        public async Task<IActionResult> LatestMeasurementByCode(string code, int? sourceId)
        {
            var result = await _parametersService.LatestMeasurementByCodeAsync(code, sourceId);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "No measurement data found for this parameter.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<List<ParameterMeasurementValueDto>>(result.Message ?? "No measurement data found for this parameter.", result.Errors));
                }
                return BadRequest(ApiResponse.Fail<List<ParameterMeasurementValueDto>>(result.Message ?? "Parameter code is required.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpGet]
        public async Task<IActionResult> DetailData(string id)
        {
            var result = await _parametersService.DetailDataAsync(id);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "Parameter not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<ParameterDto>(result.Message ?? "Parameter not found.", result.Errors));
                }
                return BadRequest(ApiResponse.Fail<ParameterDto>(result.Message ?? "Parameter code is required.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpPost]
        public async Task<IActionResult> CreateAjax([FromBody] ParameterDto dto)
        {
            var result = await _parametersService.CreateAjaxAsync(dto);
            if (!result.Success || result.Data == null)
            {
                if (result.Message != null && result.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(ApiResponse.Fail<ParameterDto>(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse.Fail<ParameterDto>(result.Message ?? "Validation failed.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAjax(string id, [FromBody] ParameterDto dto)
        {
            var result = await _parametersService.UpdateAjaxAsync(id, dto);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "Parameter not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<ParameterDto>(result.Message ?? "Parameter not found.", result.Errors));
                }
                return BadRequest(ApiResponse.Fail<ParameterDto>(result.Message ?? "Validation failed.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAjax(string id)
        {
            var result = await _parametersService.DeleteAjaxAsync(id);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "Parameter not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<object?>(result.Message ?? "Parameter not found.", result.Errors));
                }
                return BadRequest(ApiResponse.Fail<object?>(result.Message ?? "Invalid request.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpPost]
        public async Task<IActionResult> RestoreAjax([FromBody] ParameterDto request)
        {
            var result = await _parametersService.RestoreAjaxAsync(request);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "Parameter not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<object?>(result.Message ?? "Parameter not found.", result.Errors));
                }
                return BadRequest(ApiResponse.Fail<object?>(result.Message ?? "Invalid request.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }
    }
}
