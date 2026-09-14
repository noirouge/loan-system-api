using Npgsql;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    public class TestDatabaseTests
    {
        [Fact]
        public void ConnectionString_PointsToADatabaseEndingWithTest()
        {
            var builder = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString);

            Assert.EndsWith("_test", builder.Database);
        }

        [Fact]
        public async Task RecreateAsync_BuildsEveryTableFromSchemaSql()
        {
            await TestDatabase.RecreateAsync();

            var tables = new List<string>();
            await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname = 'public'", connection);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    tables.Add(reader.GetString(0));
            }

            Assert.Equal(
                new[] { "audit_logs", "cash_entries", "customers", "freezes", "job_runs", "loan_entries", "loans", "refresh_tokens", "users" },
                tables.OrderBy(t => t, StringComparer.Ordinal));
        }
    }
}
