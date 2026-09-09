using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CashEntryContributionDTO
    {
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateTime ValueDate { get; set; }
        public string? Note { get; set; } = "";
        public Guid CounterpartyUserId { get; set; }

    }
}
