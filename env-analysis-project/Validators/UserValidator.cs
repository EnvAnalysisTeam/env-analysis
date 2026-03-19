using System.Collections.Generic;
using env_analysis_project.Models;

namespace env_analysis_project.Validators
{
    public static class UserValidator
    {
        public static IReadOnlyCollection<string> Validate(ApplicationUser? user, bool requireId = false)
        {
            var errors = new List<string>();
            if (user == null)
            {
                errors.Add("User payload is required.");
                return errors;
            }

            if (requireId && string.IsNullOrWhiteSpace(user.Id))
            {
                errors.Add("User identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                errors.Add("Email is required.");
            }

            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                errors.Add("Full name is required.");
            }

            if (!string.IsNullOrWhiteSpace(user.Role) && user.Role.Length > 100)
            {
                errors.Add("Role cannot exceed 100 characters.");
            }

            return errors;
        }

        public static IReadOnlyCollection<string> ValidateForUpdate(ApplicationUser? user) =>
            Validate(user, requireId: true);
    }
}
