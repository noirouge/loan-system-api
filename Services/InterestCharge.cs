using LoanSystemAPI.DTOs;

namespace LoanSystemAPI.Services
{
    // THE MONTHLY INTEREST IS OVER THE WHOLE DEBT: RATE × (PENDING PRINCIPAL + PENDING INTEREST),
    // SO THE INTEREST LEFT UNPAID ALSO GENERATES INTEREST. ROUNDED WITH THE RULE OF D-043
    public static class InterestCharge
    {
        public static decimal Calculate(decimal interestRate, LoanBalanceDTO balance)
        {
            if (balance.Total <= 0)
                return 0;

            return MoneyRounding.Round(interestRate * balance.Total);
        }
    }
}
