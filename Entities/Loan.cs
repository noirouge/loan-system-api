using LoanSystemAPI.Enums;

namespace LoanSystemAPI.Entities
{
    public class Loan
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Principal {  get; set; }
        public int Term {  get; set; }
        public decimal InterestRate { get; set; }
       public DateTime LoanDate { get; set; }
        public LoanStatus LoanStatus { get; set; } = LoanStatus.ACTIVE;
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }

    }
}
