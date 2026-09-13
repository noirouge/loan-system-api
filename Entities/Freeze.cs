using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class Freeze
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public Guid LoanId { get; set; }
        [Required]
        required public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Reason { get; set; }
        [Required]
        required public Guid AuthorizedBy { get; set; }
        public FreezeStatus Status { get; set; } = FreezeStatus.ACTIVE;

        [Required]
        required public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
