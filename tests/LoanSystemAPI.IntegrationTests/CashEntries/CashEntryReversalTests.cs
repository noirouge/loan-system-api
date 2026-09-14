using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    [Collection(ApiCollection.Name)]
    public class CashEntryReversalTests : IntegrationTest
    {
        public CashEntryReversalTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task ReversingAContribution_CreatesTheOppositeEntry_AndMarksTheOriginalReversed()
        {
            await Client.PostContributionAsync(1000m);
            var contributionId = (await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId();

            var response = await Client.PostReversalAsync(contributionId);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var original = await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION);
            var reversal = await Client.SingleEntryAsync(CashEntryType.REVERSAL);
            Assert.Equal(CashEntryStatus.REVERSED, original.Status());
            Assert.Equal(CashEntryStatus.APPLIED, reversal.Status());
            Assert.Equal(-1000m, reversal.Amount());
            Assert.Equal(contributionId, reversal.GetProperty("reversesEntryId").GetGuid());
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task ReversingAWithdrawal_GivesTheMoneyBack()
        {
            await Client.PostContributionAsync(1000m);
            await Client.PostWithdrawalAsync(300m);
            var withdrawalId = (await Client.SingleEntryAsync(CashEntryType.WITHDRAWAL)).EntryId();

            var response = await Client.PostReversalAsync(withdrawalId);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(1000m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task ReversingAnExpense_GivesTheMoneyBack()
        {
            await Client.PostContributionAsync(1000m);
            await Client.PostExpenseAsync(200m, counterparty: "Computadora");
            var expenseId = (await Client.SingleEntryAsync(CashEntryType.EXPENSE)).EntryId();

            var response = await Client.PostReversalAsync(expenseId);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(1000m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task ReversingTheSameEntryTwice_ReturnsConflict()
        {
            await Client.PostContributionAsync(1000m);
            var contributionId = (await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId();
            await Client.PostReversalAsync(contributionId);

            var second = await Client.PostReversalAsync(contributionId);

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            await Client.SingleEntryAsync(CashEntryType.REVERSAL);
        }

        [Fact]
        public async Task AReversal_CannotBeReversed()
        {
            await Client.PostContributionAsync(1000m);
            await Client.PostReversalAsync((await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId());
            var reversalId = (await Client.SingleEntryAsync(CashEntryType.REVERSAL)).EntryId();

            var response = await Client.PostReversalAsync(reversalId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ReversingAnUnknownEntry_ReturnsNotFound()
        {
            var response = await Client.PostReversalAsync(Guid.NewGuid());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
