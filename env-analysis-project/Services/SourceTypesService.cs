using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.SourceTypes;
using env_analysis_project.Data;
using env_analysis_project.Models;
using env_analysis_project.Validators;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface ISourceTypesService
    {
        Task<IReadOnlyList<SourceTypeDto>> GetActiveListAsync();
        Task<SourceType?> FindActiveByIdAsync(int? id);
        Task<ServiceResult<SourceTypeDto>> GetByIdAsync(int id);
        Task<ServiceResult<SourceTypeDto>> CreateAsync(SourceType sourceType, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<SourceTypeDto>> EditAsync(int id, SourceType model, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<object?>> DeleteAsync(int id);
    }

    public sealed class SourceTypesService : ISourceTypesService
    {
        private readonly env_analysis_projectContext _context;
        private readonly IUserActivityLogger _activityLogger;

        public SourceTypesService(env_analysis_projectContext context, IUserActivityLogger activityLogger)
        {
            _context = context;
            _activityLogger = activityLogger;
        }

        public async Task<IReadOnlyList<SourceTypeDto>> GetActiveListAsync()
        {
            return await ActiveSourceTypes()
                .Select(st => new SourceTypeDto
                {
                    SourceTypeID = st.SourceTypeID,
                    SourceTypeName = st.SourceTypeName,
                    Description = st.Description,
                    IsActive = st.IsActive,
                    CreatedAt = st.CreatedAt,
                    UpdatedAt = st.UpdatedAt,
                    EmissionSourceCount = _context.EmissionSource.Count(es => es.SourceTypeID == st.SourceTypeID && !es.IsDeleted)
                })
                .ToListAsync();
        }

        public async Task<SourceType?> FindActiveByIdAsync(int? id)
        {
            if (!id.HasValue)
            {
                return null;
            }

            return await ActiveSourceTypes().FirstOrDefaultAsync(m => m.SourceTypeID == id.Value);
        }

        public async Task<ServiceResult<SourceTypeDto>> GetByIdAsync(int id)
        {
            var dto = await ActiveSourceTypes()
                .Where(s => s.SourceTypeID == id)
                .Select(s => new SourceTypeDto
                {
                    SourceTypeID = s.SourceTypeID,
                    SourceTypeName = s.SourceTypeName,
                    Description = s.Description,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    EmissionSourceCount = _context.EmissionSource.Count(es => es.SourceTypeID == s.SourceTypeID && !es.IsDeleted)
                })
                .FirstOrDefaultAsync();

            if (dto == null)
            {
                return ServiceResult<SourceTypeDto>.Fail("Source type not found.");
            }

            return ServiceResult<SourceTypeDto>.Ok(dto);
        }

        public async Task<ServiceResult<SourceTypeDto>> CreateAsync(SourceType sourceType, IReadOnlyCollection<string>? modelErrors = null)
        {
            var validationErrors = SourceTypeValidator.Validate(sourceType).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<SourceTypeDto>.Fail("Validation failed.", validationErrors);
            }

            sourceType.CreatedAt = DateTime.Now;
            sourceType.UpdatedAt = DateTime.Now;

            _context.SourceType.Add(sourceType);
            await _context.SaveChangesAsync();
            await LogAsync("SourceType.Create", sourceType.SourceTypeID.ToString(), $"Created source type {sourceType.SourceTypeName}");

            return ServiceResult<SourceTypeDto>.Ok(ToDto(sourceType), "Source type created successfully.");
        }

        public async Task<ServiceResult<SourceTypeDto>> EditAsync(int id, SourceType model, IReadOnlyCollection<string>? modelErrors = null)
        {
            if (id != model.SourceTypeID)
            {
                return ServiceResult<SourceTypeDto>.Fail("Invalid source type identifier.");
            }

            var validationErrors = SourceTypeValidator.Validate(model).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<SourceTypeDto>.Fail("Validation failed.", validationErrors);
            }

            var existing = await _context.SourceType.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return ServiceResult<SourceTypeDto>.Fail("Source type not found.");
            }

            existing.SourceTypeName = model.SourceTypeName?.Trim();
            existing.Description = model.Description?.Trim();
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.Now;

            _context.Update(existing);
            await _context.SaveChangesAsync();
            await LogAsync("SourceType.Update", existing.SourceTypeID.ToString(), $"Updated source type {existing.SourceTypeName}");

            return ServiceResult<SourceTypeDto>.Ok(ToDto(existing), "Source type updated successfully.");
        }

        public async Task<ServiceResult<object?>> DeleteAsync(int id)
        {
            var sourceType = await _context.SourceType.FindAsync(id);
            if (sourceType != null && !sourceType.IsDeleted)
            {
                sourceType.IsDeleted = true;
                sourceType.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await LogAsync("SourceType.Delete", sourceType.SourceTypeID.ToString(), $"Deleted source type {sourceType.SourceTypeName}");
            }

            return ServiceResult<object?>.Ok(null, "Source type deleted successfully.");
        }

        private IQueryable<SourceType> ActiveSourceTypes() =>
            _context.SourceType.Where(st => !st.IsDeleted);

        private static SourceTypeDto ToDto(SourceType sourceType, int? emissionSourceCount = null)
        {
            return new SourceTypeDto
            {
                SourceTypeID = sourceType.SourceTypeID,
                SourceTypeName = sourceType.SourceTypeName,
                Description = sourceType.Description,
                IsActive = sourceType.IsActive,
                CreatedAt = sourceType.CreatedAt,
                UpdatedAt = sourceType.UpdatedAt,
                EmissionSourceCount = emissionSourceCount
            };
        }

        private Task LogAsync(string action, string entityId, string description) =>
            _activityLogger.LogAsync(action, "SourceType", entityId, description);
    }
}
