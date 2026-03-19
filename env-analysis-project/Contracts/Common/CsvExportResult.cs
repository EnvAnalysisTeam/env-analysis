using System;

namespace env_analysis_project.Contracts.Common
{
    public sealed class CsvExportResult
    {
        public byte[] Bytes { get; init; } = Array.Empty<byte>();
        public string ContentType { get; init; } = "text/csv";
        public string FileName { get; init; } = string.Empty;
    }
}
