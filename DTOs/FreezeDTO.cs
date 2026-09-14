namespace LoanSystemAPI.DTOs
{
    // AN EMPTY EndDate MEANS THE FREEZE IS STILL OPEN
    public class FreezeDTO
    {
        public Guid Id { get; set; }
        public Guid LoanId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Reason { get; set; }
        public Guid AuthorizedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
