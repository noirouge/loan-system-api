using LoanSystemAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Services
{
    // DELETES THE REFRESH TOKENS THAT ALREADY EXPIRED: THEY CAN NO LONGER OPEN A SESSION (D-032)
    public class RefreshTokenCleanupJob : IDailyJob
    {
        public const string JobName = "refresh-token-cleanup";

        private readonly AppDbContext _dbContext;
        private readonly JobRunner _jobRunner;
        private readonly TimeProvider _timeProvider;

        public RefreshTokenCleanupJob(AppDbContext dbContext, JobRunner jobRunner, TimeProvider timeProvider)
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
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                // replaced_by HAS ON DELETE SET NULL: DELETING A TOKEN THAT REPLACED ANOTHER ONE DOES NOT BREAK THE FOREIGN KEY
                var deletedTokens = await _dbContext.RefreshTokens
                    .Where(t => t.ExpiresAt <= now)
                    .ExecuteDeleteAsync(token);

                return new JobRunCounters { Processed = deletedTokens };
            }, cancellationToken);
        }
    }
}
