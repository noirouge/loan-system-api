using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Auth
{
    // SHORTCUTS TO LOG IN AND TO CALL THE API AS A GIVEN USER
    public static class AuthApi
    {
        public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string username = TestUsers.AdminUsername, string password = TestUsers.AdminPassword)
        {
            return client.PostAsJsonAsync("/api/auth/login", new { username, password });
        }

        public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken)
        {
            return client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        }

        public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string refreshToken)
        {
            return client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        }

        public static Task<JsonElement> ReadTokensAsync(this HttpResponseMessage response)
        {
            return response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public static string AccessToken(this JsonElement tokens)
        {
            return tokens.GetProperty("accessToken").GetString()!;
        }

        public static string RefreshToken(this JsonElement tokens)
        {
            return tokens.GetProperty("refreshToken").GetString()!;
        }

        // SIGNS AN ACCESS TOKEN WITHOUT THE LOGIN, SO EVERY TEST DOES NOT PAY THE PASSWORD HASH. IT USES THE CLOCK OF THE TESTS
        public static string CreateAccessToken(this LoanApiFactory factory, Guid userId, UserRole role)
        {
            using var scope = factory.Services.CreateScope();
            var authTokenService = scope.ServiceProvider.GetRequiredService<AuthTokenService>();
            var user = new User { Id = userId, Name = "TEST", Lastname = "TEST", Username = "test", PasswordHash = "", Role = role };

            return authTokenService.CreateAccessToken(user).Token;
        }

        public static void AuthenticateAs(this HttpClient client, LoanApiFactory factory, Guid userId, UserRole role)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateAccessToken(userId, role));
        }

        public static HttpClient CreateClientAs(this LoanApiFactory factory, Guid userId, UserRole role)
        {
            var client = factory.CreateClient();
            client.AuthenticateAs(factory, userId, role);
            return client;
        }

        // INSERTS A USER DIRECTLY IN THE DATABASE, WITH ITS PASSWORD HASHED
        public static async Task<Guid> AddUserAsync(this LoanApiFactory factory, string username, UserRole role, string password = "user-test-password", UserStatus status = UserStatus.ACTIVE)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "TEST",
                Lastname = username.ToUpperInvariant(),
                Username = username,
                PasswordHash = TestUsers.HashPassword(password),
                Role = role,
                Status = status,
                CreatedBy = LoanApiFactory.AdminId,
                CreatedDate = DateTime.UtcNow,
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            return user.Id;
        }
    }
}
