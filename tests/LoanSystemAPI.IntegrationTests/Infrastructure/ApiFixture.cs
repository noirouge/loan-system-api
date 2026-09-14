namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    // ONE API AND ONE FRESH SCHEMA FOR THE WHOLE TEST RUN
    public class ApiFixture : IAsyncLifetime
    {
        public LoanApiFactory Factory { get; } = new();

        public async Task InitializeAsync()
        {
            await TestDatabase.RecreateAsync();
            Factory.EnsureUsesTestDatabase();
        }

        public async Task DisposeAsync()
        {
            await Factory.DisposeAsync();
        }
    }
}
