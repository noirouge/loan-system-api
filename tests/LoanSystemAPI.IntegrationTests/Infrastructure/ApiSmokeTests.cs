using System.Net;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    public class ApiSmokeTests
    {
        [Fact]
        public async Task Api_StartsAgainstTheTestDatabase()
        {
            await TestDatabase.RecreateAsync();
            await using var factory = new LoanApiFactory();

            factory.EnsureUsesTestDatabase();
            var response = await factory.CreateClient().GetAsync("/api/cash-entries");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
