using System;
using System.Collections.Generic;

namespace env_analysis_project.Contracts.SystemLogs
{
    public sealed class SystemLogQuery
    {
        public string? Search { get; init; }
        public string? ActionType { get; init; }
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 25;
    }

    public sealed class SystemLogManageData
    {
        public SystemLogViewModel Model { get; init; } = new SystemLogViewModel();
        public IReadOnlyList<string> ActionOptions { get; init; } = Array.Empty<string>();
    }

    public sealed class SystemLogViewModel
    {
        public IReadOnlyList<SystemLogRow> Items { get; set; } = Array.Empty<SystemLogRow>();
        public string? Search { get; set; }
        public string? ActionType { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int StartItem => TotalItems == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int EndItem => TotalItems == 0 ? 0 : Math.Min(Page * PageSize, TotalItems);
    }

    public sealed class SystemLogRow
    {
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public string? EntityId { get; set; }
        public string? Description { get; set; }
        public string? MetadataJson { get; set; }
    }
}
