using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Common;
using env_analysis_project.Contracts.SystemLogs;
using env_analysis_project.Data;
using env_analysis_project.Models;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface ISystemLogService
    {
        Task<SystemLogManageData> GetManageDataAsync(SystemLogQuery query);
        Task<CsvExportResult> ExportAsync(SystemLogQuery query);
    }

    public sealed class SystemLogService : ISystemLogService
    {
        private const int MaxPageSize = 100;
        private readonly env_analysis_projectContext _context;

        public SystemLogService(env_analysis_projectContext context)
        {
            _context = context;
        }

        public async Task<SystemLogManageData> GetManageDataAsync(SystemLogQuery queryInput)
        {
            var page = Math.Max(queryInput.Page, 1);
            var pageSize = Math.Clamp(queryInput.PageSize, 10, MaxPageSize);

            var query = BuildFilteredQuery(queryInput.Search, queryInput.ActionType, queryInput.From, queryInput.To);
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max((int)Math.Ceiling(totalItems / (double)pageSize), 1);
            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderByDescending(log => log.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(log => new SystemLogRow
                {
                    Id = log.Id,
                    OccurredAt = log.OccurredAt,
                    UserDisplayName = log.User != null
                        ? (!string.IsNullOrEmpty(log.User.FullName)
                            ? log.User.FullName
                            : (log.User.Email ?? log.User.Id))
                        : "Unknown",
                    UserEmail = log.User != null ? log.User.Email : null,
                    ActionType = log.ActionType,
                    EntityName = log.EntityName,
                    EntityId = log.EntityId,
                    Description = log.Description,
                    MetadataJson = log.MetadataJson
                })
                .ToListAsync();

            var actionOptions = await _context.UserActivityLogs
                .Select(log => log.ActionType)
                .Distinct()
                .OrderBy(x => x)
                .Take(100)
                .ToListAsync();

            return new SystemLogManageData
            {
                Model = new SystemLogViewModel
                {
                    Items = items,
                    Search = queryInput.Search,
                    ActionType = queryInput.ActionType,
                    From = queryInput.From,
                    To = queryInput.To,
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                },
                ActionOptions = actionOptions
            };
        }

        public async Task<CsvExportResult> ExportAsync(SystemLogQuery queryInput)
        {
            var logs = await BuildFilteredQuery(queryInput.Search, queryInput.ActionType, queryInput.From, queryInput.To)
                .OrderByDescending(log => log.OccurredAt)
                .Select(log => new
                {
                    log.OccurredAt,
                    UserName = log.User != null
                        ? (!string.IsNullOrEmpty(log.User.FullName)
                            ? log.User.FullName
                            : (log.User.Email ?? log.User.Id))
                        : "Unknown",
                    log.UserId,
                    log.ActionType,
                    log.EntityName,
                    log.EntityId,
                    log.Description,
                    log.MetadataJson
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Occurred At,User,User Id,Action,Entity Name,Entity Id,Description,Metadata");

            foreach (var log in logs)
            {
                csv.AppendLine(string.Join(',',
                    EscapeCsv(log.OccurredAt.ToString("u")),
                    EscapeCsv(log.UserName),
                    EscapeCsv(log.UserId),
                    EscapeCsv(log.ActionType),
                    EscapeCsv(log.EntityName),
                    EscapeCsv(log.EntityId),
                    EscapeCsv(log.Description),
                    EscapeCsv(log.MetadataJson)));
            }

            return new CsvExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(csv.ToString()),
                ContentType = "text/csv",
                FileName = $"system-log-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };
        }

        private IQueryable<UserActivityLog> BuildFilteredQuery(string? search, string? actionType, DateTime? from, DateTime? to)
        {
            var query = _context.UserActivityLogs
                .Include(log => log.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(log =>
                    (log.User != null && (
                        (!string.IsNullOrEmpty(log.User.FullName) && log.User.FullName.Contains(keyword)) ||
                        (!string.IsNullOrEmpty(log.User.Email) && log.User.Email.Contains(keyword)))) ||
                    (!string.IsNullOrEmpty(log.ActionType) && log.ActionType.Contains(keyword)) ||
                    (!string.IsNullOrEmpty(log.Description) && log.Description.Contains(keyword)) ||
                    (!string.IsNullOrEmpty(log.EntityName) && log.EntityName.Contains(keyword)) ||
                    (!string.IsNullOrEmpty(log.EntityId) && log.EntityId.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                var normalizedAction = actionType.Trim();
                query = query.Where(log => log.ActionType == normalizedAction);
            }

            if (from.HasValue)
            {
                query = query.Where(log => log.OccurredAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(log => log.OccurredAt <= to.Value);
            }

            return query;
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
