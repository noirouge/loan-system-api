using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    [Collection(ApiCollection.Name)]
    public class IsolationTests : IntegrationTest
    {
        public IsolationTests(ApiFixture fixture) : base(fixture)
        {
        }

        // BOTH RUNS INSERT ONE CONTRIBUTION: IF THE RESET FAILED, THE SECOND RUN WOULD SEE TWO
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task EveryTest_StartsWithEmptyTables(int run)
        {
            var response = await Client.PostAsJsonAsync("/api/cash-entries/contribution", new
            {
                amount = 100m * run,
                valueDate = "2026-09-01",
                counterpartyUserId = LoanApiFactory.AdminId,
            });
            response.EnsureSuccessStatusCode();

            var entries = await Client.GetFromJsonAsync<JsonElement>("/api/cash-entries");

            Assert.Equal(1, entries.GetArrayLength());
        }
    }
}
