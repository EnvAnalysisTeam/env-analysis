using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Common;
using env_analysis_project.Data;
using env_analysis_project.Models;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface ISourceManagementService
    {
        Task<SourceManagementManageData> GetManageDataAsync();
        Task<CsvExportResult> ExportCsvAsync();
    }

    public sealed class SourceManagementManageData
    {
        public IReadOnlyList<EmissionSource> Sources { get; init; } = Array.Empty<EmissionSource>();
        public IReadOnlyList<SourceTypeSummary> SourceTypes { get; init; } = Array.Empty<SourceTypeSummary>();
    }

    public sealed class SourceTypeSummary
    {
        public int SourceTypeID { get; init; }
        public string SourceTypeName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsActive { get; init; }
        public DateTime? CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public int Count { get; init; }
    }

    public sealed class SourceManagementService : ISourceManagementService
    {
        private readonly env_analysis_projectContext _context;

        public SourceManagementService(env_analysis_projectContext context)
        {
            _context = context;
        }

        public async Task<SourceManagementManageData> GetManageDataAsync()
        {
            var sources = await _context.EmissionSource
                .Include(e => e.SourceType)
                .OrderBy(e => e.IsDeleted)
                .ThenBy(e => e.SourceName)
                .ToListAsync();

            var sourceTypes = await _context.SourceType
                .Where(st => !st.IsDeleted)
                .Select(st => new SourceTypeSummary
                {
                    SourceTypeID = st.SourceTypeID,
                    SourceTypeName = st.SourceTypeName,
                    Description = st.Description,
                    IsActive = st.IsActive,
                    CreatedAt = st.CreatedAt,
                    UpdatedAt = st.UpdatedAt,
                    Count = _context.EmissionSource.Count(es => es.SourceTypeID == st.SourceTypeID && !es.IsDeleted)
                })
                .ToListAsync();

            return new SourceManagementManageData
            {
                Sources = sources,
                SourceTypes = sourceTypes
            };
        }

        public async Task<CsvExportResult> ExportCsvAsync()
        {
            var sources = await _context.EmissionSource
                .Where(e => !e.IsDeleted)
                .Include(e => e.SourceType)
                .OrderBy(e => e.SourceName)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Source Name,Source Code,Location,Latitude,Longitude,Source Type,Status");
            foreach (var source in sources)
            {
                var fields = new[]
                {
                    EscapeCsv(source.SourceName),
                    EscapeCsv(source.SourceCode),
                    EscapeCsv(source.Location),
                    EscapeCsv(source.Latitude?.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(source.Longitude?.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(source.SourceType?.SourceTypeName),
                    EscapeCsv(source.IsActive ? "Active" : "Inactive")
                };
                builder.AppendLine(string.Join(",", fields));
            }

            return new CsvExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(builder.ToString()),
                ContentType = "text/csv",
                FileName = $"emission-sources-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var sanitized = value.Replace("\"", "\"\"");
            return $"\"{sanitized}\"";
        }
    }
}
