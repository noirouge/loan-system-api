using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CashEntryDTO
    {
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateTime ValueDate { get; set; }
        public string? Note { get; set; } = "";

        public string? Counterparty { get; set; } = "";
        public Guid? CounterpartyUserId { get; set; }

    }
}
