using LoanSystemAPI.Data;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    public static class PaymentsApi
    {
        public const string DefaultPaymentDate = "2026-02-10";
        public static readonly DateOnly February = new(2026, 2, 1);

        public static Task<HttpResponseMessage> PostPaymentAsync(this HttpClient client, Guid loanId, decimal amount, Guid? idempotencyKey = null, string valueDate = DefaultPaymentDate, string? note = null)
        {
            return client.PostAsJsonAsync($"/api/loans/{loanId}/payments", new { amount, valueDate, note, idempotencyKey = idempotencyKey ?? Guid.NewGuid() });
        }

        public static async Task<List<JsonElement>> GetEntriesAsync(this HttpClient client, Guid loanId, LoanEntryType entryType)
        {
            var loan = await client.GetLoanAsync(loanId);
            return loan.GetProperty("entries").EnumerateArray()
                .Where(e => e.GetProperty("entryType").GetInt32() == (int)entryType)
                .ToList();
        }

        // THE MONTHLY JOB DOES NOT EXIST YET: THE TESTS WRITE THE INTEREST CHARGE DIRECTLY, THE WAY THE JOB WILL
        public static async Task AddInterestChargeAsync(this LoanApiFactory factory, Guid loanId, decimal interest, DateOnly period)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.LoanEntries.Add(new LoanEntry
            {
                Id = Guid.NewGuid(),
                LoanId = loanId,
                EntryType = LoanEntryType.INTERESTCHARGE,
                Principal = 0m,
                Interest = interest,
                Period = period,
                ValueDate = period,
                CreatedBy = LoanApiFactory.AdminId,
            });
            await dbContext.SaveChangesAsync();
        }
    }
}
