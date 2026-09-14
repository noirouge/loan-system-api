using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class FreezeCloseDTO
    {
        [Required]
        required public DateOnly EndDate { get; set; }
    }
}
