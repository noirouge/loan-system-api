using LoanSystemAPI.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Services
{
    // RUNS THE WORK OF A JOB WHILE HOLDING A POSTGRES ADVISORY LOCK FOR THAT JOB, SO ONLY ONE INSTANCE RUNS IT AT A TIME (D-033)
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

        // RETURNS FALSE WITHOUT DOING ANYTHING IF ANOTHER INSTANCE IS ALREADY RUNNING THIS JOB
        public async Task<bool> RunAsync(string jobName, Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            // THE ADVISORY LOCK BELONGS TO THE DATABASE SESSION, SO THE SAME CONNECTION STAYS OPEN UNTIL IT IS RELEASED
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                if (!await TryLockAsync(jobName, cancellationToken))
                {
                    _logger.LogInformation("JOB {JobName} IS ALREADY RUNNING IN ANOTHER INSTANCE", jobName);
                    return false;
                }

                try
                {
                    await work(cancellationToken);
                    return true;
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

        private async Task<bool> TryLockAsync(string jobName, CancellationToken cancellationToken)
        {
            var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockKey, hashtext(@jobName))", connection);
            command.Parameters.AddWithValue("lockKey", JobLockKey);
            command.Parameters.AddWithValue("jobName", jobName);

            return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
    }
}
