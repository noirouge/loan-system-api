using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    // THE TESTS ERASE ALL THE DATA, SO THEY ONLY RUN AGAINST A DATABASE WHOSE NAME ENDS WITH _test
    public static partial class TestDatabase
    {
        public const string DatabaseName = "prestamos_test";

        public static string ApiProjectDirectory { get; } = typeof(TestDatabase).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "ApiProjectDirectory").Value!;

        public static string ConnectionString { get; } = BuildConnectionString();

        // SAME HOST, USER AND PASSWORD AS THE API (appsettings.Development.json), ONLY THE DATABASE CHANGES.
        // THE ENVIRONMENT VARIABLE LOANSYSTEM_TEST_CONNECTION REPLACES IT COMPLETELY IF IT EXISTS
        private static string BuildConnectionString()
        {
            NpgsqlConnectionStringBuilder builder;
            var fromEnvironment = Environment.GetEnvironmentVariable("LOANSYSTEM_TEST_CONNECTION");

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                builder = new NpgsqlConnectionStringBuilder(fromEnvironment);
            }
            else
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(ApiProjectDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .Build();
                var apiConnectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("NOT FOUND DefaultConnection IN THE API appsettings");
                builder = new NpgsqlConnectionStringBuilder(apiConnectionString) { Database = DatabaseName };
            }

            // ONLY LETTERS, NUMBERS AND _ : THE NAME GOES INSIDE CREATE DATABASE, WHICH DOES NOT ACCEPT PARAMETERS
            if (builder.Database == null || !Regex.IsMatch(builder.Database, "^[a-z0-9_]+_test$"))
                throw new InvalidOperationException($"THE TEST DATABASE MUST END WITH _test, BUT IT IS '{builder.Database}'");

            return builder.ConnectionString;
        }

        // CREATES THE DATABASE IF IT DOES NOT EXIST AND REBUILDS THE WHOLE SCHEMA FROM db/schema.sql
        public static async Task RecreateAsync()
        {
            var databaseName = new NpgsqlConnectionStringBuilder(ConnectionString).Database!;
            var maintenance = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres", Pooling = false };

            await using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
            {
                await connection.OpenAsync();
                await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
                exists.Parameters.AddWithValue("name", databaseName);

                if (await exists.ExecuteScalarAsync() == null)
                {
                    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
                    await create.ExecuteNonQueryAsync();
                }
            }

            var schema = await File.ReadAllTextAsync(Path.Combine(ApiProjectDirectory, "db", "schema.sql"));

            await using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                await using var dropSchema = new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", connection);
                await dropSchema.ExecuteNonQueryAsync();
                await using var applySchema = new NpgsqlCommand(schema, connection);
                await applySchema.ExecuteNonQueryAsync();
            }
        }
    }
}
