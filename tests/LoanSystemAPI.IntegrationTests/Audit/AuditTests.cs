using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Auth;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Audit
{
    [Collection(ApiCollection.Name)]
    public class AuditTests : IntegrationTest
    {
        public AuditTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.AuditLogs.AsNoTracking().ToListAsync();
        }

        private static JsonElement Changes(AuditLog auditLog)
        {
            return JsonDocument.Parse(auditLog.Changes!).RootElement;
        }

        private async Task<Guid> PostCustomerAsync()
        {
            var response = await Client.PostAsJsonAsync("/api/customers", new { fullname = "Fulanito", code = "C-1", phone = "809-555-1234", note = "Test" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        [Fact]
        public async Task CreatingACustomer_IsAuditedAsCreate_WithEveryColumnAsNew()
        {
            var customerId = await PostCustomerAsync();

            var auditLog = Assert.Single(await GetAuditLogsAsync(), a => a.EntityName == "customers");
            Assert.Equal(AuditAction.CREATE, auditLog.Action);
            Assert.Equal(customerId, auditLog.EntityId);
            Assert.Equal(LoanApiFactory.AdminId, auditLog.UserId);
            var changes = Changes(auditLog);
            Assert.Equal("Fulanito", changes.GetProperty("fullname").GetProperty("new").GetString());
            Assert.False(changes.GetProperty("fullname").TryGetProperty("old", out _));
            Assert.Equal((int)CustomerStatus.ACTIVE, changes.GetProperty("status").GetProperty("new").GetInt32());
            Assert.Equal(LoanApiFactory.AdminId, changes.GetProperty("created_by").GetProperty("new").GetGuid());
        }

        [Fact]
        public async Task UpdatingACustomer_KeepsOnlyTheColumnsThatChanged()
        {
            var customerId = await PostCustomerAsync();

            await Client.PutAsJsonAsync("/api/customers", new { id = customerId, fullname = "Fulanito", code = "C-1", phone = "849-555-9876", note = "Test" });

            var auditLog = Assert.Single(await GetAuditLogsAsync(), a => a.EntityName == "customers" && a.Action == AuditAction.UPDATE);
            var changes = Changes(auditLog);
            Assert.Equal("809-555-1234", changes.GetProperty("phone").GetProperty("old").GetString());
            Assert.Equal("849-555-9876", changes.GetProperty("phone").GetProperty("new").GetString());
            Assert.False(changes.TryGetProperty("fullname", out _));
            Assert.False(changes.TryGetProperty("code", out _));
        }

        [Fact]
        public async Task TheLogicalDeleteOfACustomer_IsAuditedAsDelete_WithEveryColumnAsOld()
        {
            var customerId = await PostCustomerAsync();

            await Client.DeleteAsync($"/api/customers/{customerId}");

            var auditLog = Assert.Single(await GetAuditLogsAsync(), a => a.EntityName == "customers" && a.Action != AuditAction.CREATE);
            Assert.Equal(AuditAction.DELETE, auditLog.Action);
            var changes = Changes(auditLog);
            Assert.Equal("Fulanito", changes.GetProperty("fullname").GetProperty("old").GetString());
            Assert.Equal((int)CustomerStatus.ACTIVE, changes.GetProperty("status").GetProperty("old").GetInt32());
            Assert.False(changes.GetProperty("fullname").TryGetProperty("new", out _));
        }

        [Fact]
        public async Task TheLedgers_AreAudited_WithTheirInsertsAndStatusChanges()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var paymentId = await (await Client.PostPaymentAsync(loanId, 100m)).ReadIdAsync();

            await Client.PostAsync($"/api/loans/entries/{paymentId}/reversal", null);

            var auditLogs = await GetAuditLogsAsync();
            Assert.Single(auditLogs, a => a.EntityName == "loans" && a.Action == AuditAction.CREATE);
            Assert.Equal(3, auditLogs.Count(a => a.EntityName == "loan_entries" && a.Action == AuditAction.CREATE));
            Assert.Equal(4, auditLogs.Count(a => a.EntityName == "cash_entries" && a.Action == AuditAction.CREATE));
            var reversedPayment = Assert.Single(auditLogs, a => a.EntityName == "loan_entries" && a.Action == AuditAction.UPDATE);
            Assert.Equal(paymentId, reversedPayment.EntityId);
            Assert.Equal((int)LoanEntryStatus.APPLIED, Changes(reversedPayment).GetProperty("status").GetProperty("old").GetInt32());
            Assert.Equal((int)LoanEntryStatus.REVERSED, Changes(reversedPayment).GetProperty("status").GetProperty("new").GetInt32());
            Assert.Single(auditLogs, a => a.EntityName == "cash_entries" && a.Action == AuditAction.UPDATE);
        }

        [Fact]
        public async Task PasswordsTokensAndInfrastructureTables_AreNeverAudited()
        {
            var response = await Client.PostAsJsonAsync("/api/users", new { name = "Juan", lastname = "Perez", username = "worker", password = "worker-password", role = 1 });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var tokens = await (await Client.LoginAsync("worker", "worker-password")).ReadTokensAsync();
            await Client.RefreshAsync(tokens.RefreshToken());
            using (var scope = Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<JobRunner>().RunAsync("test-job", null, _ => Task.FromResult(new JobRunCounters()));
            }

            var auditLogs = await GetAuditLogsAsync();

            var createdUser = Assert.Single(auditLogs, a => a.EntityName == "users");
            Assert.Equal("worker", Changes(createdUser).GetProperty("username").GetProperty("new").GetString());
            Assert.All(auditLogs, a => Assert.DoesNotContain(a.EntityName, new[] { "refresh_tokens", "job_runs", "audit_logs" }));
            Assert.All(auditLogs.Where(a => a.Changes != null), a =>
            {
                Assert.DoesNotContain("password_hash", a.Changes);
                Assert.DoesNotContain("token_hash", a.Changes);
            });
        }

        [Fact]
        public async Task AChangeWithoutARequest_IsAuditedWithoutUser()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);

            var charge = Assert.Single(await GetAuditLogsAsync(), a => a.EntityName == "loan_entries" && Changes(a).GetProperty("entry_type").GetProperty("new").GetInt32() == (int)LoanEntryType.INTERESTCHARGE);
            Assert.Null(charge.UserId);
        }

        [Fact]
        public async Task ASuccessfulLogin_IsAuditedAsLogin()
        {
            await Client.LoginAsync();

            var login = Assert.Single(await GetAuditLogsAsync());
            Assert.Equal(AuditAction.LOGIN, login.Action);
            Assert.Equal(LoanApiFactory.AdminId, login.UserId);
            Assert.Equal(TestUsers.AdminUsername, login.AttemptedUser);
            Assert.Null(login.EntityName);
        }

        [Fact]
        public async Task FailedLogins_KeepWhatWasTyped_AndTheUserOnlyIfItExists()
        {
            await Client.LoginAsync(TestUsers.AdminUsername, "wrong-password");
            await Client.LoginAsync("nobody", "any-password");

            var failedLogins = await GetAuditLogsAsync();
            Assert.All(failedLogins, a => Assert.Equal(AuditAction.LOGINFAILED, a.Action));
            Assert.Equal(LoanApiFactory.AdminId, Assert.Single(failedLogins, a => a.AttemptedUser == TestUsers.AdminUsername).UserId);
            Assert.Null(Assert.Single(failedLogins, a => a.AttemptedUser == "nobody").UserId);
        }

        [Fact]
        public async Task ALogoutThatClosesASession_IsAuditedAsLogout()
        {
            var tokens = await (await Client.LoginAsync()).ReadTokensAsync();

            await Client.LogoutAsync(tokens.RefreshToken());
            await Client.LogoutAsync(tokens.RefreshToken());
            await Client.LogoutAsync("not-a-refresh-token");

            var logout = Assert.Single(await GetAuditLogsAsync(), a => a.Action == AuditAction.LOGOUT);
            Assert.Equal(LoanApiFactory.AdminId, logout.UserId);
        }
    }
}
