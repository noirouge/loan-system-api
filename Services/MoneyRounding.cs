namespace LoanSystemAPI.Services
{
    // ROUNDS TO 2 DECIMALS LOOKING ONLY AT THE THIRD DECIMAL: 6 TO 9 ROUNDS UP, 0 TO 5 STAYS (D-043).
    // Math.Round IS NOT USED BECAUSE NONE OF ITS MODES FOLLOWS THIS RULE
    public static class MoneyRounding
    {
        public static decimal Round(decimal value)
        {
            var sign = value < 0 ? -1 : 1;
            var absolute = Math.Abs(value);
            var cents = Math.Truncate(absolute * 100);
            var thirdDecimal = Math.Truncate(absolute * 1000) % 10;

            if (thirdDecimal > 5)
                cents += 1;

            return sign * cents / 100;
        }
    }
}
