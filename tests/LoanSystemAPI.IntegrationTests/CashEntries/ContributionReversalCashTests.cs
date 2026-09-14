using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    // REVERSING A CONTRIBUTION TAKES MONEY OUT OF THE CASH (D-070)
    [Collection(ApiCollection.Name)]
    public class ContributionReversalCashTests : IntegrationTest
    {
        public ContributionReversalCashTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task ReversingAContributionWhoseMoneyWasAlreadySpent_IsRejected()
        {
            await Client.PostContributionAsync(1000m);
            await Client.PostWithdrawalAsync(600m);
            var contribution = await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION);

            var response = await Client.PostReversalAsync(contribution.EntryId());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(400m, await Client.GetCashBalanceAsync());
            Assert.Equal(CashEntryStatus.APPLIED, (await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).Status());
        }

        [Fact]
        public async Task ReversingAWithdrawal_DoesNotNeedCash()
        {
            await Client.PostContributionAsync(1000m);
            await Client.PostWithdrawalAsync(1000m);
            var withdrawal = await Client.SingleEntryAsync(CashEntryType.WITHDRAWAL);

            var response = await Client.PostReversalAsync(withdrawal.EntryId());

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(1000m, await Client.GetCashBalanceAsync());
        }
    }
}
