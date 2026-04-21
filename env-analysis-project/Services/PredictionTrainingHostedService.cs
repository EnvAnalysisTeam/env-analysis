using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace env_analysis_project.Services
{
    public class PredictionTrainingHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PredictionTrainingHostedService> _logger;

        public PredictionTrainingHostedService(
            IServiceProvider serviceProvider,
            ILogger<PredictionTrainingHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run once at startup. The scoped prediction service is resolved safely here.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                _ = scope.ServiceProvider.GetRequiredService<IPredictionService>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while initializing prediction service.");
            }

            await Task.CompletedTask;
        }
    }
}
