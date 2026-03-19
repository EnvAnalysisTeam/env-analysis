using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using env_analysis_project.Contracts.MeasurementResults;
using env_analysis_project.Data;
using env_analysis_project.Models;
using env_analysis_project.Validators;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface IMeasurementResultsService
    {
        Task<IReadOnlyList<MeasurementResult>> GetIndexEntitiesAsync();
        Task<MeasurementResult?> GetDetailsEntityAsync(int id);
        Task<MeasurementResult?> GetEntityByIdAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task<MeasurementResultFormData> GetFormDataAsync();
        Task CreateEntityAsync(MeasurementResult measurementResult);
        Task UpdateEntityAsync(MeasurementResult measurementResult);
        Task DeleteEntityAsync(int id);
        Task<MeasurementResultsManageData> GetManageDataAsync();
        Task<MeasurementResultListResponse> ListDataAsync(MeasurementResultListQuery query);
        Task<CsvExportResult> ExportCsvAsync(MeasurementResultListQuery query);
        Task<ServiceResult<ParameterTrendResponse>> GetParameterTrendsAsync(ParameterTrendsQuery query);
        Task<ServiceResult<ParameterTrendResponse>> GetParameterTrendPredictionsAsync(ParameterTrendsQuery query);
        Task<ServiceResult<MeasurementResultDto>> GetDetailAsync(int id);
        Task<ServiceResult<MeasurementResultDto>> CreateAsync(MeasurementResultRequest request, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<MeasurementResultDto>> UpdateAsync(int id, MeasurementResultRequest request, IReadOnlyCollection<string>? modelErrors = null);
        Task<ServiceResult<object?>> DeleteAsync(int id);
    }

    public sealed class MeasurementResultFormData
    {
        public IReadOnlyList<EmissionSource> EmissionSources { get; init; } = Array.Empty<EmissionSource>();
        public IReadOnlyList<Parameter> Parameters { get; init; } = Array.Empty<Parameter>();
    }

    public sealed partial class MeasurementResultsService : IMeasurementResultsService
    {
        private readonly env_analysis_projectContext _context;
        private readonly IUserActivityLogger _activityLogger;
        private readonly IPredictionService _predictionService;

        public MeasurementResultsService(
            env_analysis_projectContext context,
            IUserActivityLogger activityLogger,
            IPredictionService predictionService)
        {
            _context = context;
            _activityLogger = activityLogger;
            _predictionService = predictionService;
        }

        public async Task<IReadOnlyList<MeasurementResult>> GetIndexEntitiesAsync()
        {
            return await _context.MeasurementResult
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .ToListAsync();
        }

        public async Task<MeasurementResult?> GetDetailsEntityAsync(int id)
        {
            return await _context.MeasurementResult
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .FirstOrDefaultAsync(m => m.ResultID == id);
        }

        public async Task<MeasurementResult?> GetEntityByIdAsync(int id)
        {
            return await _context.MeasurementResult.FindAsync(id);
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.MeasurementResult.AnyAsync(e => e.ResultID == id);
        }

        public async Task<MeasurementResultFormData> GetFormDataAsync()
        {
            var emissionSources = await _context.EmissionSource.Where(e => !e.IsDeleted).ToListAsync();
            var parameters = await _context.Set<Parameter>().Where(p => !p.IsDeleted).ToListAsync();
            return new MeasurementResultFormData
            {
                EmissionSources = emissionSources,
                Parameters = parameters
            };
        }

        public async Task CreateEntityAsync(MeasurementResult measurementResult)
        {
            _context.Add(measurementResult);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateEntityAsync(MeasurementResult measurementResult)
        {
            _context.Update(measurementResult);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteEntityAsync(int id)
        {
            var measurementResult = await _context.MeasurementResult.FindAsync(id);
            if (measurementResult != null)
            {
                _context.MeasurementResult.Remove(measurementResult);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<MeasurementResultsManageData> GetManageDataAsync()
        {
            var emissionSources = await _context.EmissionSource
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SourceName)
                .Select(s => new LookupOption
                {
                    Id = s.EmissionSourceID,
                    Label = s.SourceName
                })
                .ToListAsync();

            var parameters = await _context.Parameter
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.ParameterName)
                .Select(p => new ParameterLookup
                {
                    Code = p.ParameterCode,
                    Label = p.ParameterName,
                    Unit = p.Unit,
                    StandardValue = p.StandardValue,
                    Type = ParameterTypeHelper.Normalize(p.Type)
                })
                .ToListAsync();

            return new MeasurementResultsManageData
            {
                EmissionSources = emissionSources,
                Parameters = parameters
            };
        }

        public async Task<MeasurementResultListResponse> ListDataAsync(MeasurementResultListQuery query)
        {
            const int DefaultPageSize = 10;
            const int MaxPageSize = 100;

            var normalizedType = NormalizeTypeFilter(query.Type);
            var trimmedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var normalizedStatus = NormalizeStatusFilter(query.Status);

            var filteredQuery = BuildFilteredQuery(
                normalizedType,
                trimmedSearch,
                query.SourceId,
                query.ParameterCode,
                normalizedStatus,
                query.StartDate,
                query.EndDate);

            var totalItems = await filteredQuery.CountAsync();

            var effectivePageSize = query.Paged
                ? Math.Min(Math.Max(query.PageSize, 1), MaxPageSize)
                : (totalItems == 0 ? DefaultPageSize : totalItems);

            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(totalItems, 0) / Math.Max(effectivePageSize, 1)));
            var currentPage = query.Paged ? Math.Min(Math.Max(query.Page, 1), totalPages) : 1;

            var orderedQuery = filteredQuery
                .OrderByDescending(m => m.MeasurementDate)
                .ThenByDescending(m => m.ResultID);

            var pagedQuery = query.Paged
                ? orderedQuery.Skip((currentPage - 1) * effectivePageSize).Take(effectivePageSize)
                : orderedQuery;

            var items = await pagedQuery
                .Select(m => ToDto(m))
                .ToListAsync();

            var pagination = new PaginationMetadata
            {
                Page = query.Paged ? currentPage : 1,
                PageSize = query.Paged ? effectivePageSize : items.Count,
                TotalItems = totalItems,
                TotalPages = query.Paged ? totalPages : 1
            };

            return new MeasurementResultListResponse
            {
                Items = items,
                Pagination = pagination,
                Summary = await BuildSummaryAsync()
            };
        }

        public async Task<CsvExportResult> ExportCsvAsync(MeasurementResultListQuery query)
        {
            var normalizedType = NormalizeTypeFilter(query.Type);
            var trimmedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var normalizedStatus = NormalizeStatusFilter(query.Status);

            var filteredQuery = BuildFilteredQuery(
                normalizedType,
                trimmedSearch,
                query.SourceId,
                query.ParameterCode,
                normalizedStatus,
                query.StartDate,
                query.EndDate);

            var orderedQuery = filteredQuery
                .OrderByDescending(m => m.MeasurementDate)
                .ThenByDescending(m => m.ResultID);

            var entities = await orderedQuery.ToListAsync();
            var dtos = entities.Select(ToDto).ToList();
            var csv = BuildCsv(dtos);

            return new CsvExportResult
            {
                Bytes = Encoding.UTF8.GetBytes(csv),
                ContentType = "text/csv",
                FileName = $"measurement-results-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };
        }

        public async Task<ServiceResult<ParameterTrendResponse>> GetParameterTrendsAsync(ParameterTrendsQuery query)
        {
            var normalizeResult = NormalizeTrendQuery(query);
            if (!normalizeResult.Success || normalizeResult.Data == null)
            {
                return ServiceResult<ParameterTrendResponse>.Fail(normalizeResult.Message ?? "Invalid parameter trend query.", normalizeResult.Errors);
            }

            var request = normalizeResult.Data;

            var metadataEntries = await _context.Parameter
                .Where(p => request.NormalizedCodes.Contains(p.ParameterCode.ToUpper()) && !p.IsDeleted)
                .Select(p => new ParameterLookup
                {
                    Code = p.ParameterCode,
                    Label = p.ParameterName,
                    Unit = p.Unit,
                    StandardValue = p.StandardValue,
                    Type = ParameterTypeHelper.Normalize(p.Type)
                })
                .ToListAsync();

            if (metadataEntries.Count != request.NormalizedCodes.Count)
            {
                return ServiceResult<ParameterTrendResponse>.Fail("One or more parameters were not found.");
            }

            if (request.IsMultiParameterRequest && metadataEntries.Any(entry => entry.Type != "water"))
            {
                return ServiceResult<ParameterTrendResponse>.Fail("Multi-parameter trends are only available for water parameters.");
            }

            var metadataLookup = metadataEntries
                .ToDictionary(entry => entry.Code.ToUpperInvariant(), entry => entry);

            var normalizedCodeSet = request.NormalizedCodes.ToHashSet();

            var measurementQuery = _context.MeasurementResult
                .AsNoTracking()
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .Where(m => m.ParameterCode != null &&
                            normalizedCodeSet.Contains(m.ParameterCode.ToUpper()) &&
                            m.Value.HasValue);

            if (request.SourceId.HasValue)
            {
                measurementQuery = measurementQuery.Where(m => m.EmissionSourceID == request.SourceId.Value);
            }

            var measurements = await measurementQuery
                .Select(m => new
                {
                    CodeUpper = m.ParameterCode.ToUpper(),
                    Date = m.MeasurementDate == default ? m.EntryDate : m.MeasurementDate,
                    m.Value,
                    SourceName = m.EmissionSource != null ? m.EmissionSource.SourceName : null
                })
                .Where(x => x.Date >= request.EffectiveStart && x.Date < request.EffectiveEndExclusive)
                .OrderBy(x => x.Date)
                .ToListAsync();

            var uniqueDates = measurements
                .Select(entry => entry.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var labels = uniqueDates
                .Select(date => date.ToString("dd MMM yyyy HH:mm"))
                .ToArray();

            IReadOnlyList<ParameterTrendSeries> series;
            List<ParameterTrendPoint> tablePoints;

            if (request.IsMultiParameterRequest)
            {
                var groupedParameters = measurements
                    .GroupBy(entry => entry.CodeUpper)
                    .OrderBy(group => metadataLookup[group.Key].Label, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                series = groupedParameters.Select(group =>
                {
                    var meta = metadataLookup[group.Key];
                    var valueByDate = group
                        .GroupBy(item => item.Date)
                        .ToDictionary(g => g.Key, g => g.Average(x => x.Value));

                    var seriesPoints = uniqueDates.Select(date => new ParameterTrendPoint
                    {
                        Month = date.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Label = date.ToString("dd MMM yyyy HH:mm"),
                        Value = valueByDate.TryGetValue(date, out var value) ? value : null,
                        ParameterName = meta.Label,
                        Unit = meta.Unit
                    }).ToList();

                    return new ParameterTrendSeries
                    {
                        ParameterCode = meta.Code,
                        ParameterName = meta.Label,
                        Unit = meta.Unit,
                        StandardValue = meta.StandardValue,
                        Points = seriesPoints
                    };
                }).ToArray();

                var aggregatedSourceLabel = request.SourceId.HasValue
                    ? measurements.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.SourceName))?.SourceName ?? $"Source #{request.SourceId.Value}"
                    : "All sources";

                tablePoints = groupedParameters
                    .SelectMany(group =>
                    {
                        var meta = metadataLookup[group.Key];
                        return group
                            .GroupBy(entry => entry.Date)
                            .Select(g => new ParameterTrendPoint
                            {
                                Month = g.Key.ToString("yyyy-MM-ddTHH:mm:ss"),
                                Label = g.Key.ToString("dd MMM yyyy HH:mm"),
                                Value = g.Average(x => x.Value),
                                SourceName = aggregatedSourceLabel,
                                ParameterName = meta.Label,
                                Unit = meta.Unit
                            });
                    })
                    .OrderBy(point => point.ParameterName)
                    .ThenBy(point => point.Month)
                    .ToList();
            }
            else
            {
                var groupedSources = measurements
                    .GroupBy(entry => string.IsNullOrWhiteSpace(entry.SourceName) ? "Unknown source" : entry.SourceName!)
                    .ToList();

                var metadata = metadataLookup[request.NormalizedCodes[0]];

                series = groupedSources.Select(group =>
                {
                    var valueByDate = group
                        .GroupBy(item => item.Date)
                        .ToDictionary(g => g.Key, g => g.First().Value);

                    var seriesPoints = uniqueDates.Select(date => new ParameterTrendPoint
                    {
                        Month = date.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Label = date.ToString("dd MMM yyyy HH:mm"),
                        Value = valueByDate.TryGetValue(date, out var value) ? value : null,
                        SourceName = group.Key,
                        ParameterName = metadata.Label,
                        Unit = metadata.Unit
                    }).ToList();

                    return new ParameterTrendSeries
                    {
                        ParameterCode = metadata.Code,
                        ParameterName = group.Key,
                        Unit = metadata.Unit,
                        StandardValue = metadata.StandardValue,
                        Points = seriesPoints
                    };
                }).ToArray();

                tablePoints = measurements.Select(entry => new ParameterTrendPoint
                {
                    Month = entry.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Label = entry.Date.ToString("dd MMM yyyy HH:mm"),
                    Value = entry.Value,
                    SourceName = string.IsNullOrWhiteSpace(entry.SourceName) ? "Unknown source" : entry.SourceName,
                    ParameterName = metadata.Label,
                    Unit = metadata.Unit
                }).ToList();
            }

            var defaultMetadata = metadataLookup.TryGetValue(request.NormalizedCodes[0], out var firstMeta)
                ? firstMeta
                : null;

            var response = new ParameterTrendResponse
            {
                Labels = labels,
                Series = series,
                Table = new TrendTablePage
                {
                    Unit = !request.IsMultiParameterRequest ? defaultMetadata?.Unit : null,
                    Items = tablePoints.ToArray(),
                    Pagination = new PaginationMetadata
                    {
                        Page = 1,
                        PageSize = tablePoints.Count,
                        TotalItems = tablePoints.Count,
                        TotalPages = 1
                    },
                    SourceId = request.SourceId
                },
                StandardValue = !request.IsMultiParameterRequest ? defaultMetadata?.StandardValue : null
            };

            return ServiceResult<ParameterTrendResponse>.Ok(response);
        }

        public async Task<ServiceResult<ParameterTrendResponse>> GetParameterTrendPredictionsAsync(ParameterTrendsQuery query)
        {
            var normalizeResult = NormalizeTrendQuery(query);
            if (!normalizeResult.Success || normalizeResult.Data == null)
            {
                return ServiceResult<ParameterTrendResponse>.Fail(normalizeResult.Message ?? "Invalid parameter trend query.", normalizeResult.Errors);
            }

            var request = normalizeResult.Data;

            var metadataEntries = await _context.Parameter
                .Where(p => request.NormalizedCodes.Contains(p.ParameterCode.ToUpper()) && !p.IsDeleted)
                .Select(p => new ParameterLookup
                {
                    Code = p.ParameterCode,
                    Label = p.ParameterName,
                    Unit = p.Unit,
                    StandardValue = p.StandardValue,
                    Type = ParameterTypeHelper.Normalize(p.Type)
                })
                .ToListAsync();

            if (metadataEntries.Count != request.NormalizedCodes.Count)
            {
                return ServiceResult<ParameterTrendResponse>.Fail("One or more parameters were not found.");
            }

            if (request.IsMultiParameterRequest && metadataEntries.Any(entry => entry.Type != "water"))
            {
                return ServiceResult<ParameterTrendResponse>.Fail("Multi-parameter trends are only available for water parameters.");
            }

            var metadataLookup = metadataEntries
                .ToDictionary(entry => entry.Code.ToUpperInvariant(), entry => entry);

            var normalizedCodeSet = request.NormalizedCodes.ToHashSet();

            var measurementQuery = _context.MeasurementResult
                .AsNoTracking()
                .Include(m => m.Parameter)
                .Where(m => m.IsApproved &&
                            m.ParameterCode != null &&
                            normalizedCodeSet.Contains(m.ParameterCode.ToUpper()) &&
                            m.Value.HasValue);

            if (request.SourceId.HasValue)
            {
                measurementQuery = measurementQuery.Where(m => m.EmissionSourceID == request.SourceId.Value);
            }

            var measurements = await measurementQuery
                .Select(m => new
                {
                    CodeUpper = m.ParameterCode.ToUpper(),
                    Date = m.MeasurementDate == default ? m.EntryDate : m.MeasurementDate,
                    m.Value
                })
                .Where(x => x.Date >= request.EffectiveStart && x.Date < request.EffectiveEndExclusive)
                .OrderBy(x => x.Date)
                .ToListAsync();

            if (measurements.Count == 0)
            {
                return ServiceResult<ParameterTrendResponse>.Fail("No approved measurements found for the selected range.");
            }

            var uniqueDates = measurements
                .Select(entry => entry.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            if (uniqueDates.Count == 0)
            {
                return ServiceResult<ParameterTrendResponse>.Fail("No approved measurements found for the selected range.");
            }

            var modelInput = measurements.Select(entry => new PollutionData
            {
                Parameter = entry.CodeUpper,
                Value = entry.Value.HasValue ? (float)entry.Value.Value : 0f,
                MeasurementDate = entry.Date.ToString("yyyy-MM-ddTHH:mm:ss")
            }).ToList();

            var predictionResult = _predictionService.PredictFromData(modelInput);
            if (predictionResult.Rows.Count == 0 && predictionResult.FutureForecasts.Count == 0)
            {
                return ServiceResult<ParameterTrendResponse>.Fail("Not enough data to run the prediction model.");
            }

            var historicalPredictionLookup = predictionResult.Rows
                .GroupBy(row => row.ParameterDisplayName?.ToUpperInvariant() ?? string.Empty)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .GroupBy(row => row.MeasurementDate)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Average(item => (double)item.PredictedValue)));

            var forecastDates = predictionResult.FutureForecasts
                .SelectMany(entry => entry.Value ?? new List<FutureForecast>())
                .Select(entry => entry.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();

            var combinedTimeline = new List<(DateTime Date, string Label, string Kind)>();
            foreach (var date in uniqueDates)
            {
                combinedTimeline.Add((date, date.ToString("dd MMM yyyy HH:mm"), "historical"));
            }
            foreach (var date in forecastDates)
            {
                combinedTimeline.Add((date, date.ToString("MMM yyyy"), "future"));
            }

            var combinedLabels = combinedTimeline
                .OrderBy(entry => entry.Date)
                .GroupBy(entry => new { entry.Date, entry.Label })
                .Select(group => group.First())
                .ToList();

            var labels = combinedLabels.Select(entry => entry.Label).ToArray();
            var series = new List<ParameterTrendSeries>();

            foreach (var codeEntry in request.NormalizedCodes)
            {
                var meta = metadataLookup[codeEntry];
                var datePredictions = historicalPredictionLookup.TryGetValue(codeEntry, out var byDate)
                    ? byDate
                    : new Dictionary<DateTime, double>();

                var historicalLookupByLabel = datePredictions
                    .ToDictionary(entry => entry.Key.ToString("dd MMM yyyy HH:mm"), entry => entry.Value);

                if (historicalLookupByLabel.Count > 0)
                {
                    var points = combinedLabels.Select(entry => new ParameterTrendPoint
                    {
                        Month = entry.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Label = entry.Label,
                        Value = historicalLookupByLabel.TryGetValue(entry.Label, out var value) ? value : null,
                        ParameterName = meta.Label,
                        Unit = meta.Unit
                    }).ToList();

                    series.Add(new ParameterTrendSeries
                    {
                        ParameterCode = meta.Code,
                        ParameterName = meta.Label,
                        Unit = meta.Unit,
                        StandardValue = meta.StandardValue,
                        Points = points,
                        IsForecast = true,
                        ForecastKind = "historical"
                    });
                }

                var forecasts = predictionResult.FutureForecasts.TryGetValue(codeEntry, out var list)
                    ? list
                    : new List<FutureForecast>();

                var forecastLookupByLabel = forecasts
                    .GroupBy(item => item.Date)
                    .ToDictionary(group => group.Key.ToString("MMM yyyy"), group => group.Average(item => (double)item.Value));

                if (forecastLookupByLabel.Count > 0)
                {
                    var points = combinedLabels.Select(entry => new ParameterTrendPoint
                    {
                        Month = entry.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Label = entry.Label,
                        Value = forecastLookupByLabel.TryGetValue(entry.Label, out var value) ? value : null,
                        ParameterName = meta.Label,
                        Unit = meta.Unit
                    }).ToList();

                    series.Add(new ParameterTrendSeries
                    {
                        ParameterCode = meta.Code,
                        ParameterName = meta.Label,
                        Unit = meta.Unit,
                        StandardValue = meta.StandardValue,
                        Points = points,
                        IsForecast = true,
                        ForecastKind = "future"
                    });
                }
            }

            return ServiceResult<ParameterTrendResponse>.Ok(new ParameterTrendResponse
            {
                Labels = labels,
                Series = series,
                Table = new TrendTablePage
                {
                    Items = Array.Empty<ParameterTrendPoint>(),
                    Pagination = new PaginationMetadata
                    {
                        Page = 1,
                        PageSize = 0,
                        TotalItems = 0,
                        TotalPages = 1
                    }
                },
                StandardValue = null
            });
        }

        public async Task<ServiceResult<MeasurementResultDto>> GetDetailAsync(int id)
        {
            var measurement = await _context.MeasurementResult
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .FirstOrDefaultAsync(m => m.ResultID == id);

            if (measurement == null)
            {
                return ServiceResult<MeasurementResultDto>.Fail("Measurement result not found.");
            }

            return ServiceResult<MeasurementResultDto>.Ok(ToDto(measurement));
        }

        public async Task<ServiceResult<MeasurementResultDto>> CreateAsync(MeasurementResultRequest request, IReadOnlyCollection<string>? modelErrors = null)
        {
            var validationErrors = MeasurementResultValidator.Validate(request).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<MeasurementResultDto>.Fail("Invalid measurement result payload.", validationErrors);
            }

            if (!await _context.EmissionSource.AnyAsync(s => s.EmissionSourceID == request.EmissionSourceId && !s.IsDeleted))
            {
                return ServiceResult<MeasurementResultDto>.Fail("Emission source not found.");
            }

            if (!await _context.Parameter.AnyAsync(p => p.ParameterCode == request.ParameterCode && !p.IsDeleted))
            {
                return ServiceResult<MeasurementResultDto>.Fail("Parameter not found.");
            }

            var entity = new MeasurementResult
            {
                EmissionSourceID = request.EmissionSourceId,
                ParameterCode = request.ParameterCode,
                MeasurementDate = request.MeasurementDate,
                Value = request.Value,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
                EntryDate = DateTime.UtcNow,
                Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                IsApproved = request.IsApproved,
                ApprovedAt = request.IsApproved ? request.ApprovedAt : null
            };

            _context.MeasurementResult.Add(entity);
            await _context.SaveChangesAsync();
            await LogAsync("MeasurementResult.Create", entity.ResultID.ToString(), $"Created measurement result for parameter {entity.ParameterCode}", new { entity.EmissionSourceID, entity.Value });

            var dto = await _context.MeasurementResult
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .Where(m => m.ResultID == entity.ResultID)
                .Select(m => ToDto(m))
                .FirstAsync();

            return ServiceResult<MeasurementResultDto>.Ok(dto, "Measurement result created successfully.");
        }

        public async Task<ServiceResult<MeasurementResultDto>> UpdateAsync(int id, MeasurementResultRequest request, IReadOnlyCollection<string>? modelErrors = null)
        {
            var entity = await _context.MeasurementResult.FindAsync(id);
            if (entity == null)
            {
                return ServiceResult<MeasurementResultDto>.Fail("Measurement result not found.");
            }

            request.MeasurementDate = entity.MeasurementDate;
            request.ApprovedAt = request.IsApproved ? entity.ApprovedAt ?? DateTime.UtcNow : null;

            var validationErrors = MeasurementResultValidator.Validate(request).ToList();
            if (modelErrors != null && modelErrors.Count > 0)
            {
                validationErrors.AddRange(modelErrors);
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<MeasurementResultDto>.Fail("Invalid measurement result payload.", validationErrors);
            }

            if (!await _context.EmissionSource.AnyAsync(s => s.EmissionSourceID == request.EmissionSourceId && !s.IsDeleted))
            {
                return ServiceResult<MeasurementResultDto>.Fail("Emission source not found.");
            }

            if (!await _context.Parameter.AnyAsync(p => p.ParameterCode == request.ParameterCode && !p.IsDeleted))
            {
                return ServiceResult<MeasurementResultDto>.Fail("Parameter not found.");
            }

            entity.EmissionSourceID = request.EmissionSourceId;
            entity.ParameterCode = request.ParameterCode;
            entity.Value = request.Value;
            entity.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
            entity.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
            entity.IsApproved = request.IsApproved;
            entity.ApprovedAt = request.ApprovedAt;

            await _context.SaveChangesAsync();
            await LogAsync("MeasurementResult.Update", entity.ResultID.ToString(), $"Updated measurement result for parameter {entity.ParameterCode}", new { entity.EmissionSourceID, entity.Value });

            var dto = await _context.MeasurementResult
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .Where(m => m.ResultID == entity.ResultID)
                .Select(m => ToDto(m))
                .FirstAsync();

            return ServiceResult<MeasurementResultDto>.Ok(dto, "Measurement result updated successfully.");
        }

        public async Task<ServiceResult<object?>> DeleteAsync(int id)
        {
            var entity = await _context.MeasurementResult.FindAsync(id);
            if (entity == null)
            {
                return ServiceResult<object?>.Fail("Measurement result not found.");
            }

            _context.MeasurementResult.Remove(entity);
            await _context.SaveChangesAsync();
            await LogAsync("MeasurementResult.Delete", id.ToString(), $"Deleted measurement result for parameter {entity.ParameterCode}", new { entity.EmissionSourceID });
            return ServiceResult<object?>.Ok(null, "Measurement result deleted successfully.");
        }

        private static string? NormalizeTypeFilter(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmed = input.Trim();
            if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return ParameterTypeHelper.IsValid(trimmed)
                ? ParameterTypeHelper.Normalize(trimmed)
                : null;
        }

        private static string NormalizeType(string? input)
        {
            return ParameterTypeHelper.Normalize(input);
        }

        private static string? NormalizeStatusFilter(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim().ToLowerInvariant();
            return normalized is "approved" or "pending" ? normalized : null;
        }

        private IQueryable<MeasurementResult> BuildFilteredQuery(
            string? normalizedType,
            string? trimmedSearch,
            int? sourceId,
            string? parameterCode,
            string? normalizedStatus,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _context.MeasurementResult
                .AsNoTracking()
                .Include(m => m.EmissionSource)
                .Include(m => m.Parameter)
                .AsQueryable();

            if (!string.IsNullOrEmpty(normalizedType))
            {
                query = query.Where(m => m.Parameter != null && m.Parameter.Type == normalizedType);
            }

            if (sourceId.HasValue)
            {
                query = query.Where(m => m.EmissionSourceID == sourceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parameterCode))
            {
                var normalizedParameter = parameterCode.Trim().ToUpperInvariant();
                query = query.Where(m => m.ParameterCode != null &&
                                         m.ParameterCode.ToUpper() == normalizedParameter);
            }

            if (!string.IsNullOrEmpty(normalizedStatus))
            {
                var isApprovedFilter = normalizedStatus == "approved";
                query = query.Where(m => m.IsApproved == isApprovedFilter);
            }

            if (startDate.HasValue)
            {
                var normalizedStart = startDate.Value.Date;
                query = query.Where(m => m.MeasurementDate >= normalizedStart);
            }

            if (endDate.HasValue)
            {
                var normalizedEndExclusive = endDate.Value.Date.AddDays(1);
                query = query.Where(m => m.MeasurementDate < normalizedEndExclusive);
            }

            if (!string.IsNullOrWhiteSpace(trimmedSearch))
            {
                var likePattern = $"%{trimmedSearch}%";
                var normalizedSearch = trimmedSearch.ToLowerInvariant();
                var statusMatch = normalizedSearch switch
                {
                    "approved" => "approved",
                    "pending" => "pending",
                    _ => null
                };

                query = query.Where(m =>
                    (m.EmissionSource != null && EF.Functions.Like(m.EmissionSource.SourceName ?? string.Empty, likePattern)) ||
                    EF.Functions.Like(m.Parameter.ParameterName ?? string.Empty, likePattern) ||
                    EF.Functions.Like(m.ParameterCode ?? string.Empty, likePattern) ||
                    (statusMatch == "approved" && m.IsApproved) ||
                    (statusMatch == "pending" && !m.IsApproved));
            }

            return query;
        }

        private static MeasurementResultDto ToDto(MeasurementResult measurement)
        {
            return new MeasurementResultDto
            {
                ResultID = measurement.ResultID,
                Type = NormalizeType(measurement.Parameter?.Type),
                EmissionSourceID = measurement.EmissionSourceID,
                EmissionSourceName = measurement.EmissionSource?.SourceName ?? $"Source #{measurement.EmissionSourceID}",
                ParameterCode = measurement.ParameterCode,
                ParameterName = measurement.Parameter?.ParameterName ?? measurement.ParameterCode,
                MeasurementDate = measurement.MeasurementDate,
                Value = measurement.Value,
                Unit = measurement.Unit,
                Remark = measurement.Remark,
                IsApproved = measurement.IsApproved,
                ApprovedAt = measurement.ApprovedAt
            };
        }

        private static string BuildCsv(IEnumerable<MeasurementResultDto> items)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Type,Source,Parameter,Value,Unit,Measurement Date,Status,Approved At,Remark");
            foreach (var item in items)
            {
                var valueText = item.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                var measurementDate = item.MeasurementDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
                var statusText = item.IsApproved ? "Approved" : "Pending";
                var approvedAt = item.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
                var remark = item.Remark ?? string.Empty;

                var columns = new[]
                {
                    EscapeCsv(item.Type),
                    EscapeCsv(item.EmissionSourceName),
                    EscapeCsv(item.ParameterName),
                    EscapeCsv(valueText),
                    EscapeCsv(item.Unit ?? string.Empty),
                    EscapeCsv(measurementDate),
                    EscapeCsv(statusText),
                    EscapeCsv(approvedAt),
                    EscapeCsv(remark)
                };

                builder.AppendLine(string.Join(",", columns));
            }

            return builder.ToString();
        }

        private static string EscapeCsv(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "\"\"";
            }

            var sanitized = input.Replace("\"", "\"\"");
            return $"\"{sanitized}\"";
        }

        private async Task<MeasurementResultSummary> BuildSummaryAsync()
        {
            var typeCounts = await _context.MeasurementResult
                .AsNoTracking()
                .Include(m => m.Parameter)
                .GroupBy(m => m.Parameter != null ? m.Parameter.Type : null)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var summary = new MeasurementResultSummary();
            foreach (var entry in typeCounts)
            {
                summary.All += entry.Count;
                var normalized = NormalizeType(entry.Type);
                if (normalized == "air")
                {
                    summary.Air += entry.Count;
                }
                else
                {
                    summary.Water += entry.Count;
                }
            }

            return summary;
        }

        private static bool TryParseMonth(string? input, out DateTime month)
        {
            month = default;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            return DateTime.TryParseExact(
                input.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out month);
        }

        private ServiceResult<NormalizedTrendQuery> NormalizeTrendQuery(ParameterTrendsQuery query)
        {
            var normalizedCodes = new List<string>();
            if (query.Codes != null)
            {
                foreach (var entry in query.Codes)
                {
                    if (!string.IsNullOrWhiteSpace(entry))
                    {
                        normalizedCodes.Add(entry.Trim().ToUpperInvariant());
                    }
                }
            }

            if (normalizedCodes.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(query.Code))
                {
                    return ServiceResult<NormalizedTrendQuery>.Fail("Parameter code is required.");
                }
                normalizedCodes.Add(query.Code.Trim().ToUpperInvariant());
            }

            normalizedCodes = normalizedCodes.Distinct().ToList();
            var isMultiParameterRequest = normalizedCodes.Count > 1;

            const int MaxMonths = 36;
            var now = DateTime.UtcNow;
            var defaultEnd = new DateTime(now.Year, now.Month, 1);
            var defaultStart = defaultEnd.AddMonths(-11);

            DateTime? parsedStart = null;
            DateTime? parsedEnd = null;

            if (!string.IsNullOrWhiteSpace(query.StartMonth))
            {
                if (!TryParseMonth(query.StartMonth, out var tmpStart))
                {
                    return ServiceResult<NormalizedTrendQuery>.Fail("Invalid start month format. Use yyyy-MM.");
                }
                parsedStart = tmpStart;
            }

            if (!string.IsNullOrWhiteSpace(query.EndMonth))
            {
                if (!TryParseMonth(query.EndMonth, out var tmpEnd))
                {
                    return ServiceResult<NormalizedTrendQuery>.Fail("Invalid end month format. Use yyyy-MM.");
                }
                parsedEnd = tmpEnd;
            }

            var rangeStart = new DateTime((parsedStart ?? parsedEnd ?? defaultStart).Year, (parsedStart ?? parsedEnd ?? defaultStart).Month, 1);
            var rangeEnd = new DateTime((parsedEnd ?? parsedStart ?? defaultEnd).Year, (parsedEnd ?? parsedStart ?? defaultEnd).Month, 1);

            if (rangeEnd < rangeStart)
            {
                return ServiceResult<NormalizedTrendQuery>.Fail("End month must be greater than or equal to start month.");
            }

            var months = new List<DateTime>();
            var cursor = rangeStart;
            while (cursor <= rangeEnd && months.Count < MaxMonths)
            {
                months.Add(cursor);
                cursor = cursor.AddMonths(1);
            }

            if (months.Count == 0)
            {
                months.Add(rangeStart);
            }

            return ServiceResult<NormalizedTrendQuery>.Ok(new NormalizedTrendQuery
            {
                NormalizedCodes = normalizedCodes,
                IsMultiParameterRequest = isMultiParameterRequest,
                EffectiveStart = months.First(),
                EffectiveEndExclusive = months.Last().AddMonths(1),
                SourceId = query.SourceId
            });
        }

        private Task LogAsync(string action, string entityId, string description, object? metadata = null) =>
            _activityLogger.LogAsync(action, "MeasurementResult", entityId, description, metadata);

        private sealed class NormalizedTrendQuery
        {
            public List<string> NormalizedCodes { get; init; } = new List<string>();
            public bool IsMultiParameterRequest { get; init; }
            public DateTime EffectiveStart { get; init; }
            public DateTime EffectiveEndExclusive { get; init; }
            public int? SourceId { get; init; }
        }
    }
}
