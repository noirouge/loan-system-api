using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Auth;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Users
{
    [Collection(ApiCollection.Name)]
    public class UsersTests : IntegrationTest
    {
        private const string Password = "worker-password";

        public UsersTests(ApiFixture fixture) : base(fixture)
        {
        }

        private Task<HttpResponseMessage> PostUserAsync(string username = "worker", string password = Password, string name = "Juan", string lastname = "Perez", int role = (int)UserRole.WORKER)
        {
            return Client.PostAsJsonAsync("/api/users", new { name, lastname, username, password, role });
        }

        private async Task<Guid> CreateUserAsync(string username = "worker")
        {
            var response = await PostUserAsync(username);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        private Task<HttpResponseMessage> PutUserAsync(Guid id, UserRole role = UserRole.WORKER, UserStatus status = UserStatus.ACTIVE, string name = "Juan", string? password = null)
        {
            return Client.PutAsJsonAsync("/api/users", new { id, name, lastname = "Perez", role, status, password });
        }

        [Fact]
        public async Task CreatingAUser_ReturnsItWithoutThePassword_AndTheUserCanLogIn()
        {
            var response = await PostUserAsync();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var user = await Client.GetFromJsonAsync<JsonElement>($"/api/users/{await response.ReadIdAsync()}");
            Assert.Equal("worker", user.GetProperty("username").GetString());
            Assert.Equal((int)UserRole.WORKER, user.GetProperty("role").GetInt32());
            Assert.Equal((int)UserStatus.ACTIVE, user.GetProperty("status").GetInt32());
            Assert.False(user.TryGetProperty("passwordHash", out _));
            Assert.False(user.TryGetProperty("password", out _));
            Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync("worker", Password)).StatusCode);
        }

        [Fact]
        public async Task ARepeatedUsername_IsRejectedWithConflict()
        {
            await CreateUserAsync();

            var response = await PostUserAsync();

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Theory]
        [InlineData("worker", "short", "Juan", 1)]
        [InlineData("worker", Password, "  ", 1)]
        [InlineData("  ", Password, "Juan", 1)]
        [InlineData("worker", Password, "Juan", 99)]
        public async Task InvalidUserData_IsRejected(string username, string password, string name, int role)
        {
            var response = await PostUserAsync(username, password, name, role: role);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TheList_ShowsTheActiveAndInactiveUsers_ButNotTheDeletedOnes()
        {
            await CreateUserAsync("active");
            var inactiveId = await CreateUserAsync("inactive");
            var deletedId = await CreateUserAsync("deleted");
            await PutUserAsync(inactiveId, status: UserStatus.INACTIVE);
            await Client.DeleteAsync($"/api/users/{deletedId}");

            var users = await Client.GetFromJsonAsync<JsonElement>("/api/users");

            var usernames = users.EnumerateArray().Select(u => u.GetProperty("username").GetString()).ToList();
            Assert.Equal(new[] { "active", "admin", "inactive" }, usernames);
        }

        [Fact]
        public async Task ChangingTheRole_UpdatesTheUser_AndClosesItsSessions()
        {
            var userId = await CreateUserAsync();
            var tokens = await (await Client.LoginAsync("worker", Password)).ReadTokensAsync();

            var response = await PutUserAsync(userId, role: UserRole.ADMIN, name: "Juana");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var user = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Juana", user.GetProperty("name").GetString());
            Assert.Equal((int)UserRole.ADMIN, user.GetProperty("role").GetInt32());
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(tokens.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task ChangingOnlyTheName_KeepsTheSessions()
        {
            var userId = await CreateUserAsync();
            var tokens = await (await Client.LoginAsync("worker", Password)).ReadTokensAsync();

            await PutUserAsync(userId, name: "Juana");

            Assert.Equal(HttpStatusCode.OK, (await Client.RefreshAsync(tokens.RefreshToken())).StatusCode);
        }

        [Fact]
        public async Task ChangingThePassword_OnlyTheNewOneLogsIn()
        {
            var userId = await CreateUserAsync();

            await PutUserAsync(userId, password: "new-password");

            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.LoginAsync("worker", Password)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Client.LoginAsync("worker", "new-password")).StatusCode);
        }

        [Fact]
        public async Task AnInactiveUser_CannotLogIn()
        {
            var userId = await CreateUserAsync();

            await PutUserAsync(userId, status: UserStatus.INACTIVE);

            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.LoginAsync("worker", Password)).StatusCode);
        }

        [Fact]
        public async Task TheStatusDeleted_IsNotAcceptedByTheUpdate()
        {
            var userId = await CreateUserAsync();

            var response = await PutUserAsync(userId, status: UserStatus.DELETED);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AnAdmin_CannotChangeTheirOwnRoleOrStatus_ButCanChangeTheirName()
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await PutUserAsync(LoanApiFactory.AdminId, role: UserRole.WORKER)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutUserAsync(LoanApiFactory.AdminId, role: UserRole.ADMIN, status: UserStatus.INACTIVE)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await PutUserAsync(LoanApiFactory.AdminId, role: UserRole.ADMIN, name: "Dueño")).StatusCode);
        }

        [Fact]
        public async Task DeletingAUser_HidesIt_ClosesItsSessions_AndBlocksTheLogin()
        {
            var userId = await CreateUserAsync();
            var tokens = await (await Client.LoginAsync("worker", Password)).ReadTokensAsync();

            var response = await Client.DeleteAsync($"/api/users/{userId}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/users/{userId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.RefreshAsync(tokens.RefreshToken())).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client.LoginAsync("worker", Password)).StatusCode);
        }

        [Fact]
        public async Task AnAdmin_CannotDeleteThemselves()
        {
            var response = await Client.DeleteAsync($"/api/users/{LoanApiFactory.AdminId}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnknownUsers_ReturnNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PutUserAsync(Guid.NewGuid())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
        }
    }
}
