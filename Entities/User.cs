using System.ComponentModel.DataAnnotations;

namespace LoanSystemAPI.Entities
{
    public class User
    {

        [Required]
       required public Guid Id { get; set; }
        [Required]
        required public string Name { get; set; }
        [Required]
        required public string Lastname { get; set; }
        [Required]
       required public string Username { get; set; }
        [Required]
        required public string PasswordHash { get; set; }
        
        public int role { get; set; }
        public int Status { get; set; }
        
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate {get; set;}


        }
    }
