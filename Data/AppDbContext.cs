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
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CashEntry> CashEntries { get; set; }
        
    }
}
