using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class LoanForgivenessDTO
    {
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateOnly ValueDate { get; set; }
        public string? Note { get; set; }
    }
}
