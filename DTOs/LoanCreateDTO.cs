using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class LoanCreateDTO
    {
        [Required]
        required public Guid CustomerId { get; set; }
        [Required]
        required public decimal Principal { get; set; }
        // MONTHS, ONLY INFORMATIVE
        public int? Term { get; set; }
        // FRACTION: 0.10 IS 10% (D-041)
        [Required]
        required public decimal InterestRate { get; set; }
        [Required]
        required public DateOnly LoanDate { get; set; }
        [Required]
        required public int PaymentDay { get; set; }
    }
}
