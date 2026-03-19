using System;

namespace env_analysis_project.Contracts.EmissionSources
{
    public sealed class EmissionSourceResponse
    {
        public int EmissionSourceID { get; set; }
        public string SourceCode { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int SourceTypeID { get; set; }
        public string? SourceTypeName { get; set; }
    }
}
