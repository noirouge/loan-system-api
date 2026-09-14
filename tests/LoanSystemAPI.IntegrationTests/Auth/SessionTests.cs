using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Auth
{
    [Collection(ApiCollection.Name)]
    public class SessionTests : IntegrationTest
    {
        public SessionTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<JsonElement> LoginOkAsync(string username = TestUsers.AdminUsername, string password = TestUsers.AdminPassword)
        {
            var response = await Client.LoginAsync(username, password);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.ReadTokensAsync();
        }

        private async Task<List<RefreshToken>> GetRefreshTokensAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.RefreshTokens.AsNoTracking().ToListAsync();
        }

        private async Task<RefreshToken> GetRefreshTokenAsync(string refreshToken)
        {
            var hash = AuthTokenService.HashRefreshToken(refreshToken);
            return Assert.Single(await GetRefreshTokensAsync(), t => t.TokenHash == hash);
        }

        [Fact]
        public async Task Login_ReturnsAnAccessTokenFor15MinutesAndARefreshTokenFor7Days()
        {
            var tokens = await LoginOkAsync();

            Assert.Equal(FakeTimeProvider.DefaultUtcNow.AddMinutes(15), tokens.GetProperty("accessTokenExpiresAt").GetDateTimeOffset());
            Assert.Equal(FakeTimeProvider.DefaultUtcNow.AddDays(7), tokens.GetProperty("refreshTokenExpiresAt").GetDateTimeOffset());
            var accessToken = new JsonWebToken(tokens.AccessToken());
            Assert.Equal(LoanApiFactory.AdminId.ToString(), accessToken.Subject);
            Assert.Equal("ADMIN", accessToken.GetClaim("role").Value);
        }

        [Fact]
        public async Task Login_SavesOnlyTheHashOfTheRefreshToken()
        {
            var tokens = await LoginOkAsync();

            var saved = Assert.Single(await GetRefreshTokensAsync());
            Assert.Equal(AuthTokenService.HashRefreshToken(tokens.RefreshToken()), saved.TokenHash);
            Assert.NotEqual(tokens.RefreshToken(), saved.TokenHash);
            Assert.Equal(LoanApiFactory.AdminId, saved.UserId);
            Assert.Null(saved.RevokedAt);
            Assert.Null(saved.ReplacedBy);
        }

        [Theory]
        [InlineData(TestUsers.AdminUsername, "wrong-password")]
        [InlineData("nobody", TestUsers.AdminPassword)]
        public async Task Login_WithAWrongPasswordOrAnUnknownUser_IsRejected(string username, string password)
        {
            var response = await Client.LoginAsync(username, password);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await GetRefreshTokensAsync());
        }

        [Fact]
        public async Task Login_OfAnInactiveUser_IsRejected()
        {
            await Factory.AddUserAsync("inactive", UserRole.WORKER, "inactive-password", UserStatus.INACTIVE);

            var response = await Client.LoginAsync("inactive", "inactive-password");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Refresh_IssuesANewPair_AndRetiresTheUsedToken()
        {
            var tokens = await LoginOkAsync();

            var response = await Client.RefreshAsync(tokens.RefreshToken());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var newTokens = await response.ReadTokensAsync();
            Assert.NotEqual(tokens.RefreshToken(), newTokens.RefreshToken());
            var used = await GetRefreshTokenAsync(tokens.RefreshToken());
            var issued = await GetRefreshTokenAsync(newTokens.RefreshToken());
            Assert.Equal(issued.Id, used.ReplacedBy);
            Assert.NotNull(used.RevokedAt);
            Assert.Null(issued.RevokedAt);
            Assert.Equal(HttpStatusCode.OK, (await Client.RefreshAsync(newTokens.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task ReusingARefreshTokenAlreadyReplaced_RevokesEverySessionOfTheUser()
        {
            var firstSession = await LoginOkAsync();
            var secondSession = await LoginOkAsync();
            var rotated = await (await Client.RefreshAsync(firstSession.RefreshToken())).ReadTokensAsync();

            var reuse = await Client.RefreshAsync(firstSession.RefreshToken());

            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
            Assert.All(await GetRefreshTokensAsync(), t => Assert.NotNull(t.RevokedAt));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(rotated.RefreshToken())).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(secondSession.RefreshToken())).StatusCode);
        }

        // THE SECOND ONE LOOKS LIKE A REUSE AND CLOSES THE SESSIONS: THAT FALSE POSITIVE IS ACCEPTED (D-025)
        [Fact]
        public async Task TwoRefreshesWithTheSameTokenAtTheSameTime_OnlyOneWins_AndTheSessionsAreClosed()
        {
            var tokens = await LoginOkAsync();

            var responses = await Task.WhenAll(Client.RefreshAsync(tokens.RefreshToken()), Client.RefreshAsync(tokens.RefreshToken()));

            var winner = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Unauthorized);
            var winnerTokens = await winner.ReadTokensAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(winnerTokens.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task AnExpiredRefreshToken_IsRejected()
        {
            var tokens = await LoginOkAsync();
            Factory.Clock.SetUtcNow(FakeTimeProvider.DefaultUtcNow.AddDays(7));

            var response = await Client.RefreshAsync(tokens.RefreshToken());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AnUnknownRefreshToken_IsRejected()
        {
            var response = await Client.RefreshAsync("not-a-refresh-token");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Refresh_OfAUserThatIsNoLongerActive_IsRejected()
        {
            var workerId = await Factory.AddUserAsync("worker", UserRole.WORKER, "worker-password");
            var tokens = await LoginOkAsync("worker", "worker-password");
            using (var scope = Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Users.Where(u => u.Id == workerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.INACTIVE));
            }

            var response = await Client.RefreshAsync(tokens.RefreshToken());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Logout_RevokesOnlyThatSession()
        {
            var loggedOut = await LoginOkAsync();
            var otherSession = await LoginOkAsync();

            var response = await Client.LogoutAsync(loggedOut.RefreshToken());

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.NotNull((await GetRefreshTokenAsync(loggedOut.RefreshToken())).RevokedAt);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(loggedOut.RefreshToken())).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Client.RefreshAsync(otherSession.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task Logout_WithAnUnknownToken_StillAnswersNoContent()
        {
            var response = await Client.LogoutAsync("not-a-refresh-token");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
