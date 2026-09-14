using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class LoanPaymentDTO
    {
        [Required]
        required public decimal Amount { get; set; }
        [Required]
        required public DateOnly ValueDate { get; set; }
        // THE CLIENT GENERATES IT ONCE PER PAYMENT AND SENDS THE SAME ONE WHEN IT RETRIES
        [Required]
        required public Guid IdempotencyKey { get; set; }
        public string? Note { get; set; }
    }
}
