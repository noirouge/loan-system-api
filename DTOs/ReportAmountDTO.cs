namespace LoanSystemAPI.DTOs
{
    // AN AMOUNT BETWEEN TWO value_date, BOTH OPTIONAL AND INCLUSIVE
    public class ReportAmountDTO
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public decimal Amount { get; set; }
    }
}
