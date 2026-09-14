namespace LoanSystemAPI.DTOs
{
    // REAL PROFIT = COLLECTED INTEREST - EXPENSES. BOTH AMOUNTS ARE POSITIVE
    public class ReportProfitDTO
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public decimal CollectedInterest { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit => CollectedInterest - Expenses;
    }
}
