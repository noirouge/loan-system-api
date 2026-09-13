using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class JobRun
    {
        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public string JobName { get; set; }
        public DateOnly? Period { get; set; }
        public JobRunStatus Status { get; set; } = JobRunStatus.RUNNING;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
