using LeaveManagement.Helpers;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace LeaveManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public DateTime JoiningDate { get; set; }

        [NotMapped]
        public decimal BaseSalary
        {
            get => SalaryEncrypted == null ? 0 : EncryptionHelpers.DecryptDecimal(SalaryEncrypted);
            set => SalaryEncrypted = EncryptionHelpers.EncryptDecimal(value);
        }

        public string SalaryEncrypted { get; set; } = "";

        public string Role { get; set; }
        public bool IsActive { get; set; } = true;

    }

}
