using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class AuthLoginDTO
    {
        [Required]
        required public string Username { get; set; }
        [Required]
        required public string Password { get; set; }
    }
}
