using System;

namespace env_analysis_project.Contracts.Parameters
{
    public sealed class LatestParameterMeasurementDto
    {
        public string ParameterCode { get; init; } = string.Empty;
        public string ParameterName { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public double? Value { get; init; }
        public DateTime? MeasurementDate { get; init; }
    }

    public sealed class ParameterMeasurementValueDto
    {
        public string ParameterCode { get; init; } = string.Empty;
        public DateTime MeasurementDate { get; init; }
        public double? Value { get; init; }
        public string? Unit { get; init; }
        public int EmissionSourceId { get; init; }
        public string? EmissionSourceName { get; init; }
    }
}
