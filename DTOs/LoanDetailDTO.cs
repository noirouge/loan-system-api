namespace LoanSystemAPI.DTOs
{
    public class LoanDetailDTO : LoanDTO
    {
        // NULL WHEN THE LOAN HAS NO TERM OR NOTHING IS OWED (D-049)
        public LoanSuggestedPaymentDTO? SuggestedPayment { get; set; }
        public List<LoanEntryDTO> Entries { get; set; } = new();
    }
}
