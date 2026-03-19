using System.Collections.Generic;
using env_analysis_project.Contracts.Parameters;
using env_analysis_project.Models;

namespace env_analysis_project.Validators
{
    public static class ParameterValidator
    {
        public static IReadOnlyCollection<string> Validate(Parameter? parameter)
        {
            var errors = new List<string>();
            if (parameter == null)
            {
                errors.Add("Parameter payload is required.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(parameter.ParameterCode))
            {
                errors.Add("Parameter code is required.");
            }

            if (string.IsNullOrWhiteSpace(parameter.ParameterName))
            {
                errors.Add("Parameter name is required.");
            }

            if (parameter.StandardValue is < 0)
            {
                errors.Add("Standard value cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(parameter.Unit) && parameter.Unit.Length > 50)
            {
                errors.Add("Unit cannot exceed 50 characters.");
            }

            if (!string.IsNullOrWhiteSpace(parameter.Description) && parameter.Description.Length > 1000)
            {
                errors.Add("Description cannot exceed 1000 characters.");
            }

            if (!ParameterTypeHelper.IsValid(parameter.Type))
            {
                errors.Add("Parameter type must be either 'water' or 'air'.");
            }

            return errors;
        }

        public static IReadOnlyCollection<string> ValidateDto(ParameterDto? dto, bool isUpdate = false)
        {
            var errors = new List<string>();
            if (dto == null)
            {
                errors.Add("Parameter payload is required.");
                return errors;
            }

            if (!isUpdate && string.IsNullOrWhiteSpace(dto.ParameterCode))
            {
                errors.Add("Parameter code is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.ParameterName))
            {
                errors.Add("Parameter name is required.");
            }

            if (dto.StandardValue is < 0)
            {
                errors.Add("Standard value cannot be negative.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Unit) && dto.Unit.Length > 50)
            {
                errors.Add("Unit cannot exceed 50 characters.");
            }

            if (!string.IsNullOrWhiteSpace(dto.Description) && dto.Description.Length > 1000)
            {
                errors.Add("Description cannot exceed 1000 characters.");
            }

            if (!ParameterTypeHelper.IsValid(dto.Type))
            {
                errors.Add("Parameter type must be either 'water' or 'air'.");
            }

            return errors;
        }

        public static IReadOnlyCollection<string> ValidateIdentifier(string? code)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add("Parameter code is required.");
            }
            return errors;
        }
    }
}
