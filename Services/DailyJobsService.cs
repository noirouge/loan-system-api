namespace LoanSystemAPI.Services
{
    // WHEN THE API STARTS AND THEN ONCE A DAY, RUNS EVERY REGISTERED IDailyJob. CHECKING EVERY DAY, AND NOT ONLY ON THE 1ST,
    // MEANS THAT IF THE 1ST FAILED FOR ANY REASON THE NEXT CHECK CATCHES UP (D-054)
    public class DailyJobsService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyJobsService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IConfiguration _configuration;

        public DailyJobsService(IServiceScopeFactory scopeFactory, ILogger<DailyJobsService> logger, TimeProvider timeProvider, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timeProvider = timeProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // THE INTEGRATION TESTS TURN IT OFF AND RUN THE JOBS BY HAND
            if (!_configuration.GetValue("Jobs:Enabled", true))
                return;

            using var timer = new PeriodicTimer(TimeSpan.FromDays(1), _timeProvider);
            try
            {
                do
                {
                    await RunJobsAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        // AN ERROR IN ONE JOB IS LOGGED AND STOPS NEITHER THE OTHER JOBS NOR THE API
        private async Task RunJobsAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                foreach (var job in scope.ServiceProvider.GetServices<IDailyJob>())
                {
                    try
                    {
                        await job.RunAsync(stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(ex, "DAILY JOB {JobName} ERROR", job.Name);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "DAILY JOBS ERROR");
            }
        }
    }
}
