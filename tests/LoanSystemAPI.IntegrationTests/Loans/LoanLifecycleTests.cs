using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class LoanLifecycleTests : IntegrationTest
    {
        public LoanLifecycleTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<LoanStatus> GetStatusAsync(Guid loanId)
        {
            var loan = await Client.GetLoanAsync(loanId);
            return (LoanStatus)loan.GetProperty("status").GetInt16();
        }

        [Fact]
        public async Task PayingTheWholeDebt_ClosesTheLoan()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            await Client.PostPaymentAsync(loanId, 1000m);

            Assert.Equal(LoanStatus.CLOSED, await GetStatusAsync(loanId));
        }

        [Fact]
        public async Task APaymentThatDoesNotSettleTheDebt_KeepsTheLoanActive()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            await Client.PostPaymentAsync(loanId, 999.99m);

            Assert.Equal(LoanStatus.ACTIVE, await GetStatusAsync(loanId));
        }

        [Fact]
        public async Task ReversingThePaymentThatClosedTheLoan_OpensItAgain()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var paymentId = await (await Client.PostPaymentAsync(loanId, 1000m)).ReadIdAsync();

            await Client.PostAsync($"/api/loans/entries/{paymentId}/reversal", null);

            Assert.Equal(LoanStatus.ACTIVE, await GetStatusAsync(loanId));
        }

        [Fact]
        public async Task WriteOff_MarksTheLoan_ThatCanStillReceiveAPayment()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            var response = await Client.PostAsync($"/api/loans/{loanId}/write-off", null);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(LoanStatus.WRITTENOFF, await GetStatusAsync(loanId));
            Assert.Equal(HttpStatusCode.Created, (await Client.PostPaymentAsync(loanId, 100m)).StatusCode);
            Assert.Equal(LoanStatus.WRITTENOFF, await GetStatusAsync(loanId));
        }

        [Fact]
        public async Task PayingAWrittenOffLoanInFull_ClosesIt()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Client.PostAsync($"/api/loans/{loanId}/write-off", null);

            await Client.PostPaymentAsync(loanId, 1000m);

            Assert.Equal(LoanStatus.CLOSED, await GetStatusAsync(loanId));
        }

        [Fact]
        public async Task WriteOff_OfALoanThatIsNotActive_IsRejected()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Client.PostPaymentAsync(loanId, 1000m);

            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsync($"/api/loans/{loanId}/write-off", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsync($"/api/loans/{Guid.NewGuid()}/write-off", null)).StatusCode);
        }

        [Fact]
        public async Task DeletingALoanWithOnlyTheDisbursement_ReversesItAndGivesTheCashBack()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            Assert.Equal(0m, await Client.GetCashBalanceAsync());

            var response = await Client.DeleteAsync($"/api/loans/{loanId}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/loans/{loanId}")).StatusCode);
            Assert.Equal(0, (await Client.GetFromJsonAsync<JsonElement>("/api/loans")).GetArrayLength());
            Assert.Equal(1000m, await Client.GetCashBalanceAsync());
            Assert.Equal(CashEntryStatus.REVERSED, (await Client.SingleEntryAsync(CashEntryType.DISBURSEMENT)).Status());
            Assert.Equal(1000m, (await Client.SingleEntryAsync(CashEntryType.REVERSAL)).Amount());
        }

        [Fact]
        public async Task DeletingALoanWithAnInterestCharge_IsRejected()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);

            var response = await Client.DeleteAsync($"/api/loans/{loanId}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task DeletingALoanWithAPayment_IsRejected()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Client.PostPaymentAsync(loanId, 100m);

            var response = await Client.DeleteAsync($"/api/loans/{loanId}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task DeletingAnUnknownLoan_ReturnsNotFound()
        {
            var response = await Client.DeleteAsync($"/api/loans/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
