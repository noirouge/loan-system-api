using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Services
{
    // RUNS THE WORK OF A JOB WHILE HOLDING A POSTGRES ADVISORY LOCK FOR THAT JOB, SO ONLY ONE INSTANCE RUNS IT AT A TIME,
    // AND LEAVES ONE ROW IN job_runs PER RUN WITH ITS RESULT (D-033)
    public class JobRunner
    {
        // FIRST KEY OF THE LOCK; THE SECOND ONE IS THE HASH OF THE JOB NAME. THE CASH LOCK (1001) USES ANOTHER KEY SPACE
        private const int JobLockKey = 2001;

        private readonly AppDbContext _dbContext;
        private readonly ILogger<JobRunner> _logger;

        public JobRunner(AppDbContext dbContext, ILogger<JobRunner> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // RETURNS NULL WITHOUT DOING ANYTHING IF ANOTHER INSTANCE IS ALREADY RUNNING THIS JOB,
        // OR IF THE PERIOD ALREADY ENDED IN SUCCESS (D-061). A JOB WITHOUT PERIOD CAN SUCCEED MANY TIMES
        public async Task<JobRun?> RunAsync(string jobName, DateOnly? period, Func<CancellationToken, Task<JobRunCounters>> work, CancellationToken cancellationToken = default)
        {
            // THE ADVISORY LOCK BELONGS TO THE DATABASE SESSION, SO THE SAME CONNECTION STAYS OPEN UNTIL IT IS RELEASED
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                if (!await TryLockAsync(jobName, cancellationToken))
                {
                    _logger.LogInformation("JOB {JobName} IS ALREADY RUNNING IN ANOTHER INSTANCE", jobName);
                    return null;
                }

                try
                {
                    await MarkOrphanRunsAsFailedAsync(jobName, cancellationToken);

                    // A PERIOD THAT ALREADY ENDED IN SUCCESS IS NOT RUN AGAIN: ux_job_runs_success ALLOWS ONLY ONE SUCCESS PER PERIOD
                    if (period != null && await _dbContext.JobRuns.AnyAsync(j => j.JobName == jobName && j.Period == period && j.Status == JobRunStatus.SUCCESS, cancellationToken))
                        return null;

                    // THE RUNNING ROW IS SAVED IN ITS OWN TRANSACTION, BEFORE THE WORK: IF THE WORK FAILS,
                    // ITS ROLLBACK CANNOT TAKE AWAY THE EVIDENCE THAT THE RUN WAS ATTEMPTED
                    var jobRun = new JobRun
                    {
                        Id = Guid.NewGuid(),
                        JobName = jobName,
                        Period = period,
                        Status = JobRunStatus.RUNNING,
                        StartedAt = DateTime.UtcNow,
                    };
                    await _dbContext.JobRuns.AddAsync(jobRun, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _dbContext.Entry(jobRun).State = EntityState.Detached;

                    try
                    {
                        var counters = await work(cancellationToken);
                        jobRun.Processed = counters.Processed;
                        jobRun.Skipped = counters.Skipped;
                        jobRun.Failed = counters.Failed;
                        jobRun.Status = StatusFor(counters);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "JOB {JobName} ERROR", jobName);
                        jobRun.Status = JobRunStatus.FAILED;
                        jobRun.ErrorMessage = ex.Message;
                    }
                    jobRun.FinishedAt = DateTime.UtcNow;

                    // WRITTEN WITHOUT THE CHANGE TRACKER, SO WHATEVER THE WORK LEFT PENDING IN THE CONTEXT IS NOT SAVED WITH IT
                    await _dbContext.JobRuns
                        .Where(j => j.Id == jobRun.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(j => j.Status, jobRun.Status)
                            .SetProperty(j => j.Processed, jobRun.Processed)
                            .SetProperty(j => j.Skipped, jobRun.Skipped)
                            .SetProperty(j => j.Failed, jobRun.Failed)
                            .SetProperty(j => j.ErrorMessage, jobRun.ErrorMessage)
                            .SetProperty(j => j.FinishedAt, jobRun.FinishedAt), CancellationToken.None);

                    return jobRun;
                }
                finally
                {
                    await _dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({JobLockKey}, hashtext({jobName}))", CancellationToken.None);
                }
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }

        // SUCCESS WHEN NOTHING FAILED; PARTIAL WHEN SOMETHING FAILED BUT SOMETHING ELSE WAS PROCESSED OR SKIPPED; FAILED WHEN NOTHING WENT WELL
        public static JobRunStatus StatusFor(JobRunCounters counters)
        {
            if (counters.Failed == 0)
                return JobRunStatus.SUCCESS;

            if (counters.Processed + counters.Skipped > 0)
                return JobRunStatus.PARTIAL;

            return JobRunStatus.FAILED;
        }

        private async Task<bool> TryLockAsync(string jobName, CancellationToken cancellationToken)
        {
            var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockKey, hashtext(@jobName))", connection);
            command.Parameters.AddWithValue("lockKey", JobLockKey);
            command.Parameters.AddWithValue("jobName", jobName);

            return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        // WITH THE LOCK TAKEN NO OTHER INSTANCE CAN BE RUNNING THIS JOB, SO A RUNNING ROW WAS LEFT BY A PROCESS THAT DIED HALFWAY
        private async Task MarkOrphanRunsAsFailedAsync(string jobName, CancellationToken cancellationToken)
        {
            var orphanRuns = await _dbContext.JobRuns
                .Where(j => j.JobName == jobName && j.Status == JobRunStatus.RUNNING)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobRunStatus.FAILED)
                    .SetProperty(j => j.FinishedAt, (DateTime?)DateTime.UtcNow)
                    .SetProperty(j => j.ErrorMessage, "THE RUN WAS INTERRUPTED: THE PROCESS STOPPED BEFORE IT FINISHED"), cancellationToken);

            if (orphanRuns > 0)
                _logger.LogWarning("JOB {JobName}: {OrphanRuns} ORPHAN RUNS MARKED AS FAILED", jobName, orphanRuns);
        }
    }
}
