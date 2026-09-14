using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class LoanPaymentCascadeTests : IntegrationTest
    {
        public LoanPaymentCascadeTests(ApiFixture fixture) : base(fixture)
        {
        }

        // LOAN OF 1000 WITH A 100 INTEREST CHARGE: THE DEBT IS 1100
        private async Task<Guid> CreateLoanWithInterestAsync()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);
            return loanId;
        }

        [Fact]
        public async Task APaymentLargerThanTheInterest_PaysTheInterestFirst_AndThenPrincipal()
        {
            var loanId = await CreateLoanWithInterestAsync();

            var response = await Client.PostPaymentAsync(loanId, 300m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(800m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());

            var payment = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
            Assert.Equal(-200m, payment.GetProperty("principal").GetDecimal());
            Assert.Equal(-100m, payment.GetProperty("interest").GetDecimal());

            var cash = await Client.SingleEntryAsync(CashEntryType.PAYMENT);
            Assert.Equal(300m, cash.Amount());
            Assert.Equal(await response.ReadIdAsync(), cash.GetProperty("loanEntryId").GetGuid());
        }

        [Fact]
        public async Task APaymentEqualToTheInterest_LeavesThePrincipalIntact()
        {
            var loanId = await CreateLoanWithInterestAsync();

            await Client.PostPaymentAsync(loanId, 100m);

            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(1000m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());
        }

        [Fact]
        public async Task APaymentSmallerThanTheInterest_LeavesTheRestOfTheInterestPending()
        {
            var loanId = await CreateLoanWithInterestAsync();

            await Client.PostPaymentAsync(loanId, 40m);

            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(1000m, loan.BalancePrincipal());
            Assert.Equal(60m, loan.BalanceInterest());

            var payment = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
            Assert.Equal(0m, payment.GetProperty("principal").GetDecimal());
            Assert.Equal(-40m, payment.GetProperty("interest").GetDecimal());
        }

        [Fact]
        public async Task WithoutPendingInterest_TheWholePaymentGoesToPrincipal()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            await Client.PostPaymentAsync(loanId, 250m);

            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(750m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());
        }

        [Fact]
        public async Task PayingTheWholeDebt_LeavesPrincipalAndInterestAtZero()
        {
            var loanId = await CreateLoanWithInterestAsync();

            var response = await Client.PostPaymentAsync(loanId, 1100m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(0m, (await Client.GetLoanAsync(loanId)).BalanceTotal());
        }

        [Theory]
        [InlineData(1100.01)]
        [InlineData(5000)]
        public async Task APaymentLargerThanTheTotalDebt_IsRejected(double amount)
        {
            var loanId = await CreateLoanWithInterestAsync();

            var response = await Client.PostPaymentAsync(loanId, (decimal)amount);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
        }

        // THE TEST CLOCK SAYS TODAY IS 2026-09-15, AND THE LOAN IS FROM 2026-01-15
        [Theory]
        [InlineData(0, "2026-02-10")]
        [InlineData(10.005, "2026-02-10")]
        [InlineData(100, "2026-09-16")]
        [InlineData(100, "2026-01-14")]
        public async Task InvalidPayments_AreRejected(double amount, string valueDate)
        {
            var loanId = await CreateLoanWithInterestAsync();

            var response = await Client.PostPaymentAsync(loanId, (decimal)amount, valueDate: valueDate);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task APaymentToAnUnknownLoan_ReturnsNotFound()
        {
            var response = await Client.PostPaymentAsync(Guid.NewGuid(), 100m);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
