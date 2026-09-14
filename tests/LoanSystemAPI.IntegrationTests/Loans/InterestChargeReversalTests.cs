using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    // A CHARGE MADE BY MISTAKE IS REVERSED ONLY WHILE ITS INTEREST IS STILL PENDING (D-071)
    [Collection(ApiCollection.Name)]
    public class InterestChargeReversalTests : IntegrationTest
    {
        public InterestChargeReversalTests(ApiFixture fixture) : base(fixture)
        {
        }

        private async Task<(Guid LoanId, Guid ChargeId)> CreateLoanWithChargeAsync()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            await Factory.AddInterestChargeAsync(loanId, 100m, PaymentsApi.February);
            var charge = Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.INTERESTCHARGE));
            return (loanId, charge.GetProperty("id").GetGuid());
        }

        [Fact]
        public async Task ReversingAPendingCharge_LeavesTheInterestAtZero()
        {
            var (loanId, chargeId) = await CreateLoanWithChargeAsync();

            var response = await Client.PostAsync($"/api/loans/entries/{chargeId}/reversal", null);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(0m, (await Client.GetLoanAsync(loanId)).BalanceInterest());
        }

        [Fact]
        public async Task ReversingAChargeAlreadyPartlyPaid_IsRejected()
        {
            var (loanId, chargeId) = await CreateLoanWithChargeAsync();
            await Client.PostPaymentAsync(loanId, 50m);

            var response = await Client.PostAsync($"/api/loans/entries/{chargeId}/reversal", null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(50m, (await Client.GetLoanAsync(loanId)).BalanceInterest());
        }
    }
}
