using LoanSystemAPI.Enums;

namespace LoanSystemAPI.DTOs
{
    public class LoanDTO
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerFullname { get; set; } = "";
        // ORIGINAL PRINCIPAL. WHAT IS STILL OWED IS IN Balance
        public decimal Principal { get; set; }
        public int? Term { get; set; }
        public decimal InterestRate { get; set; }
        public DateOnly LoanDate { get; set; }
        public int PaymentDay { get; set; }
        public LoanStatus Status { get; set; }
        public LoanBalanceDTO Balance { get; set; } = new();
    }
}
