using LoanSystemAPI.Data;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace LoanSystemAPI.IntegrationTests.Auth
{
    [Collection(ApiCollection.Name)]
    public class AuthorizationTests : IntegrationTest
    {
        public AuthorizationTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Theory]
        [InlineData("GET", "/api/customers")]
        [InlineData("GET", "/api/loans")]
        [InlineData("GET", "/api/cash-entries")]
        [InlineData("GET", "/api/users")]
        [InlineData("POST", "/api/cash-entries/contribution")]
        public async Task WithoutAToken_TheApiAnswersUnauthorized(string method, string url)
        {
            using var anonymous = Factory.CreateClient();

            var response = await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task TheAuthEndpoints_WorkWithoutAToken()
        {
            using var anonymous = Factory.CreateClient();

            var login = await anonymous.LoginAsync();
            var tokens = await login.ReadTokensAsync();

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await anonymous.RefreshAsync(tokens.RefreshToken())).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await anonymous.LogoutAsync(tokens.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task TheAccessTokenOfTheLogin_OpensTheApi()
        {
            using var client = Factory.CreateClient();
            var tokens = await (await client.LoginAsync()).ReadTokensAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken());

            var response = await client.GetAsync("/api/customers");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ATokenSignedWithAnotherKey_IsRejected()
        {
            var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "LoanSystemAPI",
                Audience = "LoanSystemAPI",
                IssuedAt = FakeTimeProvider.DefaultUtcNow.UtcDateTime,
                NotBefore = FakeTimeProvider.DefaultUtcNow.UtcDateTime,
                Expires = FakeTimeProvider.DefaultUtcNow.UtcDateTime.AddMinutes(15),
                Claims = new Dictionary<string, object> { ["sub"] = LoanApiFactory.AdminId.ToString(), ["role"] = "ADMIN" },
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-key-that-also-has-more-than-32-bytes")), SecurityAlgorithms.HmacSha256),
            });
            using var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

            var response = await client.GetAsync("/api/customers");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AnAccessToken_ExpiresAfter15Minutes()
        {
            Factory.Clock.SetUtcNow(FakeTimeProvider.DefaultUtcNow.AddMinutes(15).AddSeconds(-1));
            Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/customers")).StatusCode);

            Factory.Clock.SetUtcNow(FakeTimeProvider.DefaultUtcNow.AddMinutes(15));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/customers")).StatusCode);
        }

        [Fact]
        public async Task AWorker_UsesTheApi_AndIsTheCurrentUserOfWhatItCreates()
        {
            var workerId = await Factory.AddUserAsync("worker", UserRole.WORKER);
            using var worker = Factory.CreateClientAs(workerId, UserRole.WORKER);

            var customerId = await worker.CreateCustomerAsync();

            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await dbContext.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId);
            Assert.Equal(workerId, customer.CreatedBy);
            Assert.Equal(HttpStatusCode.OK, (await worker.GetAsync("/api/loans")).StatusCode);
        }

        [Fact]
        public async Task AWorker_CanWriteOffALoan()
        {
            var loanId = await Client.CreateLoanAsync();
            var workerId = await Factory.AddUserAsync("worker", UserRole.WORKER);
            using var worker = Factory.CreateClientAs(workerId, UserRole.WORKER);

            Assert.Equal(HttpStatusCode.NoContent, (await worker.PostAsync($"/api/loans/{loanId}/write-off", null)).StatusCode);
        }

        [Fact]
        public async Task AWorker_CannotManageUsers()
        {
            var workerId = await Factory.AddUserAsync("worker", UserRole.WORKER);
            using var worker = Factory.CreateClientAs(workerId, UserRole.WORKER);

            Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync($"/api/users/{workerId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsJsonAsync("/api/users", new { name = "A", lastname = "B", username = "other", password = "password-123", role = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.PutAsJsonAsync("/api/users", new { id = workerId, name = "A", lastname = "B", role = 2, status = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.DeleteAsync($"/api/users/{LoanApiFactory.AdminId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/users")).StatusCode);
        }
    }
}
