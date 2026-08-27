using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoanSystemAPI.Entities
{
    public class Customer
    {

        [Required]
        required public Guid Id { get; set; }
        [Required]
        required public string Fullname { get; set; }

        public string? Code { get; set; }
        public string? Note { get; set; }
        public string? Phone { get; set; }

        public CustomerStatus Status { get; set; } = CustomerStatus.ACTIVE;

        [Required]
        required public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }

        //[ForeignKey(nameof(CreatedBy))]
        //public User? CreatedUser { get; set; }

    }
}
