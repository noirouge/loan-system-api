using LoanSystemAPI.Entities;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace LoanSystemAPI.IntegrationTests.Database
{
    // THE SEED ADMIN STILL LOGS IN WITH admin123, BUT schema.sql ONLY HAS ITS HASH (D-020)
    public class SchemaSeedTests
    {
        private static readonly string Schema = File.ReadAllText(Path.Combine(TestDatabase.ApiProjectDirectory, "db", "schema.sql"));

        [Fact]
        public void TheSeedAdmin_HasTheHashOfAdmin123_NotThePlainText()
        {
            var insert = Regex.Match(Schema, @"VALUES \(gen_random_uuid\(\), 'ADMIN', 'DEFAULT', 'admin', '([^']+)', 2\)");

            Assert.True(insert.Success);
            var hash = insert.Groups[1].Value;
            Assert.NotEqual("admin123", hash);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(null!, hash, "admin123"));
        }

        [Fact]
        public void DatabasesCreatedBefore_GetTheSameHash_OnlyIfThePasswordIsStillPlainText()
        {
            var insert = Regex.Match(Schema, @"'admin', '([^']+)', 2\)");
            var update = Regex.Match(Schema, @"UPDATE users SET password_hash = '([^']+)' WHERE username = 'admin' AND password_hash = 'admin123';");

            Assert.True(update.Success);
            Assert.Equal(insert.Groups[1].Value, update.Groups[1].Value);
        }
    }
}
