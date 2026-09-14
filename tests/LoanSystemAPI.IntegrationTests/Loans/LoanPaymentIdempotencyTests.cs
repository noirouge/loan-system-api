using LoanSystemAPI.Enums;
using LoanSystemAPI.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace LoanSystemAPI.IntegrationTests.Loans
{
    [Collection(ApiCollection.Name)]
    public class LoanPaymentIdempotencyTests : IntegrationTest
    {
        public LoanPaymentIdempotencyTests(ApiFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public async Task TheSameKeyAndTheSamePayment_CreateOnlyOnePayment()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var key = Guid.NewGuid();

            var first = await Client.PostPaymentAsync(loanId, 300m, key);
            var retry = await Client.PostPaymentAsync(loanId, 300m, key);

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal(await first.ReadIdAsync(), await retry.ReadIdAsync());
            Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
            Assert.Equal(700m, (await Client.GetLoanAsync(loanId)).BalanceTotal());
        }

        [Fact]
        public async Task TheSameKeyWithADifferentPayment_Returns422()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var key = Guid.NewGuid();
            await Client.PostPaymentAsync(loanId, 300m, key);

            var response = await Client.PostPaymentAsync(loanId, 200m, key);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(700m, (await Client.GetLoanAsync(loanId)).BalanceTotal());
        }

        [Fact]
        public async Task RetryingAPaymentThatSettledTheDebt_ReturnsTheSamePayment()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var key = Guid.NewGuid();
            var first = await Client.PostPaymentAsync(loanId, 1000m, key);

            var retry = await Client.PostPaymentAsync(loanId, 1000m, key);

            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.Equal(await first.ReadIdAsync(), await retry.ReadIdAsync());
        }

        [Fact]
        public async Task TheSameKeyOnAnotherLoan_Returns422()
        {
            var loanA = await Client.CreateLoanAsync(1000m);
            var loanB = await Client.CreateLoanAsync(1000m);
            var key = Guid.NewGuid();
            await Client.PostPaymentAsync(loanA, 300m, key);

            var response = await Client.PostPaymentAsync(loanB, 300m, key);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Empty(await Client.GetEntriesAsync(loanB, LoanEntryType.PAYMENT));
        }

        [Fact]
        public async Task APaymentWithoutKey_IsRejected()
        {
            var loanId = await Client.CreateLoanAsync(1000m);

            var response = await Client.PostAsJsonAsync($"/api/loans/{loanId}/payments", new { amount = 100m, valueDate = PaymentsApi.DefaultPaymentDate });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task TheSameKeySentTwiceAtTheSameTime_CreatesOnlyOnePayment()
        {
            var loanId = await Client.CreateLoanAsync(1000m);
            var key = Guid.NewGuid();

            var responses = await Task.WhenAll(Client.PostPaymentAsync(loanId, 300m, key), Client.PostPaymentAsync(loanId, 300m, key));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
            Assert.Single(await Client.GetEntriesAsync(loanId, LoanEntryType.PAYMENT));
            Assert.Equal(700m, (await Client.GetLoanAsync(loanId)).BalanceTotal());
        }
    }
}
