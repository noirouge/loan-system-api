using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Reports
{
    // THE CANONICAL EXAMPLE, WITH A PAYMENT AND AN EXPENSE THAT WERE REVERSED: EACH REVERSED PAIR MUST ADD UP TO 0 (D-055)
    [Collection(ApiCollection.Name)]
    public class ReportsTests : IntegrationTest
    {
        public ReportsTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<JsonElement> GetReportAsync(string url)
        {
            var response = await Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        private static decimal Amount(JsonElement report, string property)
        {
            return report.GetProperty(property).GetDecimal();
        }

        // THE CASH COMES IN ON JANUARY 1ST, BEFORE THE LOAN OF JANUARY 15TH
        private async Task<Guid> CreateLoanAsync(decimal principal)
        {
            await Client.PostContributionAsync(principal, "2026-01-01");
            var customerId = await Client.CreateCustomerAsync();
            var response = await Client.PostLoanAsync(customerId, principal);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        private async Task<Guid> PayAsync(Guid loanId, decimal amount, string valueDate)
        {
            var response = await Client.PostPaymentAsync(loanId, amount, valueDate: valueDate);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        // 1000 AT 10%: CHARGES OF 100, 80 AND 84; PAYMENTS OF 300, 40 AND 324; A PAYMENT OF 20 AND AN EXPENSE OF 15 THAT WERE REVERSED;
        // AND AN EXPENSE OF 30. IT ENDS WITH 600 OF PRINCIPAL, 0 OF INTEREST, 264 OF INTEREST COLLECTED AND 634 IN CASH
        private async Task CreateCanonicalExampleAsync()
        {
            var loanId = await CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, new DateOnly(2026, 2, 1));
            await PayAsync(loanId, 300m, "2026-02-10");
            await Factory.AddInterestChargeAsync(loanId, 80m, new DateOnly(2026, 3, 1));
            var reversedPaymentId = await PayAsync(loanId, 20m, "2026-03-05");
            Assert.Equal(HttpStatusCode.Created, (await Client.PostAsync($"/api/loans/entries/{reversedPaymentId}/reversal", null)).StatusCode);
            await PayAsync(loanId, 40m, "2026-03-10");
            await Factory.AddInterestChargeAsync(loanId, 84m, new DateOnly(2026, 4, 1));
            await PayAsync(loanId, 324m, "2026-04-10");

            Assert.Equal(HttpStatusCode.Created, (await Client.PostExpenseAsync(30m, counterparty: "Papeleria", valueDate: "2026-03-15")).StatusCode);
            var reversedExpenseId = await (await Client.PostExpenseAsync(15m, counterparty: "Registrado por error", valueDate: "2026-03-16")).ReadIdAsync();
            Assert.Equal(HttpStatusCode.Created, (await Client.PostReversalAsync(reversedExpenseId)).StatusCode);
        }

        [Fact]
        public async Task AccruedInterest_AddsTheInterestCharges()
        {
            await CreateCanonicalExampleAsync();

            Assert.Equal(264m, Amount(await GetReportAsync("/api/reports/accrued-interest"), "amount"));
            Assert.Equal(80m, Amount(await GetReportAsync("/api/reports/accrued-interest?from=2026-03-01&to=2026-03-31"), "amount"));
        }

        [Fact]
        public async Task CollectedInterest_AddsTheInterestOfThePayments_WithoutTheReversedPayment()
        {
            await CreateCanonicalExampleAsync();

            Assert.Equal(264m, Amount(await GetReportAsync("/api/reports/collected-interest"), "amount"));
            Assert.Equal(40m, Amount(await GetReportAsync("/api/reports/collected-interest?from=2026-03-01&to=2026-03-31"), "amount"));
            Assert.Equal(124m, Amount(await GetReportAsync("/api/reports/collected-interest?from=2026-04-01"), "amount"));
        }

        [Fact]
        public async Task Pending_IsWhatTheCustomersStillOwe_AtEachDate()
        {
            await CreateCanonicalExampleAsync();

            var today = await GetReportAsync("/api/reports/pending");
            Assert.Equal(600m, Amount(today, "principal"));
            Assert.Equal(0m, Amount(today, "interest"));
            Assert.Equal(600m, Amount(today, "total"));
            Assert.Equal(0m, Amount(today, "writtenOffPrincipal"));

            var endOfMarch = await GetReportAsync("/api/reports/pending?date=2026-03-31");
            Assert.Equal(800m, Amount(endOfMarch, "principal"));
            Assert.Equal(40m, Amount(endOfMarch, "interest"));
        }

        [Fact]
        public async Task CashBalance_ShowsEachTypeWithItsReversalsInside()
        {
            await CreateCanonicalExampleAsync();

            var cash = await GetReportAsync("/api/reports/cash-balance");
            Assert.Equal(1000m, Amount(cash, "contributions"));
            Assert.Equal(0m, Amount(cash, "withdrawals"));
            Assert.Equal(-1000m, Amount(cash, "disbursements"));
            Assert.Equal(664m, Amount(cash, "payments"));
            Assert.Equal(-30m, Amount(cash, "expenses"));
            Assert.Equal(634m, Amount(cash, "balance"));

            var endOfFebruary = await GetReportAsync("/api/reports/cash-balance?date=2026-02-28");
            Assert.Equal(300m, Amount(endOfFebruary, "payments"));
            Assert.Equal(300m, Amount(endOfFebruary, "balance"));
        }

        [Fact]
        public async Task Profit_IsTheCollectedInterestMinusTheExpenses()
        {
            await CreateCanonicalExampleAsync();

            var profit = await GetReportAsync("/api/reports/profit");
            Assert.Equal(264m, Amount(profit, "collectedInterest"));
            Assert.Equal(30m, Amount(profit, "expenses"));
            Assert.Equal(234m, Amount(profit, "profit"));

            var march = await GetReportAsync("/api/reports/profit?from=2026-03-01&to=2026-03-31");
            Assert.Equal(40m, Amount(march, "collectedInterest"));
            Assert.Equal(30m, Amount(march, "expenses"));
            Assert.Equal(10m, Amount(march, "profit"));
        }

        [Fact]
        public async Task WrittenOffLoans_GoApart_AndDeletedLoansAreNotDebt()
        {
            var writtenOffId = await CreateLoanAsync(200m);
            Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/loans/{writtenOffId}/write-off", null)).StatusCode);
            var deletedId = await CreateLoanAsync(300m);
            Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/loans/{deletedId}")).StatusCode);

            var pending = await GetReportAsync("/api/reports/pending");

            Assert.Equal(0m, Amount(pending, "principal"));
            Assert.Equal(0m, Amount(pending, "interest"));
            Assert.Equal(200m, Amount(pending, "writtenOffPrincipal"));
            Assert.Equal(0m, Amount(pending, "writtenOffInterest"));
        }

        [Theory]
        [InlineData("accrued-interest")]
        [InlineData("collected-interest")]
        [InlineData("profit")]
        public async Task ARangeThatEndsBeforeItStarts_IsRejected(string report)
        {
            var response = await Client.GetAsync($"/api/reports/{report}?from=2026-04-01&to=2026-03-01");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
