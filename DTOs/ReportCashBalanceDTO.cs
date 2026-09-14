namespace LoanSystemAPI.DTOs
{
    // SUMS WITH THE SIGN OF THE CASH: WHAT COMES IN IS POSITIVE AND WHAT GOES OUT NEGATIVE. EACH REVERSAL IS INSIDE THE TYPE IT REVERSES
    public class ReportCashBalanceDTO
    {
        public DateOnly? Date { get; set; }
        public decimal Contributions { get; set; }
        public decimal Withdrawals { get; set; }
        public decimal Disbursements { get; set; }
        public decimal Payments { get; set; }
        public decimal Expenses { get; set; }
        public decimal Balance { get; set; }
    }
}
