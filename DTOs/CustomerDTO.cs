using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CustomerDTO:CustomerRegisterDTO
    {
        public Guid Id { get; set; }
        public CustomerStatus Status { get; set; }
        required public Guid CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
