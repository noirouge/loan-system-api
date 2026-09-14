using LoanSystemAPI.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace LoanSystemAPI.Services
{
    // CREATES THE TWO TOKENS OF A SESSION: THE ACCESS JWT, THAT THE API VALIDATES ONLY BY ITS SIGNATURE,
    // AND THE REFRESH TOKEN, THAT THE DATABASE KEEPS ONLY AS A HASH. THE DATES COME FROM THE TimeProvider
    public class AuthTokenService
    {
        public const string UserIdClaim = "sub";
        public const string UsernameClaim = "unique_name";
        public const string RoleClaim = "role";

        private readonly IConfiguration _configuration;
        private readonly TimeProvider _timeProvider;

        public AuthTokenService(IConfiguration configuration, TimeProvider timeProvider)
        {
            _configuration = configuration;
            _timeProvider = timeProvider;
        }

        public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.AddMinutes(_configuration.GetValue("Jwt:AccessTokenMinutes", 15));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                IssuedAt = now,
                NotBefore = now,
                Expires = expiresAt,
                Claims = new Dictionary<string, object>
                {
                    [UserIdClaim] = user.Id.ToString(),
                    [UsernameClaim] = user.Username,
                    [RoleClaim] = user.Role.ToString(),
                },
                SigningCredentials = new SigningCredentials(GetSigningKey(_configuration), SecurityAlgorithms.HmacSha256),
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
        }

        // 32 RANDOM BYTES. THE CLIENT RECEIVES THE TOKEN AND THE DATABASE ONLY ITS HASH
        public (string Token, RefreshToken Entity) CreateRefreshToken(Guid userId, string? ipAddress)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashRefreshToken(token),
                ExpiresAt = now.AddDays(_configuration.GetValue("Jwt:RefreshTokenDays", 7)),
                IpAddress = ipAddress,
                CreatedDate = now,
            };

            return (token, entity);
        }

        // THE TOKEN IS ALREADY RANDOM WITH HIGH ENTROPY, SO A FAST SHA-256 IS ENOUGH; IT DOES NOT NEED A SLOW PASSWORD HASH (D-021)
        public static string HashRefreshToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        // THE KEY LIVES IN THE USER SECRETS (D-022). HS256 NEEDS AT LEAST 32 BYTES
        public static SymmetricSecurityKey GetSigningKey(IConfiguration configuration)
        {
            var signingKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("NOT FOUND Jwt:SigningKey");
            var keyBytes = Encoding.UTF8.GetBytes(signingKey);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("Jwt:SigningKey MUST HAVE AT LEAST 32 BYTES");

            return new SymmetricSecurityKey(keyBytes);
        }
    }
}
