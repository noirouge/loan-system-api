using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        // HASH OF A RANDOM PASSWORD: AN UNKNOWN USERNAME ALSO PAYS ONE VERIFICATION, SO THE RESPONSE TIME DOES NOT REVEAL WHICH USERNAMES EXIST
        private static readonly string UnknownUserPasswordHash = new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString());

        private readonly ILogger<AuthController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly AuthTokenService _authTokenService;
        private readonly TimeProvider _timeProvider;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AuthController(ILogger<AuthController> logger, AppDbContext dbContext, AuthTokenService authTokenService, TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _authTokenService = authTokenService;
            _timeProvider = timeProvider;
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthTokensDTO>> Login([FromBody] AuthLoginDTO loginDTO)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == loginDTO.Username && u.Status == UserStatus.ACTIVE);
                var passwordResult = _passwordHasher.VerifyHashedPassword(user!, user?.PasswordHash ?? UnknownUserPasswordHash, loginDTO.Password);

                // THE SAME ANSWER FOR AN UNKNOWN USERNAME AND FOR A WRONG PASSWORD
                if (user == null || passwordResult == PasswordVerificationResult.Failed)
                    return Unauthorized(new { message = "Invalid username or password" });

                // THE HASH WAS MADE WITH WEAKER SETTINGS THAN THE CURRENT ONES: IT IS REPLACED NOW THAT THE PASSWORD IS KNOWN
                if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
                    user.PasswordHash = _passwordHasher.HashPassword(user, loginDTO.Password);

                var (refreshToken, refreshTokenEntity) = _authTokenService.CreateRefreshToken(user.Id, GetIpAddress());
                await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
                await _dbContext.SaveChangesAsync();

                return Ok(CreateTokensDTO(user, refreshToken, refreshTokenEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOGIN ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT LOG IN" });
            }
        }

        private AuthTokensDTO CreateTokensDTO(User user, string refreshToken, RefreshToken refreshTokenEntity)
        {
            var (accessToken, accessTokenExpiresAt) = _authTokenService.CreateAccessToken(user);

            return new AuthTokensDTO
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt = accessTokenExpiresAt,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt,
            };
        }

        // WITHOUT X-Forwarded-For FOR NOW (D-026)
        private string? GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
