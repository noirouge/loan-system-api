using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    // THE USERNAME DOES NOT CHANGE. THE PASSWORD ONLY CHANGES IF IT IS SENT
    public class UserUpdateDTO
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public string Name { get; set; }
        [Required]
        required public string Lastname { get; set; }
        [Required]
        required public UserRole Role { get; set; }
        [Required]
        required public UserStatus Status { get; set; }
        public string? Password { get; set; }
    }
}
