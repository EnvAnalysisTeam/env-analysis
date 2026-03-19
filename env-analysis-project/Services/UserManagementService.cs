using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Common;
using env_analysis_project.Contracts.UserManagement;
using env_analysis_project.Models;
using env_analysis_project.Validators;
using Microsoft.AspNetCore.Identity;

namespace env_analysis_project.Services
{
    public interface IUserManagementService
    {
        Task<UserListResult> GetUsersAsync(UserListQuery query);
        Task<CsvExportResult> ExportCsvAsync(UserListQuery query);
        Task<ServiceResult<UserResponse>> CreateAsync(CreateUserRequest request, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<UserResponse>> DetailsAsync(string id);
        Task<ServiceResult<UserResponse>> UpdateAsync(UpdateUserRequest request, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<object?>> DeleteAsync(DeleteUserRequest request);
        Task<ServiceResult<object?>> RestoreAsync(RestoreUserRequest request);
    }

    public sealed class UserManagementService : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUserActivityLogger _activityLogger;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUserActivityLogger activityLogger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _activityLogger = activityLogger;
        }

        public async Task<UserListResult> GetUsersAsync(UserListQuery queryInput)
        {
            var page = Math.Max(queryInput.Page, 1);
            var pageSize = Math.Clamp(queryInput.PageSize, 5, 100);
            var normalizedStatus = NormalizeStatusFilter(queryInput.StatusFilter);

            var query = BuildUserQuery(queryInput.SearchString, queryInput.RoleFilter, queryInput.SortOption, normalizedStatus);
            var totalItems = query.Count();
            var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalItems > 0 && page > totalPages)
            {
                page = totalPages;
            }
            else if (totalItems == 0)
            {
                page = 1;
                totalPages = 1;
            }

            var users = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToList();

            var availableRoles = _roleManager.Roles
                .Select(role => role.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name)
                .ToList();

            return new UserListResult
            {
                Users = users,
                AvailableRoles = availableRoles,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = Math.Max(totalPages, 1),
                StatusFilter = normalizedStatus
            };
        }

        public Task<CsvExportResult> ExportCsvAsync(UserListQuery queryInput)
        {
            var users = BuildUserQuery(
                    queryInput.SearchString,
                    queryInput.RoleFilter,
                    queryInput.SortOption,
                    NormalizeStatusFilter(queryInput.StatusFilter))
                .Select(ToDto)
                .ToList();

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Full Name,Email,Role,Created At,Updated At");

            foreach (var user in users)
            {
                csvBuilder.Append(EscapeCsv(user.FullName));
                csvBuilder.Append(',');
                csvBuilder.Append(EscapeCsv(user.Email));
                csvBuilder.Append(',');
                csvBuilder.Append(EscapeCsv(user.Role));
                csvBuilder.Append(',');
                csvBuilder.Append(EscapeCsv(user.CreatedAt?.ToString("u")));
                csvBuilder.Append(',');
                csvBuilder.AppendLine(EscapeCsv(user.UpdatedAt?.ToString("u")));
            }

            var export = new CsvExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString()),
                ContentType = "text/csv",
                FileName = $"users-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };

            return Task.FromResult(export);
        }

        public async Task<ServiceResult<UserResponse>> CreateAsync(CreateUserRequest request, IReadOnlyCollection<string>? modelErrors = null)
        {
            if (request == null)
            {
                return ServiceResult<UserResponse>.Fail("Invalid request.", new[] { "Request payload is required." });
            }

            var validationErrors = UserValidator.Validate(ToApplicationUser(request)).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                validationErrors.Add("Password is required.");
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<UserResponse>.Fail("Validation failed.", validationErrors);
            }

            var user = new ApplicationUser
            {
                Email = request.Email?.Trim(),
                UserName = request.Email?.Trim(),
                FullName = request.FullName?.Trim(),
                Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false,
                DeletedAt = null
            };

            var createResult = await _userManager.CreateAsync(user, request.Password!);
            if (!createResult.Succeeded)
            {
                var identityErrors = createResult.Errors.Select(error => error.Description).ToList();
                return ServiceResult<UserResponse>.Fail("Failed to create user.", identityErrors);
            }

            if (!string.IsNullOrEmpty(user.Role))
            {
                await EnsureRoleAssignmentAsync(user, user.Role);
            }

            await LogActivityAsync("User.Create", user.Id, $"Created user {user.Email}", new { user.FullName, user.Role });
            return ServiceResult<UserResponse>.Ok(ToDto(user), "User created successfully.");
        }

        public async Task<ServiceResult<UserResponse>> DetailsAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return ServiceResult<UserResponse>.Fail("User identifier is required.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return ServiceResult<UserResponse>.Fail("User not found.");
            }

            return ServiceResult<UserResponse>.Ok(ToDto(user));
        }

        public async Task<ServiceResult<UserResponse>> UpdateAsync(UpdateUserRequest request, IReadOnlyCollection<string>? modelErrors = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return ServiceResult<UserResponse>.Fail("Invalid request.", new[] { "User identifier is required." });
            }

            var validationErrors = UserValidator.ValidateForUpdate(ToApplicationUser(request)).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<UserResponse>.Fail("Validation failed.", validationErrors);
            }

            var user = await _userManager.FindByIdAsync(request.Id!);
            if (user == null)
            {
                return ServiceResult<UserResponse>.Fail("User not found.");
            }
            if (user.IsDeleted)
            {
                return ServiceResult<UserResponse>.Fail("Update failed.", new[] { "Cannot update a deleted user. Please restore the user first." });
            }

            user.Email = request.Email?.Trim();
            user.UserName = request.Email?.Trim();
            user.FullName = request.FullName?.Trim();
            user.Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = updateResult.Errors.Select(error => error.Description).ToList();
                return ServiceResult<UserResponse>.Fail("Failed to update user.", errors);
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                await EnsureRoleAssignmentAsync(user, request.Role);
            }
            else
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                }
            }

            await LogActivityAsync("User.Update", user.Id, $"Updated user {user.Email}", new { user.FullName, user.Role });
            return ServiceResult<UserResponse>.Ok(ToDto(user), "User updated successfully.");
        }

        public async Task<ServiceResult<object?>> DeleteAsync(DeleteUserRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return ServiceResult<object?>.Fail("Invalid request.", new[] { "User identifier is required." });
            }

            var user = await _userManager.FindByIdAsync(request.Id);
            if (user == null)
            {
                return ServiceResult<object?>.Fail("User not found.");
            }

            if (user.IsDeleted)
            {
                return ServiceResult<object?>.Fail("Invalid request.", new[] { "User has already been deleted." });
            }

            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(error => error.Description).ToList();
                return ServiceResult<object?>.Fail("Failed to delete user.", errors);
            }

            await LogActivityAsync("User.Delete", user.Id, $"Soft deleted user {user.Email}");
            return ServiceResult<object?>.Ok(new { request.Id }, "User deleted successfully.");
        }

        public async Task<ServiceResult<object?>> RestoreAsync(RestoreUserRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Id))
            {
                return ServiceResult<object?>.Fail("Invalid request.", new[] { "User identifier is required." });
            }

            var user = await _userManager.FindByIdAsync(request.Id);
            if (user == null)
            {
                return ServiceResult<object?>.Fail("User not found.");
            }

            if (!user.IsDeleted)
            {
                return ServiceResult<object?>.Fail("Invalid request.", new[] { "User is already active." });
            }

            user.IsDeleted = false;
            user.DeletedAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(error => error.Description).ToList();
                return ServiceResult<object?>.Fail("Failed to restore user.", errors);
            }

            await LogActivityAsync("User.Restore", user.Id, $"Restored user {user.Email}");
            return ServiceResult<object?>.Ok(new { request.Id }, "User restored successfully.");
        }

        private async Task EnsureRoleAssignmentAsync(ApplicationUser user, string roleName)
        {
            var normalizedRole = roleName.Trim();
            if (!await _roleManager.RoleExistsAsync(normalizedRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(normalizedRole));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            await _userManager.AddToRoleAsync(user, normalizedRole);
        }

        private static string NormalizeStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "all";
            }

            var normalized = status.Trim().ToLowerInvariant();
            return normalized is "active" or "deleted" or "all" ? normalized : "all";
        }

        private IQueryable<ApplicationUser> BuildUserQuery(string? searchString, string? roleFilter, string? sortOption, string statusFilter)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var keyword = searchString.Trim();
                query = query.Where(user =>
                    (!string.IsNullOrEmpty(user.Email) && user.Email.Contains(keyword)) ||
                    (!string.IsNullOrEmpty(user.FullName) && user.FullName.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter))
            {
                query = query.Where(user => user.Role == roleFilter);
            }

            query = statusFilter switch
            {
                "active" => query.Where(user => !user.IsDeleted),
                "deleted" => query.Where(user => user.IsDeleted),
                _ => query
            };

            var ordered = sortOption switch
            {
                "date_asc" => query.OrderBy(user => user.CreatedAt),
                "name_asc" => query.OrderBy(user => user.FullName),
                "name_desc" => query.OrderByDescending(user => user.FullName),
                _ => query.OrderByDescending(user => user.CreatedAt)
            };

            if (statusFilter == "all")
            {
                ordered = ordered
                    .OrderBy(user => user.IsDeleted ? 1 : 0)
                    .ThenByDescending(user => user.CreatedAt);
            }

            return ordered;
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var sanitized = value.Replace("\"", "\"\"");
            return $"\"{sanitized}\"";
        }

        private Task LogActivityAsync(string actionType, string? entityId, string? description, object? metadata = null) =>
            _activityLogger.LogAsync(actionType, "User", entityId, description, metadata);

        private static ApplicationUser ToApplicationUser(CreateUserRequest request) =>
            new()
            {
                Email = request.Email,
                FullName = request.FullName,
                Role = request.Role
            };

        private static ApplicationUser ToApplicationUser(UpdateUserRequest request) =>
            new()
            {
                Id = request.Id ?? string.Empty,
                Email = request.Email,
                FullName = request.FullName,
                Role = request.Role
            };

        private static UserResponse ToDto(ApplicationUser user) =>
            new()
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role,
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsDeleted = user.IsDeleted,
                DeletedAt = user.DeletedAt
            };
    }
}
