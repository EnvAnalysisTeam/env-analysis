using System.Collections.Generic;
using env_analysis_project.Contracts.MeasurementResults;

namespace env_analysis_project.Validators
{
    public static class MeasurementResultValidator
    {
        public static IReadOnlyCollection<string> Validate(MeasurementResultRequest? request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("Measurement result payload is required.");
                return errors;
            }

            if (request.EmissionSourceId <= 0)
            {
                errors.Add("Emission source is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ParameterCode))
            {
                errors.Add("Parameter code is required.");
            }

            if (request.MeasurementDate == default)
            {
                errors.Add("Measurement date is required.");
            }

            if (request.Value is < 0)
            {
                errors.Add("Value cannot be negative.");
            }

            if (request.IsApproved && request.ApprovedAt is null)
            {
                errors.Add("Approved results must include an ApprovedAt value.");
            }

            if (!string.IsNullOrWhiteSpace(request.Unit) && request.Unit!.Length > 50)
            {
                errors.Add("Unit cannot exceed 50 characters.");
            }

            if (!string.IsNullOrWhiteSpace(request.Remark) && request.Remark!.Length > 500)
            {
                errors.Add("Remark cannot exceed 500 characters.");
            }

            return errors;
        }
    }
}
