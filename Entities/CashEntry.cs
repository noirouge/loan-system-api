using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class CashEntry
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public CashEntryType EntryType { get; set; }

        [Required]
        required public decimal amount { get; set; }

        [Required]
        required public DateOnly ValueDate { get; set; }
       public Guid? LoanEntryId { get; set; }
       public Guid? ReversesEntryId { get; set; }
        public string? Note { get; set; } = "";
        public CashEntryStatus status { get; set; }
        public Guid? CounterpartyUserId { get; set; }
        public string? Counterparty { get; set; } = "";

        [Required]
        required public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;


    }
}
