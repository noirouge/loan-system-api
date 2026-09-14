using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LoanSystemAPI.IntegrationTests.Database
{
    [Collection(ApiCollection.Name)]
    public class ConstraintsTests : IntegrationTest
    {
        private static readonly DateOnly February = new(2026, 2, 1);

        public ConstraintsTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task SaveAsync(object entity)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Add(entity);
            await dbContext.SaveChangesAsync();
        }

        // THE CUSTOMER IS SAVED FIRST: THE ENTITIES HAVE NO NAVIGATIONS, SO EF DOES NOT KNOW THE LOAN DEPENDS ON IT
        private async Task<Guid> CreateLoanAsync()
        {
            var customer = new Customer { Id = Guid.NewGuid(), Fullname = "Fulanito", CreatedBy = LoanApiFactory.AdminId };
            await SaveAsync(customer);

            var loan = new Loan
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Principal = 1000m,
                InterestRate = 0.10m,
                LoanDate = new DateOnly(2026, 1, 15),
                PaymentDay = 25,
                CreatedBy = LoanApiFactory.AdminId,
            };
            await SaveAsync(loan);

            return loan.Id;
        }

        private static LoanEntry NewEntry(Guid loanId, LoanEntryType entryType, decimal principal, decimal interest, DateOnly? period = null, Guid? reversesEntryId = null)
        {
            return new LoanEntry
            {
                Id = Guid.NewGuid(),
                LoanId = loanId,
                EntryType = entryType,
                Principal = principal,
                Interest = interest,
                Period = period,
                ReversesEntryId = reversesEntryId,
                ValueDate = February,
                CreatedBy = LoanApiFactory.AdminId,
            };
        }

        private static Freeze NewFreeze(Guid loanId, DateOnly startDate, DateOnly? endDate = null)
        {
            return new Freeze
            {
                Id = Guid.NewGuid(),
                LoanId = loanId,
                StartDate = startDate,
                EndDate = endDate,
                AuthorizedBy = LoanApiFactory.AdminId,
                CreatedBy = LoanApiFactory.AdminId,
            };
        }

        private static void AssertUniqueViolation(DbUpdateException error, string constraintName)
        {
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal(constraintName, postgres.ConstraintName);
        }

        [Fact]
        public async Task TwoInterestChargesForTheSamePeriod_AreRejected()
        {
            var loanId = await CreateLoanAsync();
            await SaveAsync(NewEntry(loanId, LoanEntryType.INTERESTCHARGE, 0m, 100m, February));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(NewEntry(loanId, LoanEntryType.INTERESTCHARGE, 0m, 100m, February)));

            AssertUniqueViolation(error, "ux_loan_entries_loan_id_and_period");
        }

        // WITH THE OLD INDEX (entry_type = 1) THIS FAILED AND TWO INTEREST CHARGES OF THE SAME MONTH WERE ALLOWED
        [Fact]
        public async Task EntriesThatAreNotInterestCharges_CanShareAPeriod()
        {
            var loanId = await CreateLoanAsync();
            await SaveAsync(NewEntry(loanId, LoanEntryType.DISBURSEMENT, 1000m, 0m, February));

            await SaveAsync(NewEntry(loanId, LoanEntryType.DISBURSEMENT, 1000m, 0m, February));
        }

        [Fact]
        public async Task TwoOpenFreezesForTheSameLoan_AreRejected()
        {
            var loanId = await CreateLoanAsync();
            await SaveAsync(NewFreeze(loanId, new DateOnly(2026, 3, 1)));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(NewFreeze(loanId, new DateOnly(2026, 4, 1))));

            AssertUniqueViolation(error, "ux_freezes_if_open");
        }

        [Fact]
        public async Task AClosedFreeze_AllowsOpeningAnotherOne()
        {
            var loanId = await CreateLoanAsync();
            await SaveAsync(NewFreeze(loanId, new DateOnly(2026, 3, 1), endDate: new DateOnly(2026, 3, 31)));

            await SaveAsync(NewFreeze(loanId, new DateOnly(2026, 4, 1)));
        }

        [Fact]
        public async Task ALoanEntryCannotBeReversedTwice()
        {
            var loanId = await CreateLoanAsync();
            var payment = NewEntry(loanId, LoanEntryType.PAYMENT, -200m, -100m);
            await SaveAsync(payment);
            await SaveAsync(NewEntry(loanId, LoanEntryType.REVERSAL, 200m, 100m, reversesEntryId: payment.Id));

            var error = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(NewEntry(loanId, LoanEntryType.REVERSAL, 200m, 100m, reversesEntryId: payment.Id)));

            AssertUniqueViolation(error, "uq_loan_entries_reverses_entry_id");
        }

        [Fact]
        public async Task SchemaSql_CanRunAgainOnADatabaseWithData()
        {
            await CreateLoanAsync();
            var schema = await File.ReadAllTextAsync(Path.Combine(TestDatabase.ApiProjectDirectory, "db", "schema.sql"));

            await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(schema, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
