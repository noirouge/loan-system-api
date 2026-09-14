using LoanSystemAPI.DTOs;
using LoanSystemAPI.Services;
using System.Globalization;

namespace LoanSystemAPI.IntegrationTests.Services
{
    public class InterestChargeTests
    {
        [Theory]
        [InlineData("0.10", "1000", "0", "100")]
        [InlineData("0.10", "800", "40", "84")]
        [InlineData("0.10", "100", "10", "11")]
        [InlineData("0.0333", "1000.50", "0", "33.32")]
        [InlineData("0.05", "1025.30", "0", "51.26")]
        public void Calculate_ChargesTheRateOverPrincipalPlusPendingInterest(string rate, string principal, string interest, string expected)
        {
            var balance = new LoanBalanceDTO { Principal = Parse(principal), Interest = Parse(interest) };

            var result = InterestCharge.Calculate(Parse(rate), balance);

            Assert.Equal(Parse(expected), result);
        }

        [Fact]
        public void Calculate_WithNothingOwed_ChargesZero()
        {
            var result = InterestCharge.Calculate(0.10m, new LoanBalanceDTO { Principal = 0m, Interest = 0m });

            Assert.Equal(0m, result);
        }

        private static decimal Parse(string value)
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
