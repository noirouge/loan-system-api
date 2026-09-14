using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class LoanDisbursementTests : IntegrationTest
    {
        public LoanDisbursementTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task CreatingALoan_SavesTheLoanTheDisbursementAndTheCashEntry()
        {
            await Client.PostContributionAsync(1000m);
            var customerId = await Client.CreateCustomerAsync();

            var response = await Client.PostLoanAsync(customerId, 1000m);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var loan = await Client.GetLoanAsync(await response.ReadIdAsync());
            Assert.Equal((int)LoanStatus.ACTIVE, loan.GetProperty("status").GetInt32());
            Assert.Equal(1000m, loan.BalancePrincipal());
            Assert.Equal(0m, loan.BalanceInterest());

            var disbursement = Assert.Single(loan.GetProperty("entries").EnumerateArray());
            Assert.Equal((int)LoanEntryType.DISBURSEMENT, disbursement.GetProperty("entryType").GetInt32());
            Assert.Equal(1000m, disbursement.GetProperty("principal").GetDecimal());
            Assert.Equal(LoansApi.DefaultLoanDate, disbursement.GetProperty("valueDate").GetString());

            var cash = await Client.SingleEntryAsync(CashEntryType.DISBURSEMENT);
            Assert.Equal(-1000m, cash.Amount());
            Assert.Equal(disbursement.GetProperty("id").GetGuid(), cash.GetProperty("loanEntryId").GetGuid());
            Assert.Equal(LoansApi.DefaultLoanDate, cash.GetProperty("valueDate").GetString());
            Assert.Equal(0m, await Client.GetCashBalanceAsync());
        }

        [Fact]
        public async Task ALoanLargerThanTheAvailableCash_IsRejected_AndNothingIsSaved()
        {
            await Client.PostContributionAsync(500m);
            var customerId = await Client.CreateCustomerAsync();

            var response = await Client.PostLoanAsync(customerId, 1000m);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, (await Client.GetFromJsonAsync<JsonElement>("/api/loans")).GetArrayLength());
            Assert.Equal(500m, await Client.GetCashBalanceAsync());
        }

        // THE TEST CLOCK SAYS TODAY IS 2026-09-15
        [Theory]
        [InlineData(0, 0.10, "2026-01-15", null, 25)]
        [InlineData(100.005, 0.10, "2026-01-15", null, 25)]
        [InlineData(1000, 0, "2026-01-15", null, 25)]
        [InlineData(1000, 10, "2026-01-15", null, 25)]
        [InlineData(1000, 0.10, "2026-09-16", null, 25)]
        [InlineData(1000, 0.10, "2026-01-15", 0, 25)]
        [InlineData(1000, 0.10, "2026-01-15", null, 29)]
        [InlineData(1000, 0.10, "2026-01-15", null, 0)]
        public async Task InvalidLoans_AreRejected(double principal, double interestRate, string loanDate, int? term, int paymentDay)
        {
            await Client.PostContributionAsync(2000m);
            var customerId = await Client.CreateCustomerAsync();

            var response = await Client.PostLoanAsync(customerId, (decimal)principal, (decimal)interestRate, loanDate, term, paymentDay);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ALoanForAnUnknownCustomer_ReturnsNotFound()
        {
            await Client.PostContributionAsync(1000m);

            var response = await Client.PostLoanAsync(Guid.NewGuid(), 1000m);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetLoans_ListsTheLoanWithItsCustomerAndBalance()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            var loans = await Client.GetFromJsonAsync<JsonElement>("/api/loans");

            var loan = Assert.Single(loans.EnumerateArray());
            Assert.Equal(loanId, loan.GetProperty("id").GetGuid());
            Assert.Equal("Fulanito", loan.GetProperty("customerFullname").GetString());
            Assert.Equal(1000m, loan.BalanceTotal());
        }

        [Fact]
        public async Task GetLoan_ShowsTheSuggestedPayment_OnlyWhenTheLoanHasATerm()
        {
            var withTerm = await Client.CreateLoanAsync(1000m, term: 5);
            var withoutTerm = await Client.CreateLoanAsync(1000m);

            var suggested = (await Client.GetLoanAsync(withTerm)).GetProperty("suggestedPayment");

            Assert.Equal(200m, suggested.GetProperty("principal").GetDecimal());
            Assert.Equal(0m, suggested.GetProperty("interest").GetDecimal());
            Assert.Equal(200m, suggested.GetProperty("total").GetDecimal());
            Assert.Equal(JsonValueKind.Null, (await Client.GetLoanAsync(withoutTerm)).GetProperty("suggestedPayment").ValueKind);
        }

        [Fact]
        public async Task GetLoan_ForAnUnknownLoan_ReturnsNotFound()
        {
            var response = await Client.GetAsync($"/api/loans/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
