namespace LoanSystemAPI.DTOs
{
    public class LoanSuggestedPaymentDTO
    {
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Total => Principal + Interest;
    }
}
