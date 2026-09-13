using LoanSystemAPI.Enums;
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

        public UserRole Role { get; set; } = UserRole.WORKER;
        public UserStatus Status { get; set; } = UserStatus.ACTIVE;
        
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedDate {get; set;}


        }
    }
