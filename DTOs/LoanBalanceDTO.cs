namespace LoanSystemAPI.DTOs
{
    // WHAT THE CUSTOMER OWES: SUM OF THE principal AND interest COLUMNS OF THE LOAN ENTRIES (D-050)
    public class LoanBalanceDTO
    {
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Total => Principal + Interest;
    }
}
