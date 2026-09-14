using LoanSystemAPI.Enums;

namespace LoanSystemAPI.DTOs
{
    // ONE ROW OF THE COLLECTION SHEET: WHAT THE LOAN HAD TO PAY THAT MONTH, WHAT IT PAID AND WHAT IT STILL OWES
    public class ReportMonthlyCollectionLoanDTO
    {
        public Guid LoanId { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? CustomerCode { get; set; }
        public DateOnly LoanDate { get; set; }
        public int PaymentDay { get; set; }
        // CHARGES UNTIL THIS MONTH OVER THE TERM (2-5); WITHOUT TERM IT IS ONE SINGLE PAYMENT (0-1)
        public string Installment { get; set; } = "";
        public decimal OriginalPrincipal { get; set; }
        public decimal InterestRate { get; set; }
        public decimal OwedAtCut { get; set; }
        public decimal InterestDue { get; set; }
        public decimal PrincipalDue { get; set; }
        public decimal TotalDue { get; set; }
        public decimal Paid { get; set; }
        public decimal PaidInterest { get; set; }
        public decimal PaidPrincipal { get; set; }
        public decimal Remaining { get; set; }
        // THE CURRENT STATUS OF THE LOAN; Frozen IS FOR THIS MONTH
        public LoanStatus Status { get; set; }
        public bool Frozen { get; set; }
    }
}
