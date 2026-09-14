namespace LoanSystemAPI.DTOs
{
    public class ReportMonthlyCollectionDTO
    {
        public string Month { get; set; } = "";
        public int? PaymentDay { get; set; }
        public List<ReportMonthlyCollectionLoanDTO> Loans { get; set; } = new();
        public decimal TotalInterestDue { get; set; }
        public decimal TotalPrincipalDue { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRemaining { get; set; }
    }
}
