using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace LoanSystemAPI.IntegrationTests.Dates
{
    [Collection(ApiCollection.Name)]
    public class DatesTests : IntegrationTest
    {
        public DatesTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task Reversal_KeepsTheValueDateOfTheOriginal()
        {
            await Client.PostContributionAsync(1000m, "2026-08-01");
            var contributionId = (await Client.SingleEntryAsync(CashEntryType.CONTRIBUTION)).EntryId();

            await Client.PostReversalAsync(contributionId);

            var reversal = await Client.SingleEntryAsync(CashEntryType.REVERSAL);
            Assert.Equal("2026-08-01", reversal.GetProperty("valueDate").GetString());
        }

        // 02:30 UTC ON THE 14TH IS STILL 22:30 ON THE 13TH IN DOMINICAN REPUBLIC (UTC-4)
        [Theory]
        [InlineData("2026-09-14T02:30:00Z", "2026-09-13")]
        [InlineData("2026-09-14T03:59:59Z", "2026-09-13")]
        [InlineData("2026-09-14T04:00:00Z", "2026-09-14")]
        public void Today_IsTheDominicanDate_NotTheUtcDate(string utcNow, string expectedToday)
        {
            Factory.Clock.SetUtcNow(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));

            var today = Factory.Services.GetRequiredService<LocalDateService>().Today();

            Assert.Equal(DateOnly.Parse(expectedToday, CultureInfo.InvariantCulture), today);
        }

        [Fact]
        public async Task ValueDate_WithTime_IsRejected()
        {
            var response = await Client.PostAsJsonAsync("/api/cash-entries/contribution", new
            {
                amount = 100m,
                valueDate = "2026-09-13T00:00:00",
                counterpartyUserId = LoanApiFactory.AdminId,
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
