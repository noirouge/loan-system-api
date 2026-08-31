using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CustomerDTO:CustomerRegisterDTO
    {
        [Required]
        public Guid Id { get; set; }
       
    }
}
