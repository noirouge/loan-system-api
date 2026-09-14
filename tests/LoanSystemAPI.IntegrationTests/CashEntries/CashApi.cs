using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.CashEntries
{
    // SHORTCUTS TO CALL THE CASH ENDPOINTS FROM THE TESTS
    public static class CashApi
    {
        public const string DefaultValueDate = "2026-09-01";

        public static Task<HttpResponseMessage> PostContributionAsync(this HttpClient client, decimal amount, string valueDate = DefaultValueDate)
        {
            return client.PostAsJsonAsync("/api/cash-entries/contribution", new { amount, valueDate, counterpartyUserId = LoanApiFactory.AdminId, note = "Test" });
        }

        public static Task<HttpResponseMessage> PostWithdrawalAsync(this HttpClient client, decimal amount, string valueDate = DefaultValueDate)
        {
            return client.PostAsJsonAsync("/api/cash-entries/withdrawal", new { amount, valueDate, counterpartyUserId = LoanApiFactory.AdminId, note = "Test" });
        }

        public static Task<HttpResponseMessage> PostExpenseAsync(this HttpClient client, decimal amount, string? counterparty = null, Guid? counterpartyUserId = null, string valueDate = DefaultValueDate)
        {
            return client.PostAsJsonAsync("/api/cash-entries/expense", new { amount, valueDate, counterparty, counterpartyUserId, note = "Test" });
        }

        public static Task<HttpResponseMessage> PostReversalAsync(this HttpClient client, Guid id)
        {
            return client.PostAsync($"/api/cash-entries/reversal/{id}", null);
        }

        public static async Task<List<JsonElement>> GetCashEntriesAsync(this HttpClient client)
        {
            var entries = await client.GetFromJsonAsync<JsonElement>("/api/cash-entries");
            return entries.EnumerateArray().ToList();
        }

        public static async Task<JsonElement> SingleEntryAsync(this HttpClient client, CashEntryType entryType)
        {
            var entries = await client.GetCashEntriesAsync();
            return Assert.Single(entries, e => e.EntryType() == entryType);
        }

        // SUM OF EVERY ENTRY, REVERSED ONES INCLUDED: THE REVERSALS CANCEL THEM BY SIGN
        public static async Task<decimal> GetCashBalanceAsync(this HttpClient client)
        {
            var entries = await client.GetCashEntriesAsync();
            return entries.Sum(e => e.Amount());
        }

        public static Guid EntryId(this JsonElement entry)
        {
            return entry.GetProperty("id").GetGuid();
        }

        public static CashEntryType EntryType(this JsonElement entry)
        {
            return (CashEntryType)entry.GetProperty("entryType").GetInt16();
        }

        public static CashEntryStatus Status(this JsonElement entry)
        {
            return (CashEntryStatus)entry.GetProperty("status").GetInt16();
        }

        public static decimal Amount(this JsonElement entry)
        {
            return entry.GetProperty("amount").GetDecimal();
        }
    }
}
