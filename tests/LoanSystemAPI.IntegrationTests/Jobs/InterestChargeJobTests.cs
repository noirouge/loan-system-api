using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Jobs
{
    [Collection(ApiCollection.Name)]
    public class InterestChargeJobTests : IntegrationTest
    {
        public InterestChargeJobTests(ApiFixture fixture) : base(fixture)
        {
        }

        private static DateOnly Month(int month)
        {
            return new DateOnly(2026, month, 1);
        }

        private async Task<JobRun?> RunPeriodAsync(DateOnly period)
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<InterestChargeJob>().RunPeriodAsync(period);
        }

        private async Task RunDailyCheckAsync()
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<InterestChargeJob>().RunAsync(CancellationToken.None);
        }

        private async Task<List<decimal>> ChargesAsync(Guid loanId)
        {
            var charges = await Client.GetEntriesAsync(loanId, LoanEntryType.INTERESTCHARGE);
            return charges.Select(c => c.GetProperty("interest").GetDecimal()).ToList();
        }

        private async Task<List<JobRun>> JobRunsAsync()
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().JobRuns.AsNoTracking().ToListAsync();
        }

        [Fact]
        public async Task TheJob_ChargesTheCanonicalExample()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            await RunPeriodAsync(Month(2));
            await Client.PostPaymentAsync(loanId, 300m, valueDate: "2026-02-10");
            await RunPeriodAsync(Month(3));
            await Client.PostPaymentAsync(loanId, 40m, valueDate: "2026-03-10");
            await RunPeriodAsync(Month(4));

            Assert.Equal(new[] { 100m, 80m, 84m }, await ChargesAsync(loanId));
            Assert.Equal(124m, (await Client.GetLoanAsync(loanId)).BalanceInterest());
        }

        [Fact]
        public async Task RunningAPeriodAgain_NeverDuplicatesTheCharge()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            var first = await RunPeriodAsync(Month(2));
            var second = await RunPeriodAsync(Month(2));

            Assert.Equal(new[] { 100m }, await ChargesAsync(loanId));
            Assert.Equal(1, first!.Processed);
            Assert.Equal("interest-charges:2026-02", second!.JobName);
            Assert.Null(second.Period);
            Assert.Equal(JobRunStatus.SUCCESS, second.Status);
            Assert.Equal(1, second.Skipped);
        }

        [Fact]
        public async Task TheFirstCharge_IsOnDay1OfTheNextMonth()
        {
            var endOfJanuary = await Client.CreateLoanAsync(1000m, loanDate: "2026-01-31");
            var firstOfFebruary = await Client.CreateLoanAsync(1000m, loanDate: "2026-02-01");

            await RunPeriodAsync(Month(2));
            Assert.Single(await ChargesAsync(endOfJanuary));
            Assert.Empty(await ChargesAsync(firstOfFebruary));

            await RunPeriodAsync(Month(3));
            Assert.Single(await ChargesAsync(firstOfFebruary));
        }

        [Fact]
        public async Task FrozenWrittenOffAndClosedLoans_AreNotCharged()
        {
            var frozen = await Client.CreateLoanAsync(1000m);
            await Client.PostAsJsonAsync($"/api/loans/{frozen}/freezes", new { startDate = "2026-01-20" });
            var freezeEnded = await Client.CreateLoanAsync(1000m);
            var freezeId = await (await Client.PostAsJsonAsync($"/api/loans/{freezeEnded}/freezes", new { startDate = "2026-01-20" })).ReadIdAsync();
            await Client.PostAsJsonAsync($"/api/freezes/{freezeId}/close", new { endDate = "2026-01-25" });
            var writtenOff = await Client.CreateLoanAsync(1000m);
            await Client.PostAsync($"/api/loans/{writtenOff}/write-off", null);
            var closed = await Client.CreateLoanAsync(1000m);
            await Client.PostPaymentAsync(closed, 1000m);

            await RunPeriodAsync(Month(2));

            Assert.Empty(await ChargesAsync(frozen));
            Assert.Single(await ChargesAsync(freezeEnded));
            Assert.Empty(await ChargesAsync(writtenOff));
            Assert.Empty(await ChargesAsync(closed));
        }

        [Fact]
        public async Task TheDailyCheck_RecoversEveryMissedPeriod_OnlyOnce()
        {
            var loanId = await Client.CreateLoanAsync(1000m, loanDate: "2026-06-10");

            await RunDailyCheckAsync();
            await RunDailyCheckAsync();

            Assert.Equal(new[] { 100m, 110m, 121m }, await ChargesAsync(loanId));
            var jobRuns = await JobRunsAsync();
            Assert.Equal(new[] { Month(7), Month(8), Month(9) }, jobRuns.Select(j => j.Period!.Value).OrderBy(p => p));
            Assert.All(jobRuns, j => Assert.Equal(JobRunStatus.SUCCESS, j.Status));
        }

        [Fact]
        public async Task ALoanRegisteredWithAPastDate_GetsItsMissingCharges()
        {
            var recent = await Client.CreateLoanAsync(1000m, loanDate: "2026-08-10");
            await RunDailyCheckAsync();
            var backdated = await Client.CreateLoanAsync(1000m, loanDate: "2026-07-05");

            await RunDailyCheckAsync();

            Assert.Equal(new[] { 100m }, await ChargesAsync(recent));
            Assert.Equal(new[] { 100m, 110m }, await ChargesAsync(backdated));
        }

        [Fact]
        public async Task TheEndpoint_RunsAPeriodThatAlreadyStarted()
        {
            await Client.CreateLoanAsync(1000m);

            var response = await Client.PostAsync("/api/jobs/interest-charges/2026-09-01", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jobRun = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal((int)JobRunStatus.SUCCESS, jobRun.GetProperty("status").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsync("/api/jobs/interest-charges/2026-09-15", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsync("/api/jobs/interest-charges/2026-10-01", null)).StatusCode);
        }
    }
}
