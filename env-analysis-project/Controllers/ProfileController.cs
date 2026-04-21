using System;
using System.Threading.Tasks;
using env_analysis_project.Contracts.UserProfile;
using env_analysis_project.Models;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserProfileService _userProfileService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            IUserProfileService userProfileService,
            UserManager<ApplicationUser> userManager)
        {
            _userProfileService = userProfileService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var result = await _userProfileService.GetProfileAsync(userId);
            if (!result.Success || result.Data == null)
            {
                return RedirectToAction("Error", "Home");
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([FromBody] ChangeOwnPasswordRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(ApiResponse.Fail<object?>("User is not authenticated."));
            }

            var result = await _userProfileService.ChangePasswordAsync(
                userId,
                request ?? new ChangeOwnPasswordRequest());

            if (!result.Success)
            {
                var message = result.Message ?? "Unable to update password.";
                if (string.Equals(message, "User not found.", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(ApiResponse.Fail<object?>(message, result.Errors));
                }

                return BadRequest(ApiResponse.Fail<object?>(message, result.Errors));
            }

            return Ok(ApiResponse.Success<object?>(null, result.Message ?? "Password updated successfully."));
        }
    }
}
