using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;

namespace LoanSystemAPI.Services
{
    // ONLY INFORMATIVE: THE CUSTOMER CAN PAY ANY AMOUNT (D-049)
    public static class SuggestedPayment
    {
        public static LoanSuggestedPaymentDTO? Calculate(Loan loan, LoanBalanceDTO balance)
        {
            if (loan.Term == null || loan.Term <= 0 || loan.Status != LoanStatus.ACTIVE || balance.Total <= 0)
                return null;

            // ORIGINAL PRINCIPAL DIVIDED BY THE TERM, OR WHAT IS LEFT IF IT IS LESS
            var principalPerMonth = MoneyRounding.Round(loan.Principal / loan.Term.Value);

            return new LoanSuggestedPaymentDTO
            {
                Principal = Math.Min(principalPerMonth, Math.Max(balance.Principal, 0)),
                Interest = Math.Max(balance.Interest, 0),
            };
        }
    }
}
