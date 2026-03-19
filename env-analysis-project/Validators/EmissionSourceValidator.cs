using System.Collections.Generic;
using env_analysis_project.Contracts.EmissionSources;
using env_analysis_project.Models;

namespace env_analysis_project.Validators
{
    public static class EmissionSourceValidator
    {
        public static IReadOnlyCollection<string> Validate(EmissionSource? model)
        {
            var errors = new List<string>();
            if (model == null)
            {
                errors.Add("Emission source payload is required.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(model.SourceCode))
            {
                errors.Add("Source code is required.");
            }

            if (string.IsNullOrWhiteSpace(model.SourceName))
            {
                errors.Add("Source name is required.");
            }

            if (model.SourceTypeID <= 0)
            {
                errors.Add("A valid source type is required.");
            }

            if (model.Latitude is < -90 or > 90)
            {
                errors.Add("Latitude must be between -90 and 90.");
            }

            if (model.Longitude is < -180 or > 180)
            {
                errors.Add("Longitude must be between -180 and 180.");
            }

            return errors;
        }

        public static IReadOnlyCollection<string> ValidateDelete(DeleteEmissionSourceRequest? request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("Delete request payload is required.");
                return errors;
            }

            if (request.Id <= 0)
            {
                errors.Add("Invalid emission source identifier.");
            }

            return errors;
        }
    }
}
