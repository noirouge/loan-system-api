using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class UserCreateDTO
    {
        [Required]
        required public string Name { get; set; }
        [Required]
        required public string Lastname { get; set; }
        [Required]
        required public string Username { get; set; }
        [Required]
        required public string Password { get; set; }
        [Required]
        required public UserRole Role { get; set; }
    }
}
