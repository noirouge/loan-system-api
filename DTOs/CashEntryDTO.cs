using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CashEntryDTO
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public CashEntryType EntryType { get; set; }
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateTime ValueDate { get; set; }
        public string? Note { get; set; } = "";
        public CashEntryStatus Status { get; set; }
        public Guid? CounterpartyUserId { get; set; }
        public string? Counterparty { get; set; } = "";
        public Guid? LoanEntryId { get; set; }
        public Guid? ReversesEntryId { get; set; }
        public DateTime CreatedDate { get; set; }

    }
}
