using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Common;
using env_analysis_project.Contracts.Parameters;
using env_analysis_project.Data;
using env_analysis_project.Models;
using env_analysis_project.Validators;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface IParametersService
    {
        Task<IReadOnlyList<Parameter>> GetActiveListAsync();
        Task<CsvExportResult> ExportCsvAsync();
        Task<Parameter?> GetActiveByIdAsync(string id);
        Task<ServiceResult<Parameter>> CreateAsync(Parameter parameter, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<Parameter>> EditAsync(string id, Parameter parameter, IReadOnlyCollection<string>? modelErrors = null);
        Task DeleteSoftAsync(string id);
        Task<IReadOnlyList<ParameterDto>> ListDataAsync();
        Task<IReadOnlyList<LatestParameterMeasurementDto>> LatestMeasurementValuesAsync();
        Task<ServiceResult<IReadOnlyList<ParameterMeasurementValueDto>>> LatestMeasurementByCodeAsync(string code, int? sourceId);
        Task<ServiceResult<ParameterDto>> DetailDataAsync(string id);
        Task<ServiceResult<ParameterDto>> CreateAjaxAsync(ParameterDto dto);
        Task<ServiceResult<ParameterDto>> UpdateAjaxAsync(string id, ParameterDto dto);
        Task<ServiceResult<ParameterDto>> DeleteAjaxAsync(string id);
        Task<ServiceResult<ParameterDto>> RestoreAjaxAsync(ParameterDto request);
    }

    public sealed class ParametersService : IParametersService
    {
        private readonly env_analysis_projectContext _context;
        private readonly IUserActivityLogger _activityLogger;

        public ParametersService(env_analysis_projectContext context, IUserActivityLogger activityLogger)
        {
            _context = context;
            _activityLogger = activityLogger;
        }

        public async Task<IReadOnlyList<Parameter>> GetActiveListAsync()
        {
            return await _context.Parameter.Where(p => !p.IsDeleted).ToListAsync();
        }

        public async Task<CsvExportResult> ExportCsvAsync()
        {
            var parameters = await _context.Parameter
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.ParameterName)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Code,Name,Unit,Standard,Description,Created");
            foreach (var parameter in parameters)
            {
                var fields = new[]
                {
                    EscapeCsv(parameter.ParameterCode),
                    EscapeCsv(parameter.ParameterName),
                    EscapeCsv(parameter.Unit),
                    EscapeCsv(parameter.StandardValue?.ToString(CultureInfo.InvariantCulture)),
                    EscapeCsv(parameter.Description),
                    EscapeCsv(parameter.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                };
                builder.AppendLine(string.Join(",", fields));
            }

            return new CsvExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(builder.ToString()),
                ContentType = "text/csv",
                FileName = $"parameters-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };
        }

        public async Task<Parameter?> GetActiveByIdAsync(string id)
        {
            return await _context.Parameter.FirstOrDefaultAsync(m => m.ParameterCode == id && !m.IsDeleted);
        }

        public async Task<ServiceResult<Parameter>> CreateAsync(Parameter parameter, IReadOnlyCollection<string>? modelErrors = null)
        {
            parameter.Type = ParameterTypeHelper.Normalize(parameter.Type);
            var validationErrors = ParameterValidator.Validate(parameter).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<Parameter>.Fail("Validation failed.", validationErrors);
            }

            parameter.ParameterCode = parameter.ParameterCode.Trim();
            parameter.ParameterName = parameter.ParameterName.Trim();
            parameter.Unit = string.IsNullOrWhiteSpace(parameter.Unit) ? null : parameter.Unit.Trim();
            parameter.Description = string.IsNullOrWhiteSpace(parameter.Description) ? null : parameter.Description.Trim();
            parameter.Type = ParameterTypeHelper.Normalize(parameter.Type);
            parameter.CreatedAt = DateTime.UtcNow;
            parameter.UpdatedAt = DateTime.UtcNow;
            parameter.IsDeleted = false;

            _context.Add(parameter);
            await _context.SaveChangesAsync();
            return ServiceResult<Parameter>.Ok(parameter);
        }

        public async Task<ServiceResult<Parameter>> EditAsync(string id, Parameter parameter, IReadOnlyCollection<string>? modelErrors = null)
        {
            if (id != parameter.ParameterCode)
            {
                return ServiceResult<Parameter>.Fail("Parameter not found.");
            }
            parameter.Type = ParameterTypeHelper.Normalize(parameter.Type);

            var validationErrors = ParameterValidator.Validate(parameter).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<Parameter>.Fail("Validation failed.", validationErrors);
            }

            try
            {
                parameter.ParameterName = parameter.ParameterName.Trim();
                parameter.Unit = string.IsNullOrWhiteSpace(parameter.Unit) ? null : parameter.Unit.Trim();
                parameter.Description = string.IsNullOrWhiteSpace(parameter.Description) ? null : parameter.Description.Trim();
                parameter.Type = ParameterTypeHelper.Normalize(parameter.Type);
                parameter.UpdatedAt = DateTime.UtcNow;

                _context.Update(parameter);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ParameterExists(parameter.ParameterCode))
                {
                    return ServiceResult<Parameter>.Fail("Parameter not found.");
                }

                throw;
            }

            return ServiceResult<Parameter>.Ok(parameter);
        }

        public async Task DeleteSoftAsync(string id)
        {
            var parameter = await _context.Parameter.FindAsync(id);
            if (parameter != null && !parameter.IsDeleted)
            {
                parameter.IsDeleted = true;
                parameter.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<ParameterDto>> ListDataAsync()
        {
            return await _context.Parameter
                .OrderBy(p => p.IsDeleted)
                .ThenBy(p => p.ParameterName)
                .Select(p => ToDto(p))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<LatestParameterMeasurementDto>> LatestMeasurementValuesAsync()
        {
            const string sql = """
                WITH LatestMeasurement AS (
                    SELECT
                        mr.ResultID,
                        mr.ParameterCode,
                        mr.MeasurementDate,
                        mr.EntryDate,
                        mr.Value,
                        mr.Unit,
                        mr.EmissionSourceID,
                        p.ParameterName,
                        p.StandardValue,
                        p.Type,
                        ROW_NUMBER() OVER (
                            PARTITION BY mr.ParameterCode
                            ORDER BY
                                mr.MeasurementDate DESC,
                                mr.EntryDate DESC,
                                mr.ResultID DESC
                        ) AS rn
                    FROM MeasurementResult mr
                    INNER JOIN Parameter p
                        ON mr.ParameterCode = p.ParameterCode
                    WHERE p.IsDeleted = 0
                )
                SELECT
                    ResultID,
                    ParameterCode,
                    MeasurementDate,
                    EntryDate,
                    Value,
                    Unit,
                    EmissionSourceID,
                    ParameterName,
                    StandardValue,
                    Type
                FROM LatestMeasurement
                WHERE rn = 1
                ORDER BY ParameterName;
                """;

            var records = await _context.Set<LatestParameterMeasurementRecord>()
                .FromSqlRaw(sql)
                .AsNoTracking()
                .ToListAsync();

            return records
                .Select(record => new LatestParameterMeasurementDto
                {
                    ParameterCode = record.ParameterCode,
                    ParameterName = record.ParameterName,
                    Unit = record.Unit,
                    Value = record.Value,
                    MeasurementDate = record.MeasurementDate == default(DateTime)
                        ? record.EntryDate
                        : record.MeasurementDate
                })
                .OrderBy(dto => dto.ParameterName)
                .ToList();
        }

        public async Task<ServiceResult<IReadOnlyList<ParameterMeasurementValueDto>>> LatestMeasurementByCodeAsync(string code, int? sourceId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return ServiceResult<IReadOnlyList<ParameterMeasurementValueDto>>.Fail("Parameter code is required.");
            }

            var normalizedCode = code.Trim();
            var records = await _context.MeasurementResult
                .Where(result => result.ParameterCode == normalizedCode)
                .Where(result => !sourceId.HasValue || result.EmissionSourceID == sourceId.Value)
                .OrderByDescending(result => result.MeasurementDate)
                .Select(result => new ParameterMeasurementValueDto
                {
                    ParameterCode = result.ParameterCode,
                    MeasurementDate = result.MeasurementDate,
                    Value = result.Value,
                    Unit = result.Unit,
                    EmissionSourceId = result.EmissionSourceID,
                    EmissionSourceName = result.EmissionSource.SourceName
                })
                .AsNoTracking()
                .ToListAsync();

            if (records.Count == 0)
            {
                return ServiceResult<IReadOnlyList<ParameterMeasurementValueDto>>.Fail("No measurement data found for this parameter.");
            }

            return ServiceResult<IReadOnlyList<ParameterMeasurementValueDto>>.Ok(records);
        }

        public async Task<ServiceResult<ParameterDto>> DetailDataAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return ServiceResult<ParameterDto>.Fail("Parameter code is required.");
            }

            var code = id.Trim();
            var parameter = await _context.Parameter.FindAsync(code);
            if (parameter == null || parameter.IsDeleted)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter not found.");
            }

            return ServiceResult<ParameterDto>.Ok(ToDto(parameter));
        }

        public async Task<ServiceResult<ParameterDto>> CreateAjaxAsync(ParameterDto dto)
        {
            var validationErrors = ParameterValidator.ValidateDto(dto).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<ParameterDto>.Fail("Validation failed.", validationErrors);
            }

            var code = dto.ParameterCode.Trim();
            if (await _context.Parameter.AnyAsync(p => p.ParameterCode == code))
            {
                return ServiceResult<ParameterDto>.Fail($"Parameter with code '{code}' already exists.");
            }

            var entity = new Parameter
            {
                ParameterCode = code,
                ParameterName = dto.ParameterName.Trim(),
                Type = ParameterTypeHelper.Normalize(dto.Type),
                Unit = string.IsNullOrWhiteSpace(dto.Unit) ? null : dto.Unit.Trim(),
                StandardValue = dto.StandardValue,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Parameter.Add(entity);
            await _context.SaveChangesAsync();
            await LogAsync("Parameter.Create", entity.ParameterCode, $"Created parameter {entity.ParameterName}");
            return ServiceResult<ParameterDto>.Ok(ToDto(entity), "Parameter created successfully.");
        }

        public async Task<ServiceResult<ParameterDto>> UpdateAjaxAsync(string id, ParameterDto dto)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return ServiceResult<ParameterDto>.Fail("Parameter code is required.");
            }

            var code = id.Trim();
            var parameter = await _context.Parameter.FindAsync(code);
            if (parameter == null || parameter.IsDeleted)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter not found.");
            }

            var validationErrors = ParameterValidator.ValidateDto(dto, isUpdate: true).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<ParameterDto>.Fail("Validation failed.", validationErrors);
            }

            parameter.ParameterName = dto.ParameterName.Trim();
            parameter.Type = ParameterTypeHelper.Normalize(dto.Type);
            parameter.Unit = string.IsNullOrWhiteSpace(dto.Unit) ? null : dto.Unit.Trim();
            parameter.StandardValue = dto.StandardValue;
            parameter.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            parameter.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogAsync("Parameter.Update", parameter.ParameterCode, $"Updated parameter {parameter.ParameterName}");
            return ServiceResult<ParameterDto>.Ok(ToDto(parameter), "Parameter updated successfully.");
        }

        public async Task<ServiceResult<ParameterDto>> DeleteAjaxAsync(string id)
        {
            var validationErrors = ParameterValidator.ValidateIdentifier(id).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<ParameterDto>.Fail(validationErrors[0], validationErrors);
            }

            var parameter = await _context.Parameter.FindAsync(id.Trim());
            if (parameter == null)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter not found.");
            }

            if (parameter.IsDeleted)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter is already deleted.");
            }

            parameter.IsDeleted = true;
            parameter.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await LogAsync("Parameter.Delete", parameter.ParameterCode, $"Deleted parameter {parameter.ParameterName}");
            return ServiceResult<ParameterDto>.Ok(ToDto(parameter), "Parameter deleted successfully.");
        }

        public async Task<ServiceResult<ParameterDto>> RestoreAjaxAsync(ParameterDto request)
        {
            var validationErrors = ParameterValidator.ValidateIdentifier(request?.ParameterCode).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<ParameterDto>.Fail(validationErrors[0], validationErrors);
            }

            var parameter = await _context.Parameter.FindAsync(request.ParameterCode.Trim());
            if (parameter == null)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter not found.");
            }

            if (!parameter.IsDeleted)
            {
                return ServiceResult<ParameterDto>.Fail("Parameter is already active.");
            }

            parameter.IsDeleted = false;
            parameter.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await LogAsync("Parameter.Restore", parameter.ParameterCode, $"Restored parameter {parameter.ParameterName}");
            return ServiceResult<ParameterDto>.Ok(ToDto(parameter), "Parameter restored successfully.");
        }

        private bool ParameterExists(string id)
        {
            return _context.Parameter.Any(e => e.ParameterCode == id && !e.IsDeleted);
        }

        private static ParameterDto ToDto(Parameter parameter)
        {
            return new ParameterDto
            {
                ParameterCode = parameter.ParameterCode,
                ParameterName = parameter.ParameterName,
                Type = parameter.Type,
                Unit = parameter.Unit,
                StandardValue = parameter.StandardValue,
                Description = parameter.Description,
                CreatedAt = parameter.CreatedAt,
                UpdatedAt = parameter.UpdatedAt,
                IsDeleted = parameter.IsDeleted
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

        private Task LogAsync(string action, string entityId, string description) =>
            _activityLogger.LogAsync(action, "Parameter", entityId, description);
    }
}
