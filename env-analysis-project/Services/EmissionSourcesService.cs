using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.EmissionSources;
using env_analysis_project.Data;
using env_analysis_project.Models;
using env_analysis_project.Validators;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface IEmissionSourcesService
    {
        Task<EmissionSourcesIndexData> GetIndexDataAsync();
        Task<ServiceResult<EmissionSourceResponse>> GetDetailAsync(int id);
        Task<ServiceResult<EmissionSourceResponse>> CreateAsync(EmissionSource model, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<EmissionSourceResponse>> EditAsync(int id, EmissionSource model, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<object?>> DeleteAsync(DeleteEmissionSourceRequest request);
        Task<ServiceResult<object?>> RestoreAsync(DeleteEmissionSourceRequest request);
    }

    public sealed class EmissionSourcesIndexData
    {
        public IReadOnlyList<EmissionSource> EmissionSources { get; init; } = Array.Empty<EmissionSource>();
        public IReadOnlyList<SourceType> SourceTypes { get; init; } = Array.Empty<SourceType>();
    }

    public sealed class EmissionSourcesService : IEmissionSourcesService
    {
        private readonly env_analysis_projectContext _context;
        private readonly IUserActivityLogger _activityLogger;

        public EmissionSourcesService(env_analysis_projectContext context, IUserActivityLogger activityLogger)
        {
            _context = context;
            _activityLogger = activityLogger;
        }

        public async Task<EmissionSourcesIndexData> GetIndexDataAsync()
        {
            var emissionSources = await ActiveEmissionSources()
                .Include(e => e.SourceType)
                .OrderBy(e => e.SourceName)
                .ToListAsync();

            var sourceTypes = await _context.SourceType
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.SourceTypeName)
                .ToListAsync();

            return new EmissionSourcesIndexData
            {
                EmissionSources = emissionSources,
                SourceTypes = sourceTypes
            };
        }

        public async Task<ServiceResult<EmissionSourceResponse>> GetDetailAsync(int id)
        {
            var source = await ActiveEmissionSources()
                .Include(e => e.SourceType)
                .FirstOrDefaultAsync(e => e.EmissionSourceID == id);

            if (source == null)
            {
                return ServiceResult<EmissionSourceResponse>.Fail("Emission source not found.");
            }

            return ServiceResult<EmissionSourceResponse>.Ok(ToDto(source));
        }

        public async Task<ServiceResult<EmissionSourceResponse>> CreateAsync(EmissionSource model, IReadOnlyCollection<string>? modelErrors = null)
        {
            var validationErrors = EmissionSourceValidator.Validate(model).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<EmissionSourceResponse>.Fail("Validation failed.", validationErrors);
            }

            model.CreatedAt = DateTime.Now;
            model.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location;
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description;

            _context.EmissionSource.Add(model);
            await _context.SaveChangesAsync();
            await _context.Entry(model).Reference(e => e.SourceType).LoadAsync();
            await LogAsync("EmissionSource.Create", model.EmissionSourceID.ToString(), $"Created source {model.SourceName}");
            return ServiceResult<EmissionSourceResponse>.Ok(ToDto(model), "Emission source created successfully!");
        }

        public async Task<ServiceResult<EmissionSourceResponse>> EditAsync(int id, EmissionSource model, IReadOnlyCollection<string>? modelErrors = null)
        {
            if (id != model.EmissionSourceID)
            {
                return ServiceResult<EmissionSourceResponse>.Fail("Invalid emission source identifier.");
            }

            var existing = await _context.EmissionSource.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return ServiceResult<EmissionSourceResponse>.Fail("Emission source not found.");
            }

            var validationErrors = EmissionSourceValidator.Validate(model).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<EmissionSourceResponse>.Fail("Validation failed.", validationErrors);
            }

            existing.SourceCode = model.SourceCode;
            existing.SourceName = model.SourceName;
            existing.SourceTypeID = model.SourceTypeID;
            existing.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location;
            existing.Latitude = model.Latitude;
            existing.Longitude = model.Longitude;
            existing.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            await _context.Entry(existing).Reference(e => e.SourceType).LoadAsync();
            await LogAsync("EmissionSource.Update", existing.EmissionSourceID.ToString(), $"Updated source {existing.SourceName}");
            return ServiceResult<EmissionSourceResponse>.Ok(ToDto(existing), "Emission source updated successfully!");
        }

        public async Task<ServiceResult<object?>> DeleteAsync(DeleteEmissionSourceRequest request)
        {
            var validationErrors = EmissionSourceValidator.ValidateDelete(request).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<object?>.Fail("Invalid emission source identifier.", validationErrors);
            }

            var emissionSource = await _context.EmissionSource.FindAsync(request.Id);
            if (emissionSource == null || emissionSource.IsDeleted)
            {
                return ServiceResult<object?>.Fail("Emission source not found.");
            }

            emissionSource.IsDeleted = true;
            emissionSource.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await LogAsync("EmissionSource.Delete", emissionSource.EmissionSourceID.ToString(), $"Deleted source {emissionSource.SourceName}");
            return ServiceResult<object?>.Ok(new { request.Id }, "Emission source deleted successfully!");
        }

        public async Task<ServiceResult<object?>> RestoreAsync(DeleteEmissionSourceRequest request)
        {
            var validationErrors = EmissionSourceValidator.ValidateDelete(request).ToList();
            if (validationErrors.Count > 0)
            {
                return ServiceResult<object?>.Fail("Invalid emission source identifier.", validationErrors);
            }

            var emissionSource = await _context.EmissionSource.FindAsync(request.Id);
            if (emissionSource == null)
            {
                return ServiceResult<object?>.Fail("Emission source not found.");
            }

            if (!emissionSource.IsDeleted)
            {
                return ServiceResult<object?>.Fail("Emission source is already active.");
            }

            emissionSource.IsDeleted = false;
            emissionSource.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await LogAsync("EmissionSource.Restore", emissionSource.EmissionSourceID.ToString(), $"Restored source {emissionSource.SourceName}");
            return ServiceResult<object?>.Ok(new { request.Id }, "Emission source restored successfully!");
        }

        private IQueryable<EmissionSource> ActiveEmissionSources() =>
            _context.EmissionSource.Where(e => !e.IsDeleted);

        private static EmissionSourceResponse ToDto(EmissionSource source)
        {
            return new EmissionSourceResponse
            {
                EmissionSourceID = source.EmissionSourceID,
                SourceCode = source.SourceCode,
                SourceName = source.SourceName,
                Description = source.Description,
                Location = source.Location,
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                IsActive = source.IsActive,
                CreatedAt = source.CreatedAt,
                UpdatedAt = source.UpdatedAt,
                SourceTypeID = source.SourceTypeID,
                SourceTypeName = source.SourceType?.SourceTypeName
            };
        }

        private Task LogAsync(string action, string? entityId, string? description) =>
            _activityLogger.LogAsync(action, "EmissionSource", entityId, description);
    }
}
