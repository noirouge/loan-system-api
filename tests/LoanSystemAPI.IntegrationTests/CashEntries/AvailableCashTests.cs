using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    [Collection(ApiCollection.Name)]
    public class AvailableCashTests : IntegrationTest
    {
        public AvailableCashTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task Withdrawal_LargerThanTheAvailableCash_IsRejected()
        {
            await Client.PostContributionAsync(100m);

            var response = await Client.PostWithdrawalAsync(150m);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(100m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task Expense_LargerThanTheAvailableCash_IsRejected()
        {
            await Client.PostContributionAsync(100m);

            var response = await Client.PostExpenseAsync(150m, counterparty: "Computadora");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(100m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task Withdrawal_OfExactlyTheAvailableCash_LeavesTheCashAtZero()
        {
            await Client.PostContributionAsync(100m);

            var response = await Client.PostWithdrawalAsync(100m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task AReversedContribution_IsNotAvailableCash()
        {
            await Client.PostContributionAsync(100m);
            await Client.PostReversalAsync((await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId());

            var response = await Client.PostWithdrawalAsync(10m);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // WITHOUT THE CASH LOCK BOTH WITHDRAWALS WOULD SEE 100 AND THE CASH WOULD END AT -20
        [Fact]
        public async Task TwoWithdrawalsAtTheSameTime_CannotLeaveTheCashNegative()
        {
            await Client.PostContributionAsync(100m);

            var responses = await Task.WhenAll(Client.PostWithdrawalAsync(60m), Client.PostWithdrawalAsync(60m));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
            Assert.Equal(40m, await Client.GetCashBalanceAsync());
        }
    }
}
