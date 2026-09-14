using LoanSystemAPI.DTOs;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    // THE EXAMPLES OF THE BUSINESS MODEL WITH EXACTLY THEIR NUMBERS. THE KEY ONE IS THE 84 OF MONTH 3:
    // THE INTEREST IS CHARGED OVER 840 BECAUSE 40 OF INTEREST WERE LEFT UNPAID. AN IMPLEMENTATION THAT GIVES 80 IS WRONG
    [Collection(ApiCollection.Name)]
    public class CanonicalExampleTests : IntegrationTest
    {
        public CanonicalExampleTests(ApiFixture fixture) : base(fixture)
        {
        }

        // WHAT THE MONTHLY JOB WILL DO: CALCULATE THE CHARGE OVER THE CURRENT BALANCE AND SAVE IT
        private async Task<decimal> ChargeInterestAsync(Guid loanId, DateOnly period)
        {
            var loan = await Client.GetLoanAsync(loanId);
            var balance = new LoanBalanceDTO { Principal = loan.BalancePrincipal(), Interest = loan.BalanceInterest() };
            var interest = InterestCharge.Calculate(loan.GetProperty("interestRate").GetDecimal(), balance);

            await Factory.AddInterestChargeAsync(loanId, interest, period);
            return interest;
        }

        private async Task PayAsync(Guid loanId, decimal amount, string valueDate)
        {
            var response = await Client.PostPaymentAsync(loanId, amount, valueDate: valueDate);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        private async Task AssertBalanceAsync(Guid loanId, decimal principal, decimal interest)
        {
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(principal, loan.BalancePrincipal());
            Assert.Equal(interest, loan.BalanceInterest());
        }

        [Fact]
        public async Task ALoanOf1000At10Percent_FollowsTheCanonicalExample()
        {
            var loanId = await Client.CreateLoanAsync(1000m, 0.10m);
            await AssertBalanceAsync(loanId, 1000m, 0m);

            Assert.Equal(100m, await ChargeInterestAsync(loanId, new DateOnly(2026, 2, 1)));
            await AssertBalanceAsync(loanId, 1000m, 100m);

            await PayAsync(loanId, 300m, "2026-02-10");
            await AssertBalanceAsync(loanId, 800m, 0m);

            Assert.Equal(80m, await ChargeInterestAsync(loanId, new DateOnly(2026, 3, 1)));
            await AssertBalanceAsync(loanId, 800m, 80m);

            await PayAsync(loanId, 40m, "2026-03-10");
            await AssertBalanceAsync(loanId, 800m, 40m);

            Assert.Equal(84m, await ChargeInterestAsync(loanId, new DateOnly(2026, 4, 1)));
            await AssertBalanceAsync(loanId, 800m, 124m);

            await PayAsync(loanId, 324m, "2026-04-10");
            await AssertBalanceAsync(loanId, 600m, 0m);

            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(LoanStatus.ACTIVE, (LoanStatus)loan.GetProperty("status").GetInt16());
            Assert.Equal(664m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task Fulanito_WhoDoesNotPayMonth1_PaysInterestOverTheUnpaidInterest()
        {
            var loanId = await Client.CreateLoanAsync(100m, 0.10m);

            Assert.Equal(10m, await ChargeInterestAsync(loanId, new DateOnly(2026, 2, 1)));
            Assert.Equal(11m, await ChargeInterestAsync(loanId, new DateOnly(2026, 3, 1)));
            await AssertBalanceAsync(loanId, 100m, 21m);

            await PayAsync(loanId, 31m, "2026-03-10");
            await AssertBalanceAsync(loanId, 90m, 0m);
        }
    }
}
