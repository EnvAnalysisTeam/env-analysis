using System;

namespace env_analysis_project.Contracts.Parameters
{
    public sealed class ParameterDto
    {
        public string ParameterCode { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public string Type { get; set; } = "water";
        public string? Unit { get; set; }
        public double? StandardValue { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
