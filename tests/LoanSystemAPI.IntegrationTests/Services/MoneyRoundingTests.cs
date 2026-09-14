using LoanSystemAPI.Services;
using System.Globalization;

namespace LoanSystemAPI.IntegrationTests.Services
{
    public class MoneyRoundingTests
    {
        [Theory]
        [InlineData("1.266", "1.27")]
        [InlineData("1.265", "1.26")]
        [InlineData("1.264", "1.26")]
        [InlineData("1.2659", "1.26")]
        [InlineData("104.16625", "104.17")]
        [InlineData("84.000", "84")]
        [InlineData("0.005", "0")]
        [InlineData("0.006", "0.01")]
        [InlineData("-1.266", "-1.27")]
        [InlineData("-1.265", "-1.26")]
        public void Round_LooksOnlyAtTheThirdDecimal(string value, string expected)
        {
            var result = MoneyRounding.Round(decimal.Parse(value, CultureInfo.InvariantCulture));

            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), result);
        }
    }
}
