using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options):base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
           modelBuilder.Entity<User>().ToTable("users");
           modelBuilder.Entity<Customer>().ToTable("customers").HasQueryFilter(c => c.Status != CustomerStatus.DELETED);
            modelBuilder.Entity<CashEntry>().ToTable("cash_entries");
            modelBuilder.Entity<Loan>().ToTable("loans").HasQueryFilter(l => l.Status != LoanStatus.DELETED);
            modelBuilder.Entity<LoanEntry>().ToTable("loan_entries");
            modelBuilder.Entity<Freeze>().ToTable("freezes").HasQueryFilter(f => f.Status != FreezeStatus.DELETED);
            modelBuilder.Entity<RefreshToken>().ToTable("refresh_tokens");
            modelBuilder.Entity<AuditLog>().ToTable("audit_logs").Property(a => a.Changes).HasColumnType("jsonb");
            modelBuilder.Entity<JobRun>().ToTable("job_runs");
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CashEntry> CashEntries { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<LoanEntry> LoanEntries { get; set; }
        public DbSet<Freeze> Freezes { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<JobRun> JobRuns { get; set; }
        
    }
}
