using System;

namespace env_analysis_project.Contracts.MeasurementResults
{
    public sealed class MeasurementResultListQuery
    {
        public string? Type { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public bool Paged { get; init; }
        public string? Search { get; init; }
        public int? SourceId { get; init; }
        public string? ParameterCode { get; init; }
        public string? Status { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
    }

    public sealed class ParameterTrendsQuery
    {
        public string? Code { get; init; }
        public string[]? Codes { get; init; }
        public string? StartMonth { get; init; }
        public string? EndMonth { get; init; }
        public int? SourceId { get; init; }
    }
}
