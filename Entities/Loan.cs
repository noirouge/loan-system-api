using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class Loan
    {
        [Required]
       required public Guid Id { get; set; }

        [Required]
       required public Guid CustomerId { get; set; }

        [Required]
       required public decimal Principal {  get; set; }


        public int? Term {  get; set; }

        [Required]
        required public decimal InterestRate { get; set; }

        [Required]
       required public DateTime LoanDate { get; set; }

        [Required]
        required public int PaymentDay { get; set; } = 1;


        public LoanStatus Status { get; set; } = LoanStatus.ACTIVE;

        [Required]
        required public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }

    }
}
