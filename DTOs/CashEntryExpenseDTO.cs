using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CashEntryExpenseDTO
    {
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateOnly ValueDate { get; set; }
        public string? Note { get; set; } = "";
        public Guid? CounterpartyUserId { get; set; }
        [MaxLength(100)]
        public string? Counterparty { get; set; } = "";

    }
}
