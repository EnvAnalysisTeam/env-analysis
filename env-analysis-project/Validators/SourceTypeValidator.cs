using System.Collections.Generic;
using env_analysis_project.Models;

namespace env_analysis_project.Validators
{
    public static class SourceTypeValidator
    {
        public static IReadOnlyCollection<string> Validate(SourceType? model)
        {
            var errors = new List<string>();
            if (model == null)
            {
                errors.Add("Source type payload is required.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(model.SourceTypeName))
            {
                errors.Add("Source type name is required.");
            }
            else if (model.SourceTypeName.Length > 200)
            {
                errors.Add("Source type name cannot exceed 200 characters.");
            }

            if (!string.IsNullOrWhiteSpace(model.Description) && model.Description.Length > 1000)
            {
                errors.Add("Description cannot exceed 1000 characters.");
            }

            return errors;
        }
    }
}
