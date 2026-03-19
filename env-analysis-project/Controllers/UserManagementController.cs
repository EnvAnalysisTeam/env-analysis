using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.UserManagement;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly IUserManagementService _userManagementService;

        public UserManagementController(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
        }

        public async Task<IActionResult> Index(string? searchString, string? roleFilter, string? sortOption, string? statusFilter, int page = 1, int pageSize = 10)
        {
            var result = await _userManagementService.GetUsersAsync(new UserListQuery
            {
                SearchString = searchString,
                RoleFilter = roleFilter,
                SortOption = sortOption,
                StatusFilter = statusFilter,
                Page = page,
                PageSize = pageSize
            });

            ViewBag.AvailableRoles = result.AvailableRoles;
            ViewBag.Page = result.Page;
            ViewBag.PageSize = result.PageSize;
            ViewBag.TotalItems = result.TotalItems;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.StatusFilter = result.StatusFilter;

            return View("Manage", result.Users);
        }

        [HttpGet]
        public async Task<IActionResult> Export(string? searchString, string? roleFilter, string? sortOption, string? statusFilter)
        {
            var export = await _userManagementService.ExportCsvAsync(new UserListQuery
            {
                SearchString = searchString,
                RoleFilter = roleFilter,
                SortOption = sortOption,
                StatusFilter = statusFilter
            });

            return File(export.Bytes, export.ContentType, export.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var result = await _userManagementService.CreateAsync(request, !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());
            if (!result.Success || result.Data == null)
            {
                return HandleFailure(result.Errors ?? new[] { "Unknown error." }, result.Message ?? "Invalid request.");
            }

            return HandleSuccess(result.Data, result.Message ?? "User created successfully.");
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var result = await _userManagementService.DetailsAsync(id);
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "User not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<UserResponse>(result.Message, result.Errors));
                }

                return BadRequest(ApiResponse.Fail<UserResponse>(result.Message ?? "User identifier is required.", result.Errors));
            }

            return Ok(ApiResponse.Success(result.Data));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] UpdateUserRequest request)
        {
            var result = await _userManagementService.UpdateAsync(request, !ModelState.IsValid ? GetModelErrors() : Array.Empty<string>());
            if (!result.Success || result.Data == null)
            {
                if (string.Equals(result.Message, "User not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleNotFound();
                }
                return HandleFailure(result.Errors ?? new[] { "Unknown error." }, result.Message ?? "Update failed.");
            }

            return HandleSuccess(result.Data, result.Message ?? "User updated successfully.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromBody] DeleteUserRequest request)
        {
            var result = await _userManagementService.DeleteAsync(request);
            if (!result.Success)
            {
                if (string.Equals(result.Message, "User not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleNotFound();
                }
                return HandleFailure(result.Errors ?? new[] { "Unknown error." }, result.Message ?? "Delete failed.");
            }

            return HandleSuccess(result.Data, result.Message ?? "User deleted successfully.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore([FromBody] RestoreUserRequest request)
        {
            var result = await _userManagementService.RestoreAsync(request);
            if (!result.Success)
            {
                if (string.Equals(result.Message, "User not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return HandleNotFound();
                }
                return HandleFailure(result.Errors ?? new[] { "Unknown error." }, result.Message ?? "Restore failed.");
            }

            return HandleSuccess(result.Data, result.Message ?? "User restored successfully.");
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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

        private IActionResult HandleSuccess<T>(T? payload, string message)
        {
            if (IsAjaxRequest())
            {
                return Ok(ApiResponse.Success(payload, message));
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        private IActionResult HandleFailure(IEnumerable<string> errors, string message)
        {
            var errorList = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
            if (IsAjaxRequest())
            {
                return BadRequest(ApiResponse.Fail<object?>(message, errorList));
            }

            TempData["Error"] = string.Join(Environment.NewLine, errorList);
            return RedirectToAction(nameof(Index));
        }

        private IActionResult HandleNotFound()
        {
            if (IsAjaxRequest())
            {
                return NotFound(ApiResponse.Fail<object?>("User not found."));
            }

            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }
    }
}
