using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class ConcurrentPaymentTests : IntegrationTest
    {
        public ConcurrentPaymentTests(ApiFixture fixture) : base(fixture)
        {
        }

        // WITHOUT THE LOCK ON THE LOAN ROW BOTH PAYMENTS WOULD READ A DEBT OF 1100 AND THE PRINCIPAL WOULD END AT -100
        [Fact]
        public async Task TwoPaymentsAtTheSameTime_CannotPayMoreThanTheDebt()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);

            var responses = await Task.WhenAll(Client.PostPaymentAsync(loanId, 600m), Client.PostPaymentAsync(loanId, 600m));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(500m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());
        }

        // WITHOUT THE LOCK BOTH PAYMENTS WOULD TAKE THE SAME 100 OF INTEREST AND THE INTEREST WOULD END AT -100
        [Fact]
        public async Task TwoPaymentsAtTheSameTime_PayThePendingInterestOnlyOnce()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);

            var responses = await Task.WhenAll(Client.PostPaymentAsync(loanId, 300m), Client.PostPaymentAsync(loanId, 300m));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
            var loan = await Client.GetLoanAsync(loanId);
            Assert.Equal(500m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());
        }
    }
}
