using System;

namespace env_analysis_project.Contracts.MeasurementResults
{
    public sealed class MeasurementResultRequest
    {
        public int EmissionSourceId { get; set; }
        public string ParameterCode { get; set; } = string.Empty;
        public DateTime MeasurementDate { get; set; }
        public double? Value { get; set; }
        public string? Unit { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? Remark { get; set; }
    }
}
