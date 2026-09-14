using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    [Collection(ApiCollection.Name)]
    public class ConcurrentReversalTests : IntegrationTest
    {
        public ConcurrentReversalTests(ApiFixture fixture) : base(fixture)
        {
        }

        // A "WAS IT ALREADY REVERSED?" QUERY WOULD LET SEVERAL OF THESE THROUGH AT ONCE; THE UNIQUE INDEX DOES NOT
        [Fact]
        public async Task SeveralReversalsOfTheSameEntryAtTheSameTime_OnlyOneWins()
        {
            await Client.PostContributionAsync(1000m);
            var contributionId = (await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId();

            var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Client.PostReversalAsync(contributionId)));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            await Client.SingleEntryAsync(CashEntryType.REVERSAL);
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }
    }
}
