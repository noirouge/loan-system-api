using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystemAPI.IntegrationTests.Jobs
{
    [Collection(ApiCollection.Name)]
    public class MaintenanceJobsTests : IntegrationTest
    {
        private static readonly DateTime Now = FakeTimeProvider.DefaultUtcNow.UtcDateTime;

        public MaintenanceJobsTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task RunDailyJobAsync(string jobName)
        {
            using var scope = Factory.Services.CreateScope();
            var job = scope.ServiceProvider.GetServices<IDailyJob>().Single(j => j.Name == jobName);
            await job.RunAsync(CancellationToken.None);
        }

        // ONE BY ONE, SO EACH ROW EXISTS BEFORE ANOTHER ONE POINTS TO IT
        private async Task SaveAsync(params object[] entities)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var entity in entities)
            {
                dbContext.Add(entity);
                await dbContext.SaveChangesAsync();
            }
        }

        private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = Factory.Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        private static RefreshToken NewRefreshToken(DateTime expiresAt, Guid? replacedBy = null)
        {
            return new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = LoanApiFactory.AdminId,
                TokenHash = AuthTokenService.HashRefreshToken(Guid.NewGuid().ToString()),
                ExpiresAt = expiresAt,
                ReplacedBy = replacedBy,
                CreatedDate = Now.AddDays(-8),
            };
        }

        private static AuditLog NewAuditLog(DateTime createdDate)
        {
            return new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = AuditAction.LOGIN,
                UserId = LoanApiFactory.AdminId,
                AttemptedUser = TestUsers.AdminUsername,
                CreatedDate = createdDate,
            };
        }

        [Fact]
        public void BothMaintenanceJobs_AreRegisteredForTheDailyCheck()
        {
            using var scope = Factory.Services.CreateScope();

            var jobNames = scope.ServiceProvider.GetServices<IDailyJob>().Select(j => j.Name).ToList();

            Assert.Contains(RefreshTokenCleanupJob.JobName, jobNames);
            Assert.Contains(AuditLogPurgeJob.JobName, jobNames);
        }

        [Fact]
        public async Task TheCleanup_DeletesTheExpiredRefreshTokens_AndKeepsTheValidOnes()
        {
            var expiredReplacement = NewRefreshToken(Now.AddMinutes(-1));
            var expiredReplaced = NewRefreshToken(Now.AddDays(-1), replacedBy: expiredReplacement.Id);
            var expiringNow = NewRefreshToken(Now);
            var valid = NewRefreshToken(Now.AddDays(1));
            // UNUSUAL, BUT IT PROVES THAT DELETING A TOKEN LEAVES replaced_by EMPTY INSTEAD OF BREAKING THE FOREIGN KEY
            var validPointingToAnExpiredOne = NewRefreshToken(Now.AddDays(2), replacedBy: expiringNow.Id);
            await SaveAsync(expiredReplacement, expiredReplaced, expiringNow, valid, validPointingToAnExpiredOne);

            await RunDailyJobAsync(RefreshTokenCleanupJob.JobName);

            var remaining = await QueryAsync(db => db.RefreshTokens.AsNoTracking().ToListAsync());
            Assert.Equal(2, remaining.Count);
            Assert.Contains(remaining, t => t.Id == valid.Id);
            Assert.Null(Assert.Single(remaining, t => t.Id == validPointingToAnExpiredOne.Id).ReplacedBy);
            var jobRun = Assert.Single(await QueryAsync(db => db.JobRuns.AsNoTracking().ToListAsync()));
            Assert.Equal(RefreshTokenCleanupJob.JobName, jobRun.JobName);
            Assert.Null(jobRun.Period);
            Assert.Equal(JobRunStatus.SUCCESS, jobRun.Status);
            Assert.Equal(3, jobRun.Processed);
        }

        [Fact]
        public async Task TheCleanup_CanSucceedEveryDay_EvenWithNothingToDelete()
        {
            await RunDailyJobAsync(RefreshTokenCleanupJob.JobName);
            await RunDailyJobAsync(RefreshTokenCleanupJob.JobName);

            var jobRuns = await QueryAsync(db => db.JobRuns.AsNoTracking().ToListAsync());
            Assert.Equal(2, jobRuns.Count);
            Assert.All(jobRuns, j =>
            {
                Assert.Equal(JobRunStatus.SUCCESS, j.Status);
                Assert.Equal(0, j.Processed);
            });
        }

        [Fact]
        public async Task ThePurge_DeletesOnlyTheAuditLogsOlderThanOneYear()
        {
            var olderThanOneYear = NewAuditLog(Now.AddYears(-1).AddDays(-1));
            var exactlyOneYear = NewAuditLog(Now.AddYears(-1));
            var recent = NewAuditLog(Now.AddDays(-1));
            await SaveAsync(olderThanOneYear, exactlyOneYear, recent);

            await RunDailyJobAsync(AuditLogPurgeJob.JobName);

            var remaining = await QueryAsync(db => db.AuditLogs.AsNoTracking().Select(a => a.Id).ToListAsync());
            Assert.DoesNotContain(olderThanOneYear.Id, remaining);
            Assert.Contains(exactlyOneYear.Id, remaining);
            Assert.Contains(recent.Id, remaining);
            var jobRun = Assert.Single(await QueryAsync(db => db.JobRuns.AsNoTracking().ToListAsync()));
            Assert.Equal(AuditLogPurgeJob.JobName, jobRun.JobName);
            Assert.Equal(1, jobRun.Processed);
        }
    }
}
