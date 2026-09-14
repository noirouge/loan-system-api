namespace LoanSystemAPI.DTOs
{
    // WHAT THE CUSTOMERS STILL OWE. THE WRITTEN OFF LOANS GO APART: NOBODY EXPECTS TO COLLECT THEM (D-067)
    public class ReportPendingDTO
    {
        public DateOnly? Date { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public decimal Total => Principal + Interest;
        public decimal WrittenOffPrincipal { get; set; }
        public decimal WrittenOffInterest { get; set; }
    }
}
