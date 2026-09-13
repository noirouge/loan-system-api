namespace LoanSystemAPI.Services
{
    // THIS IS FOR TESTING UNTIL LOGIN IS AVAILABLE: THE USER COMES FROM appsettings (AdminId).
    // WITH LOGIN, ONLY THIS CLASS CHANGES TO READ THE USER FROM THE JWT
    public class CurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; }

        public CurrentUserService(IConfiguration configuration)
        {
            var configAdminId = configuration["AdminId"] ?? throw new InvalidOperationException("NOT FOUND AdminId");
            if (!Guid.TryParse(configAdminId, out Guid adminId))
                throw new InvalidOperationException("AdminId IS NOT A VALID GUID");
            UserId = adminId;
        }
    }
}
