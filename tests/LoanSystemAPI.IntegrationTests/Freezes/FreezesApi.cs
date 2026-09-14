using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Freezes
{
    // SHORTCUTS TO CALL THE FREEZE ENDPOINTS FROM THE TESTS
    public static class FreezesApi
    {
        public const string DefaultStartDate = "2026-03-01";
        public const string DefaultEndDate = "2026-04-01";

        public static Task<HttpResponseMessage> PostFreezeAsync(this HttpClient client, Guid loanId, string startDate = DefaultStartDate, string? reason = "Test")
        {
            return client.PostAsJsonAsync($"/api/loans/{loanId}/freezes", new { startDate, reason });
        }

        public static Task<HttpResponseMessage> CloseFreezeAsync(this HttpClient client, Guid freezeId, string endDate = DefaultEndDate)
        {
            return client.PostAsJsonAsync($"/api/freezes/{freezeId}/close", new { endDate });
        }

        public static async Task<List<JsonElement>> GetFreezesAsync(this HttpClient client, Guid loanId)
        {
            var freezes = await client.GetFromJsonAsync<JsonElement>($"/api/loans/{loanId}/freezes");
            return freezes.EnumerateArray().ToList();
        }

        public static string? EndDate(this JsonElement freeze)
        {
            return freeze.GetProperty("endDate").GetString();
        }
    }
}
