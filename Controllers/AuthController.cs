using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/auth")]
    // THE LOGIN, THE REFRESH AND THE LOGOUT ARE CALLED WITHOUT AN ACCESS TOKEN, OR WITH ONE ALREADY EXPIRED
    [AllowAnonymous]
    public class AuthController : Controller
    {
        // HASH OF A RANDOM PASSWORD: AN UNKNOWN USERNAME ALSO PAYS ONE VERIFICATION, SO THE RESPONSE TIME DOES NOT REVEAL WHICH USERNAMES EXIST
        private static readonly string UnknownUserPasswordHash = new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString());
        private const int AttemptedUserMaxLength = 100;

        private readonly ILogger<AuthController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly AuthTokenService _authTokenService;
        private readonly AuditLogWriter _auditLogWriter;
        private readonly TimeProvider _timeProvider;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public AuthController(ILogger<AuthController> logger, AppDbContext dbContext, AuthTokenService authTokenService, AuditLogWriter auditLogWriter, TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _authTokenService = authTokenService;
            _auditLogWriter = auditLogWriter;
            _timeProvider = timeProvider;
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthTokensDTO>> Login([FromBody] AuthLoginDTO loginDTO)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == loginDTO.Username);
                var passwordResult = _passwordHasher.VerifyHashedPassword(user!, user?.PasswordHash ?? UnknownUserPasswordHash, loginDTO.Password);

                // THE SAME ANSWER FOR AN UNKNOWN USERNAME, A WRONG PASSWORD AND A USER THAT IS NOT ACTIVE
                if (user == null || user.Status != UserStatus.ACTIVE || passwordResult == PasswordVerificationResult.Failed)
                {
                    await WriteSessionAuditAsync(AuditAction.LOGINFAILED, user?.Id, loginDTO.Username);
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                // THE HASH WAS MADE WITH WEAKER SETTINGS THAN THE CURRENT ONES: IT IS REPLACED NOW THAT THE PASSWORD IS KNOWN
                if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
                    user.PasswordHash = _passwordHasher.HashPassword(user, loginDTO.Password);

                var (refreshToken, refreshTokenEntity) = _authTokenService.CreateRefreshToken(user.Id, GetIpAddress());
                await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
                await _dbContext.SaveChangesAsync();

                await WriteSessionAuditAsync(AuditAction.LOGIN, user.Id, loginDTO.Username);

                return Ok(CreateTokensDTO(user, refreshToken, refreshTokenEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOGIN ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT LOG IN" });
            }
        }

        // EVERY USE OF A REFRESH TOKEN RETIRES IT AND ISSUES A NEW PAIR. A TOKEN ALREADY REPLACED THAT ARRIVES AGAIN WAS COPIED
        // BY SOMEONE: ALL THE SESSIONS OF THE USER ARE REVOKED (D-024), EVEN IF IT WAS TWO TABS REFRESHING AT ONCE (D-025)
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthTokensDTO>> Refresh([FromBody] AuthRefreshTokenDTO refreshDTO)
        {
            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var tokenHash = AuthTokenService.HashRefreshToken(refreshDTO.RefreshToken);

                var usedToken = await _dbContext.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
                if (usedToken == null)
                    return Unauthorized(new { message = "Invalid refresh token" });

                if (usedToken.ReplacedBy != null)
                {
                    await RevokeUserSessionsAsync(usedToken.UserId, now);
                    return Unauthorized(new { message = "This refresh token was already used. All the sessions of the user were closed" });
                }

                if (usedToken.RevokedAt != null || usedToken.ExpiresAt <= now)
                    return Unauthorized(new { message = "The refresh token expired or was revoked" });

                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == usedToken.UserId && u.Status == UserStatus.ACTIVE);
                if (user == null)
                    return Unauthorized(new { message = "Invalid refresh token" });

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                var (refreshToken, refreshTokenEntity) = _authTokenService.CreateRefreshToken(user.Id, GetIpAddress());
                await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
                await _dbContext.SaveChangesAsync();

                // THE CONDITION ON replaced_by LETS ONLY ONE REQUEST RETIRE THE TOKEN: OF TWO SIMULTANEOUS REFRESHES,
                // THE SECOND ONE WAITS FOR THE ROW AND THEN FINDS IT ALREADY REPLACED
                var retiredRows = await _dbContext.RefreshTokens
                    .Where(t => t.Id == usedToken.Id && t.ReplacedBy == null && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.ReplacedBy, (Guid?)refreshTokenEntity.Id)
                        .SetProperty(t => t.RevokedAt, (DateTime?)now));

                if (retiredRows == 0)
                {
                    await transaction.RollbackAsync();
                    await RevokeUserSessionsAsync(usedToken.UserId, now);
                    return Unauthorized(new { message = "This refresh token was already used. All the sessions of the user were closed" });
                }

                await transaction.CommitAsync();

                return Ok(CreateTokensDTO(user, refreshToken, refreshTokenEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "REFRESH TOKEN ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT REFRESH THE SESSION" });
            }
        }

        // CLOSES THE SESSION OF THIS REFRESH TOKEN. THE ACCESS TOKEN STILL WORKS UNTIL IT EXPIRES: THAT IS WHY IT IS SHORT
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] AuthRefreshTokenDTO logoutDTO)
        {
            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var tokenHash = AuthTokenService.HashRefreshToken(logoutDTO.RefreshToken);

                var token = await _dbContext.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null);
                if (token != null)
                {
                    var revokedRows = await _dbContext.RefreshTokens
                        .Where(t => t.Id == token.Id && t.RevokedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now));

                    if (revokedRows > 0)
                        await WriteSessionAuditAsync(AuditAction.LOGOUT, token.UserId, null);
                }

                // THE SAME ANSWER WHETHER THE TOKEN EXISTED OR NOT
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOGOUT ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT LOG OUT" });
            }
        }

        private async Task RevokeUserSessionsAsync(Guid userId, DateTime now)
        {
            await _dbContext.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now));
        }

        // LOGIN, LOGINFAILED AND LOGOUT DO NOT CHANGE ANY AUDITED ENTITY, SO THE INTERCEPTOR DOES NOT SEE THEM (D-028).
        // attempted_user KEEPS WHAT WAS TYPED, ALSO WHEN THE USER DOES NOT EXIST
        private Task WriteSessionAuditAsync(AuditAction action, Guid? userId, string? attemptedUser)
        {
            return _auditLogWriter.WriteAsync(new[]
            {
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = action,
                    UserId = userId,
                    AttemptedUser = attemptedUser?.Length > AttemptedUserMaxLength ? attemptedUser[..AttemptedUserMaxLength] : attemptedUser,
                    IpAddress = GetIpAddress(),
                    CreatedDate = DateTime.UtcNow,
                },
            });
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
