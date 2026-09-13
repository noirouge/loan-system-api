using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class LoanEntry
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public Guid LoanId { get; set; }
        public Guid? IdempotencyKey { get; set; }
        [Required]
        required public LoanEntryType EntryType { get; set; }
        [Required]
        required public decimal Principal { get; set; }
        [Required]
        required public decimal Interest { get; set; }
        public string? Note { get; set; }
        public DateOnly? Period { get; set; }
        public Guid? ReversesEntryId { get; set; }
        [Required]
        required public DateOnly ValueDate { get; set; }
        public LoanEntryStatus Status { get; set; } = LoanEntryStatus.APPLIED;

        [Required]
        required public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
