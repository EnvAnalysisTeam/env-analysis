using System;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.UserProfile;
using env_analysis_project.Models;
using Microsoft.AspNetCore.Identity;

namespace env_analysis_project.Services
{
    public interface IUserProfileService
    {
        Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId);
        Task<ServiceResult<object?>> ChangePasswordAsync(string userId, ChangeOwnPasswordRequest request);
    }

    public sealed class UserProfileService : IUserProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUserActivityLogger _activityLogger;

        public UserProfileService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserActivityLogger activityLogger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _activityLogger = activityLogger;
        }

        public async Task<ServiceResult<UserProfileResponse>> GetProfileAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return ServiceResult<UserProfileResponse>.Fail("User identifier is required.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<UserProfileResponse>.Fail("User not found.");
            }

            return ServiceResult<UserProfileResponse>.Ok(new UserProfileResponse
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
        }

        public async Task<ServiceResult<object?>> ChangePasswordAsync(string userId, ChangeOwnPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return ServiceResult<object?>.Fail("User identifier is required.");
            }

            if (request == null)
            {
                return ServiceResult<object?>.Fail("Invalid request.", new[] { "Request payload is required." });
            }

            var validationErrors = ValidateChangePasswordRequest(request).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<object?>.Fail("Validation failed.", validationErrors);
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return ServiceResult<object?>.Fail("User not found.");
            }

            if (user.IsDeleted)
            {
                return ServiceResult<object?>.Fail("Cannot update password for a deleted user.");
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword!.Trim());
            if (!resetResult.Succeeded)
            {
                var errors = resetResult.Errors.Select(error => error.Description);
                return ServiceResult<object?>.Fail("Failed to update password.", errors);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);
            await _activityLogger.LogAsync(
                "User.ChangeOwnPassword",
                "User",
                user.Id,
                $"User {user.Email ?? user.Id} changed own password.",
                null,
                user.Id);

            return ServiceResult<object?>.Ok(null, "Password updated successfully.");
        }

        private static string[] ValidateChangePasswordRequest(ChangeOwnPasswordRequest request)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                errors.Add("New password is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                errors.Add("Confirm password is required.");
            }

            if (!string.IsNullOrWhiteSpace(request.NewPassword) &&
                !string.IsNullOrWhiteSpace(request.ConfirmPassword) &&
                !string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
            {
                errors.Add("Confirm password does not match the new password.");
            }

            return errors.ToArray();
        }
    }
}
