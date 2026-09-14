namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    // BASE FOR THE TESTS THAT CALL THE API: EVERY TEST STARTS WITH EMPTY TABLES AND ONLY THE ADMIN USER.
    // EACH TEST CLASS STILL NEEDS [Collection(ApiCollection.Name)] TO RECEIVE THE SHARED ApiFixture
    public abstract class IntegrationTest : IAsyncLifetime
    {
        protected LoanApiFactory Factory { get; }
        protected HttpClient Client { get; }

        protected IntegrationTest(ApiFixture fixture)
        {
            Factory = fixture.Factory;
            Client = fixture.Factory.CreateClient();
        }

        public virtual Task InitializeAsync()
        {
            Factory.Clock.SetUtcNow(FakeTimeProvider.DefaultUtcNow);
            return TestDatabase.ResetAsync(LoanApiFactory.AdminId);
        }

        public Task DisposeAsync()
        {
            Client.Dispose();
            return Task.CompletedTask;
        }
    }
}
