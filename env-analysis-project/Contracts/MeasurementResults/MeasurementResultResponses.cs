using System;
using System.Collections.Generic;

namespace env_analysis_project.Contracts.MeasurementResults
{
    public sealed class LookupOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public sealed class ParameterLookup
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public double? StandardValue { get; set; }
        public string Type { get; set; } = "water";
    }

    public sealed class MeasurementResultsManageData
    {
        public IReadOnlyList<LookupOption> EmissionSources { get; init; } = Array.Empty<LookupOption>();
        public IReadOnlyList<ParameterLookup> Parameters { get; init; } = Array.Empty<ParameterLookup>();
    }

    public sealed class CsvExportResult
    {
        public byte[] Bytes { get; init; } = Array.Empty<byte>();
        public string ContentType { get; init; } = "text/csv";
        public string FileName { get; init; } = string.Empty;
    }

    public sealed class MeasurementResultListResponse
    {
        public IReadOnlyList<MeasurementResultDto> Items { get; init; } = Array.Empty<MeasurementResultDto>();
        public PaginationMetadata Pagination { get; init; } = new PaginationMetadata();
        public MeasurementResultSummary Summary { get; init; } = new MeasurementResultSummary();
    }

    public sealed class PaginationMetadata
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages { get; init; }
    }

    public sealed class MeasurementResultSummary
    {
        public int All { get; set; }
        public int Water { get; set; }
        public int Air { get; set; }
    }

    public sealed class ParameterTrendResponse
    {
        public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<ParameterTrendSeries> Series { get; init; } = Array.Empty<ParameterTrendSeries>();
        public TrendTablePage Table { get; init; } = new TrendTablePage();
        public double? StandardValue { get; init; }
    }

    public sealed class ParameterTrendSeries
    {
        public string ParameterCode { get; init; } = string.Empty;
        public string ParameterName { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public double? StandardValue { get; init; }
        public IReadOnlyList<ParameterTrendPoint> Points { get; init; } = Array.Empty<ParameterTrendPoint>();
        public bool IsForecast { get; init; }
        public string? ForecastKind { get; init; }
    }

    public sealed class ParameterTrendPoint
    {
        public string Month { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public double? Value { get; init; }
        public string? SourceName { get; init; }
        public string? ParameterName { get; init; }
        public string? Unit { get; init; }
    }

    public sealed class TrendTablePage
    {
        public IReadOnlyList<ParameterTrendPoint> Items { get; init; } = Array.Empty<ParameterTrendPoint>();
        public PaginationMetadata Pagination { get; init; } = new PaginationMetadata();
        public string? Unit { get; init; }
        public int? SourceId { get; init; }
    }
}
