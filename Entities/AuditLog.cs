using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class AuditLog
    {
        [Required]
        required public Guid Id { get; set; }
        public string? EntityName { get; set; }
        public Guid? EntityId { get; set; }
        [Required]
        required public AuditAction Action { get; set; }
        public Guid? UserId { get; set; }
        public string? AttemptedUser { get; set; }
        public string? IpAddress { get; set; }
        // JSONB: ONLY THE FIELDS THAT CHANGED, WITH "old" AND "new"
        public string? Changes { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
