namespace LoanSystemAPI.Services
{
    // A JOB THAT DailyJobsService RUNS WHEN THE API STARTS AND THEN ONCE A DAY. EACH JOB DECIDES WHAT IS PENDING
    // (FOR EXAMPLE, A PERIOD WITHOUT ITS CHARGES) AND RUNS IT THROUGH JobRunner
    public interface IDailyJob
    {
        string Name { get; }
        Task RunAsync(CancellationToken cancellationToken);
    }
}
