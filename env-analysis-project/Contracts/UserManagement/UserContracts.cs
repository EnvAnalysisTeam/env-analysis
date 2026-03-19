using System;
using System.Collections.Generic;

namespace env_analysis_project.Contracts.UserManagement
{
    public sealed class UserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public sealed class CreateUserRequest
    {
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public sealed class UpdateUserRequest
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }

    public sealed class DeleteUserRequest
    {
        public string? Id { get; set; }
    }

    public sealed class RestoreUserRequest
    {
        public string? Id { get; set; }
    }

    public sealed class UserListQuery
    {
        public string? SearchString { get; init; }
        public string? RoleFilter { get; init; }
        public string? SortOption { get; init; }
        public string? StatusFilter { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 10;
    }

    public sealed class UserListResult
    {
        public IReadOnlyList<UserResponse> Users { get; init; } = Array.Empty<UserResponse>();
        public IReadOnlyList<string> AvailableRoles { get; init; } = Array.Empty<string>();
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages { get; init; }
        public string StatusFilter { get; init; } = "all";
    }
}
