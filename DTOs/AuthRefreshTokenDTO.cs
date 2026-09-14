using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class AuthRefreshTokenDTO
    {
        [Required]
        required public string RefreshToken { get; set; }
    }
}
