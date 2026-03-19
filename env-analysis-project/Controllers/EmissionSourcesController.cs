using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using env_analysis_project.Contracts.EmissionSources;
using env_analysis_project.Models;
using env_analysis_project.Services;

namespace env_analysis_project.Controllers
{
    public class EmissionSourcesController : Controller
    {
        private readonly IEmissionSourcesService _emissionSourcesService;

        public EmissionSourcesController(IEmissionSourcesService emissionSourcesService)
        {
            _emissionSourcesService = emissionSourcesService;
        }

        // =============================
        //  LIST VIEW
        // =============================
        [HttpGet]
        public IActionResult Index() => RedirectToAction("Manage", "SourceManagement");

        // =============================
        //  DETAIL (AJAX)
        // =============================
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var result = await _emissionSourcesService.GetDetailAsync(id);
            if (!result.Success || result.Data == null)
                return NotFound(ApiResponse.Fail<EmissionSourceResponse>(result.Message ?? "Emission source not found.", result.Errors));
            return Ok(ApiResponse.Success(result.Data));
        }

        // =============================
        //  CREATE
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] EmissionSource model)
        {
            var result = await _emissionSourcesService.CreateAsync(model, !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());
            if (!result.Success || result.Data == null)
            {
                return BadRequest(ApiResponse.Fail<EmissionSourceResponse>(result.Message ?? "Validation failed.", result.Errors));
            }
            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        // =============================
        //  EDIT
        // =============================
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromForm] EmissionSource model)
        {
            var result = await _emissionSourcesService.EditAsync(id, model, !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());
            if (!result.Success)
            {
                if (string.Equals(result.Message, "Emission source not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<EmissionSourceResponse>(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse.Fail<EmissionSourceResponse>(result.Message ?? "Validation failed.", result.Errors));
            }
            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        // =============================
        //  DELETE
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] DeleteEmissionSourceRequest request)
        {
            var result = await _emissionSourcesService.DeleteAsync(request);
            if (!result.Success)
            {
                if (string.Equals(result.Message, "Emission source not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<object?>(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse.Fail<object?>(result.Message ?? "Invalid emission source identifier.", result.Errors));
            }
            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore([FromBody] DeleteEmissionSourceRequest request)
        {
            var result = await _emissionSourcesService.RestoreAsync(request);
            if (!result.Success)
            {
                if (string.Equals(result.Message, "Emission source not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<object?>(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse.Fail<object?>(result.Message ?? "Invalid emission source identifier.", result.Errors));
            }
            return Ok(ApiResponse.Success(result.Data, result.Message));
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
