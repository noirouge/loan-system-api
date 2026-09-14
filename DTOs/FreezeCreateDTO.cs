using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class FreezeCreateDTO
    {
        [Required]
        required public DateOnly StartDate { get; set; }
        public string? Reason { get; set; }
    }
}
