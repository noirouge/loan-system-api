using Npgsql;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    public static partial class TestDatabase
    {
        // EMPTIES EVERY TABLE AND INSERTS THE ADMIN OF THE TESTS, WHO CAN LOG IN WITH TestUsers.AdminPassword
        public static async Task ResetAsync(Guid adminId)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var truncate = new NpgsqlCommand(
                """
                DO $$
                DECLARE tables TEXT;
                BEGIN
                    SELECT string_agg(format('%I', tablename), ', ') INTO tables FROM pg_tables WHERE schemaname = 'public';
                    IF tables IS NOT NULL THEN
                        EXECUTE 'TRUNCATE ' || tables || ' CASCADE';
                    END IF;
                END $$;
                """, connection);
            await truncate.ExecuteNonQueryAsync();

            await using var insertAdmin = new NpgsqlCommand(
                "INSERT INTO users (id, name, lastname, username, password_hash, role) VALUES (@id, 'ADMIN', 'TEST', 'admin', @passwordHash, 2)",
                connection);
            insertAdmin.Parameters.AddWithValue("id", adminId);
            insertAdmin.Parameters.AddWithValue("passwordHash", TestUsers.AdminPasswordHash);
            await insertAdmin.ExecuteNonQueryAsync();
        }
    }
}
