using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.DTOs
{
    public class CustomerDTO
    {
        [Required]
        required public string Fullname { get; set; }
        public string? Code { get; set; }
        public string? Note { get; set; }
        public string? Phone { get; set; }
    }
}
