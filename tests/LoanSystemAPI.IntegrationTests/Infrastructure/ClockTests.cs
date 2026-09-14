using LoanSystemAPI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    [Collection(ApiCollection.Name)]
    public class ClockTests : IntegrationTest
    {
        public ClockTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void Api_UsesTheClockControlledByTheTests()
        {
            Factory.Clock.SetUtcNow(new DateTimeOffset(2030, 1, 10, 15, 0, 0, TimeSpan.Zero));

            var today = Factory.Services.GetRequiredService<LocalDateService>().Today();

            Assert.Equal(new DateOnly(2030, 1, 10), today);
        }
    }
}
