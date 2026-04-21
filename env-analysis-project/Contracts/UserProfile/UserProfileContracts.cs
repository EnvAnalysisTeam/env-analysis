using System;

namespace env_analysis_project.Contracts.UserProfile
{
    public sealed class UserProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class ChangeOwnPasswordRequest
    {
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
