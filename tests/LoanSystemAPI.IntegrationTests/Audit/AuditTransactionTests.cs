using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Net;
using System.Net.Http.Json;

namespace LoanSystemAPI.IntegrationTests.Audit
{
    // THE BUSINESS CHANGE WINS (D-030): THE AUDIT LOG IS WRITTEN AFTER THE COMMIT, AND ITS FAILURE DOES NOT UNDO THE CHANGE
    [Collection(ApiCollection.Name)]
    public class AuditTransactionTests : IntegrationTest
    {
        public AuditTransactionTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<List<AuditLog>> GetAuditLogsAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.AuditLogs.AsNoTracking().ToListAsync();
        }

        private static Customer NewCustomer(string fullname)
        {
            return new Customer { Id = Guid.NewGuid(), Fullname = fullname, CreatedBy = LoanApiFactory.AdminId };
        }

        private static async Task ExecuteSqlAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        [Fact]
        public async Task AChangeInsideATransaction_IsAuditedOnlyAfterTheCommit()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            dbContext.Customers.Add(NewCustomer("Fulanito"));
            await dbContext.SaveChangesAsync();

            Assert.Empty(await GetAuditLogsAsync());

            await transaction.CommitAsync();

            var auditLog = Assert.Single(await GetAuditLogsAsync());
            Assert.Equal(AuditAction.CREATE, auditLog.Action);
        }

        [Fact]
        public async Task ARolledBackChange_IsNotAudited()
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using (var transaction = await dbContext.Database.BeginTransactionAsync())
            {
                dbContext.Customers.Add(NewCustomer("Rolled back"));
                await dbContext.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            dbContext.ChangeTracker.Clear();

            dbContext.Customers.Add(NewCustomer("Saved"));
            await dbContext.SaveChangesAsync();

            var auditLog = Assert.Single(await GetAuditLogsAsync());
            Assert.Contains("Saved", auditLog.Changes);
        }

        [Fact]
        public async Task IfTheAuditLogCannotBeWritten_TheBusinessChangeStaysSaved()
        {
            await ExecuteSqlAsync("ALTER TABLE audit_logs RENAME TO audit_logs_blocked");
            try
            {
                var response = await Client.PostAsJsonAsync("/api/customers", new { fullname = "Fulanito" });

                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/customers/{await response.ReadIdAsync()}")).StatusCode);
            }
            finally
            {
                await ExecuteSqlAsync("ALTER TABLE audit_logs_blocked RENAME TO audit_logs");
            }

            Assert.Empty(await GetAuditLogsAsync());
        }
    }
}
