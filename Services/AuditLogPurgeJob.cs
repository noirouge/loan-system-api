using LoanSystemAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Services
{
    // DELETES THE AUDIT LOGS OLDER THAN ONE YEAR (D-032)
    public class AuditLogPurgeJob : IDailyJob
    {
        public const string JobName = "audit-log-purge";

        private readonly AppDbContext _dbContext;
        private readonly JobRunner _jobRunner;
        private readonly TimeProvider _timeProvider;

        public AuditLogPurgeJob(AppDbContext dbContext, JobRunner jobRunner, TimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _jobRunner = jobRunner;
            _timeProvider = timeProvider;
        }

        public string Name => JobName;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _jobRunner.RunAsync(JobName, null, async token =>
            {
                var oneYearAgo = _timeProvider.GetUtcNow().UtcDateTime.AddYears(-1);

                var deletedLogs = await _dbContext.AuditLogs
                    .Where(a => a.CreatedDate < oneYearAgo)
                    .ExecuteDeleteAsync(token);

                return new JobRunCounters { Processed = deletedLogs };
            }, cancellationToken);
        }
    }
}
