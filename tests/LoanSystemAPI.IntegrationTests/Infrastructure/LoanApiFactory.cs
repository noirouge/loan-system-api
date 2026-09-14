using LoanSystemAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoanSystemAPI.IntegrationTests.Infrastructure
{
    public class LoanApiFactory : WebApplicationFactory<Program>
    {
        // FIXED ADMIN THAT THE TESTS INSERT, SO CreatedBy POINTS TO A REAL USER
        public static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-00000000a001");

        public LoanApiFactory()
        {
            // Program.cs READS THE CONNECTION STRING WHILE IT BUILDS THE APP. ENVIRONMENT VARIABLES ARE ALREADY
            // THERE AT THAT MOMENT AND THEY OVERRIDE appsettings.Development.json
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestDatabase.ConnectionString);
            Environment.SetEnvironmentVariable("AdminId", AdminId.ToString());
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }

        // LAST SAFETY NET: IF THE OVERRIDE FAILED, THE API WOULD WRITE INTO THE DEVELOPMENT DATABASE
        public void EnsureUsesTestDatabase()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var database = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString()).Database;

            if (database == null || !database.EndsWith("_test"))
                throw new InvalidOperationException($"THE API IS USING '{database}' INSTEAD OF THE TEST DATABASE");
        }
    }
}
