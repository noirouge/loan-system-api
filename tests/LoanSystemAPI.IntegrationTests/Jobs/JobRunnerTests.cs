using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoanSystemAPI.IntegrationTests.Jobs
{
    [Collection(ApiCollection.Name)]
    public class JobRunnerTests : IntegrationTest
    {
        private const string JobName = "test-job";
        private static readonly DateOnly February = new(2026, 2, 1);

        public JobRunnerTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<JobRun?> RunAsync(Func<CancellationToken, Task<JobRunCounters>> work, DateOnly? period = null, string jobName = JobName)
        {
            using var scope = Factory.Services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<JobRunner>();
            return await runner.RunAsync(jobName, period, work);
        }

        private static Func<CancellationToken, Task<JobRunCounters>> Returns(int processed, int skipped, int failed)
        {
            return _ => Task.FromResult(new JobRunCounters { Processed = processed, Skipped = skipped, Failed = failed });
        }

        private async Task<List<JobRun>> GetJobRunsAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.JobRuns.AsNoTracking().ToListAsync();
        }

        private async Task SaveAsync(JobRun jobRun)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.JobRuns.Add(jobRun);
            await dbContext.SaveChangesAsync();
        }

        private static JobRun NewJobRun(string jobName, DateOnly? period, JobRunStatus status)
        {
            return new JobRun
            {
                Id = Guid.NewGuid(),
                JobName = jobName,
                Period = period,
                Status = status,
                StartedAt = DateTime.UtcNow,
            };
        }

        [Fact]
        public async Task AJobWithoutFailures_EndsInSuccess_WithItsCounters()
        {
            var result = await RunAsync(Returns(3, 1, 0), February);

            Assert.NotNull(result);
            var jobRun = Assert.Single(await GetJobRunsAsync());
            Assert.Equal(JobName, jobRun.JobName);
            Assert.Equal(February, jobRun.Period);
            Assert.Equal(JobRunStatus.SUCCESS, jobRun.Status);
            Assert.Equal(3, jobRun.Processed);
            Assert.Equal(1, jobRun.Skipped);
            Assert.Equal(0, jobRun.Failed);
            Assert.NotNull(jobRun.FinishedAt);
            Assert.Null(jobRun.ErrorMessage);
        }

        [Theory]
        [InlineData(0, 0, 0, JobRunStatus.SUCCESS)]
        [InlineData(0, 5, 0, JobRunStatus.SUCCESS)]
        [InlineData(2, 0, 1, JobRunStatus.PARTIAL)]
        [InlineData(0, 1, 1, JobRunStatus.PARTIAL)]
        [InlineData(0, 0, 2, JobRunStatus.FAILED)]
        public async Task TheStatus_DependsOnTheCounters(int processed, int skipped, int failed, JobRunStatus expected)
        {
            await RunAsync(Returns(processed, skipped, failed));

            Assert.Equal(expected, Assert.Single(await GetJobRunsAsync()).Status);
        }

        [Fact]
        public async Task AJobThatThrows_EndsInFailed_WithTheErrorMessage()
        {
            await RunAsync(_ => throw new InvalidOperationException("SOMETHING BROKE"));

            var jobRun = Assert.Single(await GetJobRunsAsync());
            Assert.Equal(JobRunStatus.FAILED, jobRun.Status);
            Assert.Equal("SOMETHING BROKE", jobRun.ErrorMessage);
            Assert.NotNull(jobRun.FinishedAt);
        }

        [Fact]
        public async Task TheRunningRow_IsSavedBeforeTheWorkStarts()
        {
            JobRun? seenDuringTheWork = null;

            await RunAsync(async _ =>
            {
                seenDuringTheWork = Assert.Single(await GetJobRunsAsync());
                return new JobRunCounters();
            });

            Assert.NotNull(seenDuringTheWork);
            Assert.Equal(JobRunStatus.RUNNING, seenDuringTheWork!.Status);
            Assert.Null(seenDuringTheWork.FinishedAt);
        }

        [Fact]
        public async Task APeriodThatAlreadySucceeded_IsNotRunAgain()
        {
            await RunAsync(Returns(1, 0, 0), February);
            var workRan = false;

            var result = await RunAsync(_ =>
            {
                workRan = true;
                return Task.FromResult(new JobRunCounters());
            }, February);

            Assert.Null(result);
            Assert.False(workRan);
            Assert.Single(await GetJobRunsAsync());
        }

        [Fact]
        public async Task APeriodThatFailed_CanBeRunAgainUntilItSucceeds()
        {
            await RunAsync(Returns(0, 0, 1), February);

            var result = await RunAsync(Returns(1, 0, 0), February);

            Assert.NotNull(result);
            var jobRuns = await GetJobRunsAsync();
            Assert.Equal(2, jobRuns.Count);
            Assert.Single(jobRuns, j => j.Status == JobRunStatus.FAILED);
            Assert.Single(jobRuns, j => j.Status == JobRunStatus.SUCCESS);
        }

        [Fact]
        public async Task AJobWithoutPeriod_CanSucceedManyTimes()
        {
            await RunAsync(Returns(1, 0, 0));
            await RunAsync(Returns(1, 0, 0));

            Assert.Equal(2, (await GetJobRunsAsync()).Count(j => j.Status == JobRunStatus.SUCCESS));
        }

        [Fact]
        public async Task TheDatabase_AllowsOnlyOneSuccessPerPeriod()
        {
            await SaveAsync(NewJobRun(JobName, February, JobRunStatus.SUCCESS));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(NewJobRun(JobName, February, JobRunStatus.SUCCESS)));

            var postgresError = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
            Assert.Equal("ux_job_runs_success", postgresError.ConstraintName);
        }

        [Fact]
        public async Task RunningRowsLeftByADeadProcess_AreMarkedFailed_OnTheNextRunOfTheSameJob()
        {
            var orphan = NewJobRun(JobName, February, JobRunStatus.RUNNING);
            var otherJobRun = NewJobRun("other-job", February, JobRunStatus.RUNNING);
            await SaveAsync(orphan);
            await SaveAsync(otherJobRun);

            await RunAsync(Returns(1, 0, 0), new DateOnly(2026, 3, 1));

            var jobRuns = await GetJobRunsAsync();
            var orphanAfter = jobRuns.Single(j => j.Id == orphan.Id);
            Assert.Equal(JobRunStatus.FAILED, orphanAfter.Status);
            Assert.NotNull(orphanAfter.FinishedAt);
            Assert.NotNull(orphanAfter.ErrorMessage);
            Assert.Equal(JobRunStatus.RUNNING, jobRuns.Single(j => j.Id == otherJobRun.Id).Status);
        }

        [Fact]
        public async Task WhileAJobHoldsItsLock_ASecondRunOfTheSameJobDoesNothing()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var firstRun = RunAsync(async _ =>
            {
                started.SetResult();
                await release.Task;
                return new JobRunCounters { Processed = 1 };
            });
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            var secondWorkRan = false;
            var secondRun = await RunAsync(_ =>
            {
                secondWorkRan = true;
                return Task.FromResult(new JobRunCounters());
            });
            var otherJobRun = await RunAsync(Returns(1, 0, 0), jobName: "other-job");

            release.SetResult();
            var first = await firstRun;

            Assert.Null(secondRun);
            Assert.False(secondWorkRan);
            Assert.NotNull(otherJobRun);
            Assert.Equal(JobRunStatus.SUCCESS, first!.Status);
            Assert.Equal(2, (await GetJobRunsAsync()).Count);
        }
    }
}
