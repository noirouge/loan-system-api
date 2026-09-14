using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.CashEntries;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using LoanSystemAPI.IntegrationTests.Loans;
using LoanSystemAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LoanSystemAPI.IntegrationTests.Reports
{
    [Collection(ApiCollection.Name)]
    public class MonthlyCollectionTests : IntegrationTest
    {
        public MonthlyCollectionTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<Guid> CreateLoanAsync(string customer, decimal principal, string loanDate, int? term, int paymentDay)
        {
            await Client.PostContributionAsync(principal, "2026-01-01");
            var customerId = await (await Client.PostAsJsonAsync("/api/customers", new { fullname = customer, code = $"C-{customer}" })).ReadIdAsync();
            var response = await Client.PostLoanAsync(customerId, principal, 0.10m, loanDate, term, paymentDay);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.ReadIdAsync();
        }

        private async Task RunPeriodAsync(int month)
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<InterestChargeJob>().RunPeriodAsync(new DateOnly(2026, month, 1));
        }

        private async Task<JsonElement> GetSheetAsync(string query)
        {
            var response = await Client.GetAsync($"/api/reports/monthly-collection?{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        private static decimal Amount(JsonElement element, string property)
        {
            return element.GetProperty(property).GetDecimal();
        }

        // ANA: 1000 AT 10% IN 5 PAYMENTS ON THE 25TH. BETO: 500 WITHOUT TERM ON THE 5TH, PAID OFF IN FEBRUARY. CARLA: 1000 WITHOUT TERM, LENT IN FEBRUARY
        private async Task<(Guid Ana, Guid Beto, Guid Carla)> CreateExampleAsync()
        {
            var ana = await CreateLoanAsync("Ana", 1000m, "2026-01-15", 5, 25);
            var beto = await CreateLoanAsync("Beto", 500m, "2026-01-20", null, 5);
            await RunPeriodAsync(2);
            await Client.PostPaymentAsync(ana, 300m, valueDate: "2026-02-25");
            await Client.PostPaymentAsync(beto, 550m, valueDate: "2026-02-05");
            var carla = await CreateLoanAsync("Carla", 1000m, "2026-02-10", null, 5);
            await RunPeriodAsync(3);
            return (ana, beto, carla);
        }

        [Fact]
        public async Task February_ShowsTheFeeThePaymentAndWhatRemains()
        {
            await CreateExampleAsync();

            var sheet = await GetSheetAsync("month=2026-02");

            var loans = sheet.GetProperty("loans").EnumerateArray().ToList();
            Assert.Equal(new[] { "Beto", "Ana" }, loans.Select(l => l.GetProperty("customerName").GetString()));
            var beto = loans[0];
            Assert.Equal("0-1", beto.GetProperty("installment").GetString());
            Assert.Equal("C-Beto", beto.GetProperty("customerCode").GetString());
            Assert.Equal(50m, Amount(beto, "interestDue"));
            Assert.Equal(500m, Amount(beto, "principalDue"));
            Assert.Equal(550m, Amount(beto, "paid"));
            Assert.Equal(0m, Amount(beto, "remaining"));
            Assert.Equal((int)LoanStatus.CLOSED, beto.GetProperty("status").GetInt32());
            var ana = loans[1];
            Assert.Equal("1-5", ana.GetProperty("installment").GetString());
            Assert.Equal(1000m, Amount(ana, "owedAtCut"));
            Assert.Equal(100m, Amount(ana, "interestDue"));
            Assert.Equal(200m, Amount(ana, "principalDue"));
            Assert.Equal(300m, Amount(ana, "totalDue"));
            Assert.Equal(100m, Amount(ana, "paidInterest"));
            Assert.Equal(200m, Amount(ana, "paidPrincipal"));
            Assert.Equal(800m, Amount(ana, "remaining"));
            Assert.Equal(850m, Amount(sheet, "totalDue"));
            Assert.Equal(850m, Amount(sheet, "totalPaid"));
            Assert.Equal(800m, Amount(sheet, "totalRemaining"));
        }

        [Fact]
        public async Task March_DropsWhoPaidOff_AndAddsTheLoanOfFebruary()
        {
            await CreateExampleAsync();

            var loans = (await GetSheetAsync("month=2026-03")).GetProperty("loans").EnumerateArray().ToList();

            Assert.Equal(new[] { "Carla", "Ana" }, loans.Select(l => l.GetProperty("customerName").GetString()));
            Assert.Equal(1100m, Amount(loans[0], "totalDue"));
            var ana = loans[1];
            Assert.Equal("2-5", ana.GetProperty("installment").GetString());
            Assert.Equal(800m, Amount(ana, "owedAtCut"));
            Assert.Equal(280m, Amount(ana, "totalDue"));
            Assert.Equal(0m, Amount(ana, "paid"));
            Assert.Equal(880m, Amount(ana, "remaining"));
        }

        [Fact]
        public async Task ThePaymentDay_FiltersTheSheet()
        {
            await CreateExampleAsync();

            var loans = (await GetSheetAsync("month=2026-03&paymentDay=25")).GetProperty("loans").EnumerateArray().ToList();

            Assert.Equal("Ana", Assert.Single(loans).GetProperty("customerName").GetString());
        }

        [Fact]
        public async Task FrozenAndWrittenOffLoans_SayIt()
        {
            var frozen = await CreateLoanAsync("Frozen", 1000m, "2026-01-15", null, 10);
            await Client.PostAsJsonAsync($"/api/loans/{frozen}/freezes", new { startDate = "2026-01-20" });
            var writtenOff = await CreateLoanAsync("WrittenOff", 1000m, "2026-01-15", null, 10);
            await Client.PostAsync($"/api/loans/{writtenOff}/write-off", null);
            await RunPeriodAsync(2);

            var loans = (await GetSheetAsync("month=2026-02")).GetProperty("loans").EnumerateArray().ToList();

            var frozenRow = Assert.Single(loans, l => l.GetProperty("loanId").GetGuid() == frozen);
            Assert.True(frozenRow.GetProperty("frozen").GetBoolean());
            Assert.Equal(0m, Amount(frozenRow, "interestDue"));
            var writtenOffRow = Assert.Single(loans, l => l.GetProperty("loanId").GetGuid() == writtenOff);
            Assert.Equal((int)LoanStatus.WRITTENOFF, writtenOffRow.GetProperty("status").GetInt32());
            Assert.False(writtenOffRow.GetProperty("frozen").GetBoolean());
        }

        [Theory]
        [InlineData("month=2026-13")]
        [InlineData("month=septiembre")]
        [InlineData("month=2026-03&paymentDay=29")]
        [InlineData("paymentDay=5")]
        public async Task InvalidParameters_AreRejected(string query)
        {
            var response = await Client.GetAsync($"/api/reports/monthly-collection?{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
