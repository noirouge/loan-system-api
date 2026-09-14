using LoanSystemAPI.IntegrationTests.CashEntries;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    // SHORTCUTS TO CALL THE LOAN ENDPOINTS FROM THE TESTS
    public static class LoansApi
    {
        public const string DefaultLoanDate = "2026-01-15";

        public static async Task<Guid> ReadIdAsync(this HttpResponseMessage response)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetGuid();
        }

        public static async Task<Guid> CreateCustomerAsync(this HttpClient client, string fullname = "Fulanito")
        {
            var response = await client.PostAsJsonAsync("/api/customers", new { fullname });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        public static Task<HttpResponseMessage> PostLoanAsync(this HttpClient client, Guid customerId, decimal principal, decimal interestRate = 0.10m, string loanDate = DefaultLoanDate, int? term = null, int paymentDay = 25)
        {
            return client.PostAsJsonAsync("/api/loans", new { customerId, principal, interestRate, loanDate, term, paymentDay });
        }

        // PUTS THE CASH IN FIRST, SO THE DISBURSEMENT IS NEVER REJECTED FOR LACK OF MONEY
        public static async Task<Guid> CreateLoanAsync(this HttpClient client, decimal principal = 1000m, decimal interestRate = 0.10m, int? term = null, string loanDate = DefaultLoanDate)
        {
            await client.PostContributionAsync(principal);
            var customerId = await client.CreateCustomerAsync();

            var response = await client.PostLoanAsync(customerId, principal, interestRate, loanDate, term);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            return await response.ReadIdAsync();
        }

        public static Task<JsonElement> GetLoanAsync(this HttpClient client, Guid loanId)
        {
            return client.GetFromJsonAsync<JsonElement>($"/api/loans/{loanId}");
        }

        public static decimal BalancePrincipal(this JsonElement loan)
        {
            return loan.GetProperty("balance").GetProperty("principal").GetDecimal();
        }

        public static decimal BalanceInterest(this JsonElement loan)
        {
            return loan.GetProperty("balance").GetProperty("interest").GetDecimal();
        }

        public static decimal BalanceTotal(this JsonElement loan)
        {
            return loan.GetProperty("balance").GetProperty("total").GetDecimal();
        }
    }
}
