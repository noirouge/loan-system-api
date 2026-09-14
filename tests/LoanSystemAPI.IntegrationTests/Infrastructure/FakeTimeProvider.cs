namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    // CLOCK CONTROLLED BY THE TESTS. THE API READS IT THROUGH TimeProvider (LocalDateService)
    public class FakeTimeProvider : TimeProvider
    {
        public static readonly DateTimeOffset DefaultUtcNow = new(2026, 9, 15, 16, 0, 0, TimeSpan.Zero);

        private DateTimeOffset _utcNow = DefaultUtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void SetUtcNow(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
