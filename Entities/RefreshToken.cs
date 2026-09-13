using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class RefreshToken
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public Guid UserId { get; set; }
        [Required]
        required public string TokenHash { get; set; }
        [Required]
        required public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public Guid? ReplacedBy { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
