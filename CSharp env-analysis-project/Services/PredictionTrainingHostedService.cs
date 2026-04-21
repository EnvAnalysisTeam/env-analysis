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

        public PredictionTrainingHostedService(IServiceProvider serviceProvider, ILogger<PredictionTrainingHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run once at startup (not repeating). If you need periodic work, loop with delays.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
                // If the service has an initialization method, call it here.
                // await predictionService.InitializeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while initializing prediction service.");
            }

            await Task.CompletedTask;
        }
    }
}