using System;

namespace env_analysis_project.Contracts.SourceTypes
{
    public sealed class SourceTypeDto
    {
        public int SourceTypeID { get; set; }
        public string SourceTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? EmissionSourceCount { get; set; }
    }
}
