using System;

namespace env_analysis_project.Contracts.MeasurementResults
{
    public sealed class MeasurementResultDto
    {
        public int ResultID { get; set; }
        public string Type { get; set; } = string.Empty;
        public int EmissionSourceID { get; set; }
        public string EmissionSourceName { get; set; } = string.Empty;
        public string ParameterCode { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public DateTime? MeasurementDate { get; set; }
        public double? Value { get; set; }
        public string? Unit { get; set; }
        public string? Remark { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
