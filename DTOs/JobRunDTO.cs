using LoanSystemAPI.Enums;

namespace LoanSystemAPI.DTOs
{
    public class JobRunDTO
    {
        public Guid Id { get; set; }
        public string JobName { get; set; } = "";
        public DateOnly? Period { get; set; }
        public JobRunStatus Status { get; set; }
        public int Processed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
