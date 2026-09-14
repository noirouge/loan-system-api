using System.Security.Claims;

namespace LoanSystemAPI.Services
{
    // THE CURRENT USER IS THE ONE OF THE JWT OF THE REQUEST (THE sub CLAIM). WITHOUT A VALID TOKEN THERE IS NO USER,
    // AND ASKING FOR IT IS A PROGRAMMING ERROR: THAT ENDPOINT SHOULD NOT ALLOW ANONYMOUS CALLS
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid UserId
        {
            get
            {
                ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
                var userIdClaim = user?.FindFirst(AuthTokenService.UserIdClaim)?.Value;

                if (!Guid.TryParse(userIdClaim, out Guid userId))
                    throw new InvalidOperationException("THERE IS NO AUTHENTICATED USER IN THIS REQUEST");

                return userId;
            }
        }
    }
}
