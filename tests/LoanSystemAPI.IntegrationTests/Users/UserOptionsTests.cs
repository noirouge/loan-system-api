using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Auth;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Users
{
    [Collection(ApiCollection.Name)]
    public class UserOptionsTests : IntegrationTest
    {
        public UserOptionsTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task AWorker_ListsTheActiveUsers_WithoutRoleOrStatus()
        {
            var workerId = await Factory.AddUserAsync("worker", UserRole.WORKER);
            await Factory.AddUserAsync("inactive", UserRole.WORKER, status: UserStatus.INACTIVE);
            await Factory.AddUserAsync("deleted", UserRole.WORKER, status: UserStatus.DELETED);
            using var worker = Factory.CreateClientAs(workerId, UserRole.WORKER);

            var response = await worker.GetAsync("/api/users/options");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var users = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            Assert.Equal(new[] { "admin", "worker" }, users.Select(u => u.GetProperty("username").GetString()).OrderBy(u => u));
            Assert.All(users, u => Assert.False(u.TryGetProperty("role", out _) || u.TryGetProperty("status", out _)));
            Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/users")).StatusCode);
        }

        [Fact]
        public async Task WithoutAToken_TheOptionsAreNotListed()
        {
            using var anonymous = Factory.CreateClient();

            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/options")).StatusCode);
        }
    }
}
