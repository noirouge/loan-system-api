namespace LoanSystemAPI.Services
{
    public class LocalDateService
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _timeZone;

        public LocalDateService(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");
        }

        // TODAY IN DOMINICAN REPUBLIC (UTC-4), NOT IN UTC
        public DateOnly Today()
        {
            var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
            return DateOnly.FromDateTime(localNow.DateTime);
        }
    }
}
