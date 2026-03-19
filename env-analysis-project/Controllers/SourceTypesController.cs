using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.SourceTypes;
using env_analysis_project.Models;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    public class SourceTypesController : Controller
    {
        private readonly ISourceTypesService _sourceTypesService;

        public SourceTypesController(ISourceTypesService sourceTypesService)
        {
            _sourceTypesService = sourceTypesService;
        }

        [HttpGet]
        public IActionResult Index() => RedirectToAction("Manage", "SourceManagement");

        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var sourceTypes = await _sourceTypesService.GetActiveListAsync();
            return Ok(ApiResponse.Success(sourceTypes));
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var result = await _sourceTypesService.GetByIdAsync(id);
            if (!result.Success || result.Data == null)
            {
                return NotFound(ApiResponse.Fail<SourceTypeDto>(result.Message ?? "Source type not found.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SourceType sourceType)
        {
            var result = await _sourceTypesService.CreateAsync(
                sourceType,
                !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());

            if (!result.Success || result.Data == null)
            {
                if (IsAjaxRequest())
                {
                    return BadRequest(ApiResponse.Fail<SourceTypeDto>(result.Message ?? "Validation failed.", result.Errors));
                }

                TempData["Error"] = string.Join(Environment.NewLine, result.Errors ?? Array.Empty<string>());
                return RedirectToAction("Manage", "SourceManagement");
            }

            if (IsAjaxRequest())
            {
                return Ok(ApiResponse.Success(result.Data, result.Message));
            }

            TempData["Success"] = "Source type created successfully!";
            return RedirectToAction("Manage", "SourceManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SourceTypeID,SourceTypeName,Description,IsActive,CreatedAt,UpdatedAt")] SourceType model)
        {
            var result = await _sourceTypesService.EditAsync(
                id,
                model,
                !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());

            if (!result.Success)
            {
                if (string.Equals(result.Message, "Source type not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<SourceTypeDto>(result.Message, result.Errors));
                }
                return BadRequest(ApiResponse.Fail<SourceTypeDto>(result.Message ?? "Validation failed.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data, result.Message));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _sourceTypesService.DeleteAsync(id);

            if (IsAjaxRequest())
            {
                return Ok(ApiResponse.Success<object?>(null, result.Message ?? "Source type deleted successfully."));
            }

            return RedirectToAction("Manage", "SourceManagement");
        }

        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

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
