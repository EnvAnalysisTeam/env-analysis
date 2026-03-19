using System;
using System.Collections.Generic;

namespace env_analysis_project.Contracts.Common
{
    public sealed class DashboardLookupData
    {
        public IReadOnlyList<LookupOption> EmissionSources { get; init; } = Array.Empty<LookupOption>();
        public IReadOnlyList<ParameterOption> Parameters { get; init; } = Array.Empty<ParameterOption>();
    }

    public sealed class LookupOption
    {
        public int Id { get; init; }
        public string Label { get; init; } = string.Empty;
    }

    public sealed class ParameterOption
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public double? StandardValue { get; init; }
        public string Type { get; init; } = "water";
    }
}
