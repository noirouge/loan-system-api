using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    [Collection(ApiCollection.Name)]
    public class CashEntryTests : IntegrationTest
    {
        public CashEntryTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task Contribution_IsStoredPositive()
        {
            var response = await Client.PostContributionAsync(1000m, "2026-09-10");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var entry = Assert.Single(await Client.GetCashEntriesAsync());
            Assert.Equal(1000m, entry.Amount());
            Assert.Equal(CashEntryType.CONTRIBUTION, entry.EntryType());
            Assert.Equal(CashEntryStatus.APPLIED, entry.Status());
            Assert.Equal("2026-09-10", entry.GetProperty("valueDate").GetString());
        }

        [Fact]
        public async Task Withdrawal_IsReceivedPositive_AndStoredNegative()
        {
            await Client.PostContributionAsync(1000m);

            var response = await Client.PostWithdrawalAsync(300m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var withdrawal = await Client.SingleEntryAsync(CashEntryType.WITHDRAWAL);
            Assert.Equal(-300m, withdrawal.Amount());
        }

        [Fact]
        public async Task Expense_WithOnlyTheCounterpartyText_IsStoredNegative()
        {
            await Client.PostContributionAsync(1000m);

            var response = await Client.PostExpenseAsync(50m, counterparty: "Computadora");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var expense = await Client.SingleEntryAsync(CashEntryType.EXPENSE);
            Assert.Equal(-50m, expense.Amount());
            Assert.Equal("Computadora", expense.GetProperty("counterparty").GetString());
            Assert.Equal(JsonValueKind.Null, expense.GetProperty("counterpartyUserId").ValueKind);
        }

        [Fact]
        public async Task Expense_WithoutAnyCounterparty_IsRejected()
        {
            await Client.PostContributionAsync(1000m);

            var response = await Client.PostExpenseAsync(50m, counterparty: "   ", counterpartyUserId: Guid.Empty);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Single(await Client.GetCashEntriesAsync());
        }

        [Theory]
        [InlineData("contribution")]
        [InlineData("withdrawal")]
        [InlineData("expense")]
        public async Task AmountsNotGreaterThanZero_AreRejected(string operation)
        {
            await Client.PostContributionAsync(1000m);

            foreach (var amount in new[] { 0m, -10m })
            {
                var response = await Client.PostAsJsonAsync($"/api/cash-entries/{operation}", new
                {
                    amount,
                    valueDate = CashApi.DefaultValueDate,
                    counterpartyUserId = LoanApiFactory.AdminId,
                    counterparty = "Test",
                });

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }

            Assert.Single(await Client.GetCashEntriesAsync());
        }

        [Fact]
        public async Task List_DoesNotExposeAuditFields()
        {
            await Client.PostContributionAsync(1000m);

            var entry = Assert.Single(await Client.GetCashEntriesAsync());

            Assert.False(entry.TryGetProperty("createdBy", out _));
            Assert.False(entry.TryGetProperty("updatedBy", out _));
            Assert.False(entry.TryGetProperty("updatedDate", out _));
        }

        [Fact]
        public async Task List_OrdersByValueDate_NewestFirst()
        {
            await Client.PostContributionAsync(100m, "2026-08-01");
            await Client.PostContributionAsync(200m, "2026-09-01");

            var entries = await Client.GetCashEntriesAsync();

            Assert.Equal("2026-09-01", entries[0].GetProperty("valueDate").GetString());
            Assert.Equal("2026-08-01", entries[1].GetProperty("valueDate").GetString());
        }
    }
}
