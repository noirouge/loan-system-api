using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Auth
{
    [Collection(ApiCollection.Name)]
    public class AuthApiTests : IntegrationTest
    {
        public AuthApiTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TheAdminOfTheTests_CanLogInWithItsPassword()
        {
            var response = await Client.LoginAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AUserAddedByTheTests_CanLogInWithItsPassword()
        {
            await Factory.AddUserAsync("worker", UserRole.WORKER, "worker-password");

            var response = await Client.LoginAsync("worker", "worker-password");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void CreateAccessToken_SignsAJwtForThatUserAndRole()
        {
            var userId = Guid.NewGuid();

            var token = new JsonWebToken(Factory.CreateAccessToken(userId, UserRole.WORKER));

            Assert.Equal(userId.ToString(), token.Subject);
            Assert.Equal("WORKER", token.GetClaim("role").Value);
            Assert.Equal(FakeTimeProvider.DefaultUtcNow.AddMinutes(15).UtcDateTime, token.ValidTo);
        }
    }
}
