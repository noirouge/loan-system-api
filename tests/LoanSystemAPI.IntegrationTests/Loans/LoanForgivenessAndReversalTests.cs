using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class LoanForgivenessAndReversalTests : IntegrationTest
    {
        public LoanForgivenessAndReversalTests(ApiFixture fixture) : base(fixture)
        {
        }

        // LOAN OF 1000 WITH A 100 INTEREST CHARGE
        private async Task<Guid> CreateLoanWithInterestAsync()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);
            return loanId;
        }

        private Task<HttpResponseMessage> PostForgivenessAsync(Guid loanId, decimal amount, string valueDate = PaymentsApi.DefaultPaymentDate)
        {
            return Client.PostAsJsonAsync($"/api/loans/{loanId}/forgiveness", new { amount, valueDate, note = "Test" });
        }

        private Task<HttpResponseMessage> PostEntryReversalAsync(Guid entryId)
        {
            return Client.PostAsync($"/api/loans/entries/{entryId}/reversal", null);
        }

        [Fact]
        public async Task Forgiveness_LowersOnlyTheInterest_WithoutTouchingTheCash()
        {
            var loanId = await CreateLoanWithInterestAsync();
            var cashEntriesBefore = (await Client.GetCashEntriesAsync()).Count;

            var response = await PostForgivenessAsync(loanId, 30m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(1000m, loan.BalancePrincipal());
            Assert.Equal(70m, loan.BalanceInterest());

            var forgiveness = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.FORGIVENESS));
            Assert.Equal(0m, forgiveness.GetProperty("principal").GetDecimal());
            Assert.Equal(-30m, forgiveness.GetProperty("interest").GetDecimal());
            Assert.Equal(cashEntriesBefore, (await Client.GetCashEntriesAsync()).Count);
        }

        [Fact]
        public async Task ForgivingMoreThanThePendingInterest_IsRejected()
        {
            var loanId = await CreateLoanWithInterestAsync();

            var response = await PostForgivenessAsync(loanId, 100.01m);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(100m, (await Client.GetLoanAsync(loanId)).BalanceInterest());
        }

        [Fact]
        public async Task InvalidForgiveness_IsRejected()
        {
            var loanId = await CreateLoanWithInterestAsync();

            Assert.Equal(HttpStatusCode.BadRequest, (await PostForgivenessAsync(loanId, 0m)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostForgivenessAsync(loanId, 10m, "2026-09-16")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PostForgivenessAsync(Guid.NewGuid(), 10m)).StatusCode);
        }

        [Fact]
        public async Task ReversingAPayment_RestoresTheDebt_AndReversesItsCashEntry()
        {
            var loanId = await CreateLoanWithInterestAsync();
            var paymentId = await (await Client.PostPaymentAsync(loanId, 300m, valueDate: "2026-02-20")).ReadIdAsync();

            var response = await PostEntryReversalAsync(paymentId);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var reversalId = await response.ReadIdAsync();
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(1000m, loan.BalancePrincipal());
            Assert.Equal(100m, loan.BalanceInterest());

            var payment = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
            Assert.Equal((int)LoanEntryStatus.REVERSED, payment.GetProperty("status").GetInt32());
            var reversal = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.REVERSAL));
            Assert.Equal(200m, reversal.GetProperty("principal").GetDecimal());
            Assert.Equal(100m, reversal.GetProperty("interest").GetDecimal());
            Assert.Equal("2026-02-20", reversal.GetProperty("valueDate").GetString());
            Assert.Equal(paymentId, reversal.GetProperty("reversesEntryId").GetGuid());

            var cashPayment = await Client.SingleEntryAsync(CashEntryType.PAYMENT);
            var cashReversal = await Client.SingleEntryAsync(CashEntryType.REVERSAL);
            Assert.Equal(CashEntryStatus.REVERSED, cashPayment.Status());
            Assert.Equal(-300m, cashReversal.Amount());
            Assert.Equal(reversalId, cashReversal.GetProperty("loanEntryId").GetGuid());
            Assert.Equal(cashPayment.EntryId(), cashReversal.GetProperty("reversesEntryId").GetGuid());
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task ReversingAForgiveness_RestoresTheInterest_WithoutCash()
        {
            var loanId = await CreateLoanWithInterestAsync();
            var forgivenessId = await (await PostForgivenessAsync(loanId, 30m)).ReadIdAsync();

            var response = await PostEntryReversalAsync(forgivenessId);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(100m, (await Client.GetLoanAsync(loanId)).BalanceInterest());
            Assert.DoesNotContain(await Client.GetCashEntriesAsync(), e => e.EntryType() == CashEntryType.REVERSAL);
        }

        [Fact]
        public async Task ReversingTheSameEntryTwice_ReturnsConflict()
        {
            var loanId = await CreateLoanWithInterestAsync();
            var paymentId = await (await Client.PostPaymentAsync(loanId, 300m)).ReadIdAsync();
            await PostEntryReversalAsync(paymentId);

            var second = await PostEntryReversalAsync(paymentId);

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.REVERSAL));
        }

        [Fact]
        public async Task APendingInterestCharge_CanBeReversed_ButADisbursementCannot()
        {
            var loanId = await CreateLoanWithInterestAsync();
            var charge = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.INTERESTCHARGE));
            var disbursement = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.DISBURSEMENT));

            Assert.Equal(HttpStatusCode.Created, (await PostEntryReversalAsync(charge.GetProperty("id").GetGuid())).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PostEntryReversalAsync(disbursement.GetProperty("id").GetGuid())).StatusCode);
        }

        [Fact]
        public async Task ReversingAnUnknownEntry_ReturnsNotFound()
        {
            var response = await PostEntryReversalAsync(Guid.NewGuid());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
