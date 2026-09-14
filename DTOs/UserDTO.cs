using LoanSystemAPI.Enums;

namespace LoanSystemAPI.DTOs
{
    // WITHOUT THE PASSWORD HASH NOR THE AUDIT FIELDS
    public class UserDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Lastname { get; set; } = "";
        public string Username { get; set; } = "";
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
    }
}
