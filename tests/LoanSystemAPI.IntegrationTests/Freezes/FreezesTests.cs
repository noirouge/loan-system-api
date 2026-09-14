using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Freezes
{
    [Collection(ApiCollection.Name)]
    public class FreezesTests : IntegrationTest
    {
        public FreezesTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task OpeningAFreeze_ListsItAsOpen()
        {
            var loanId = await Client.CreateLoanAsync();

            var response = await Client.PostFreezeAsync(loanId, reason: "Enfermedad");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var freeze = Assert.Single(await Client.GetFreezesAsync(loanId));
            Assert.Equal(await response.ReadIdAsync(), freeze.GetProperty("id").GetGuid());
            Assert.Equal(FreezesApi.DefaultStartDate, freeze.GetProperty("startDate").GetString());
            Assert.Null(freeze.EndDate());
            Assert.Equal("Enfermedad", freeze.GetProperty("reason").GetString());
            Assert.Equal(LoanApiFactory.AdminId, freeze.GetProperty("authorizedBy").GetGuid());
            Assert.False(freeze.TryGetProperty("createdBy", out _));
        }

        [Fact]
        public async Task ASecondOpenFreeze_IsRejectedWithConflict()
        {
            var loanId = await Client.CreateLoanAsync();
            await Client.PostFreezeAsync(loanId);

            var response = await Client.PostFreezeAsync(loanId, startDate: "2026-04-01");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Single(await Client.GetFreezesAsync(loanId));
        }

        [Fact]
        public async Task SeveralFreezesOpenedAtTheSameTime_OnlyOneWins()
        {
            var loanId = await Client.CreateLoanAsync();

            var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Client.PostFreezeAsync(loanId)));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            Assert.Single(await Client.GetFreezesAsync(loanId));
        }

        [Fact]
        public async Task ClosingAFreeze_WritesTheEndDate_AndAllowsOpeningAnotherOne()
        {
            var loanId = await Client.CreateLoanAsync();
            var freezeId = await (await Client.PostFreezeAsync(loanId)).ReadIdAsync();

            var response = await Client.CloseFreezeAsync(freezeId);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(FreezesApi.DefaultEndDate, Assert.Single(await Client.GetFreezesAsync(loanId)).EndDate());

            Assert.Equal(HttpStatusCode.Created, (await Client.PostFreezeAsync(loanId, startDate: "2026-05-01")).StatusCode);
            var freezes = await Client.GetFreezesAsync(loanId);
            Assert.Equal(2, freezes.Count);
            Assert.Null(freezes[0].EndDate());
            Assert.Equal(FreezesApi.DefaultEndDate, freezes[1].EndDate());
        }

        [Fact]
        public async Task ClosingAFreezeTwice_IsRejectedWithConflict()
        {
            var loanId = await Client.CreateLoanAsync();
            var freezeId = await (await Client.PostFreezeAsync(loanId)).ReadIdAsync();
            await Client.CloseFreezeAsync(freezeId);

            var response = await Client.CloseFreezeAsync(freezeId, endDate: "2026-05-01");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(FreezesApi.DefaultEndDate, Assert.Single(await Client.GetFreezesAsync(loanId)).EndDate());
        }

        [Theory]
        [InlineData("2026-02-28")]
        [InlineData("2026-09-16")]
        public async Task ClosingWithAnEndDateBeforeTheStartOrInTheFuture_IsRejected(string endDate)
        {
            var loanId = await Client.CreateLoanAsync();
            var freezeId = await (await Client.PostFreezeAsync(loanId)).ReadIdAsync();

            var response = await Client.CloseFreezeAsync(freezeId, endDate);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(Assert.Single(await Client.GetFreezesAsync(loanId)).EndDate());
        }

        [Theory]
        [InlineData("2026-01-14")]
        [InlineData("2026-09-16")]
        public async Task OpeningWithAStartDateBeforeTheLoanOrInTheFuture_IsRejected(string startDate)
        {
            var loanId = await Client.CreateLoanAsync();

            var response = await Client.PostFreezeAsync(loanId, startDate);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await Client.GetFreezesAsync(loanId));
        }

        [Fact]
        public async Task OpeningAFreezeOnALoanThatIsNotActive_IsRejected()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Client.PostPaymentAsync(loanId, 1000m);

            var response = await Client.PostFreezeAsync(loanId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnknownLoansAndFreezes_ReturnNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PostFreezeAsync(Guid.NewGuid())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/loans/{Guid.NewGuid()}/freezes")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.CloseFreezeAsync(Guid.NewGuid())).StatusCode);
        }
    }
}
