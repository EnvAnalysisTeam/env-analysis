using System.Linq;
using System.Threading.Tasks;
using env_analysis_project.Contracts.Common;
using env_analysis_project.Data;
using env_analysis_project.Models;
using Microsoft.EntityFrameworkCore;

namespace env_analysis_project.Services
{
    public interface IDashboardLookupService
    {
        Task<DashboardLookupData> GetLookupDataAsync();
    }

    public sealed class DashboardLookupService : IDashboardLookupService
    {
        private readonly env_analysis_projectContext _context;

        public DashboardLookupService(env_analysis_projectContext context)
        {
            _context = context;
        }

        public async Task<DashboardLookupData> GetLookupDataAsync()
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
                .Select(p => new ParameterOption
                {
                    Code = p.ParameterCode,
                    Label = p.ParameterName,
                    Unit = p.Unit,
                    StandardValue = p.StandardValue,
                    Type = ParameterTypeHelper.Normalize(p.Type)
                })
                .ToListAsync();

            return new DashboardLookupData
            {
                EmissionSources = emissionSources,
                Parameters = parameters
            };
        }
    }
}
