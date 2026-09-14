using LoanSystemAPI.Enums;

namespace LoanSystemAPI.DTOs
{
    public class LoanEntryDTO
    {
        public Guid Id { get; set; }
        public LoanEntryType EntryType { get; set; }
        public decimal Principal { get; set; }
        public decimal Interest { get; set; }
        public DateOnly? Period { get; set; }
        public DateOnly ValueDate { get; set; }
        public LoanEntryStatus Status { get; set; }
        public Guid? ReversesEntryId { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
