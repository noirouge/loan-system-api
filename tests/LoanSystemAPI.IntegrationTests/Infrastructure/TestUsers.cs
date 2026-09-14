using LoanSystemAPI.Entities;
using Microsoft.AspNetCore.Identity;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    // USERS OF THE TESTS. THE ADMIN PASSWORD IS HASHED ONLY ONCE: PasswordHasher IS SLOW ON PURPOSE
    public static class TestUsers
    {
        public const string AdminUsername = "admin";
        public const string AdminPassword = "admin-test-password";
        public static readonly string AdminPasswordHash = HashPassword(AdminPassword);

        public static string HashPassword(string password)
        {
            return new PasswordHasher<User>().HashPassword(null!, password);
        }
    }
}
