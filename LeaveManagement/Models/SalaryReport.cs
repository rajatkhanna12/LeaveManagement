namespace LeaveManagement.Models
{
    using LeaveManagement.Helpers;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Security.Cryptography;
    using System.Text;

    public class SalaryReport
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int Year { get; set; }
        public int Month { get; set; }

        // Encrypted fields
        public string BaseSalaryEncrypted { get; set; } = "";
        public string DeductionsEncrypted { get; set; } = "";
        public string BonusesEncrypted { get; set; } = "";

        // Exposed properties
        [NotMapped]
        public decimal BaseSalary
        {
            get => EncryptionHelpers.DecryptDecimal(BaseSalaryEncrypted);
            set => BaseSalaryEncrypted = EncryptionHelpers.EncryptDecimal(value);
        }

        [NotMapped]
        public decimal Deductions
        {
            get => EncryptionHelpers.DecryptDecimal(DeductionsEncrypted);
            set => DeductionsEncrypted = EncryptionHelpers.EncryptDecimal(value);
        }

        [NotMapped]
        public decimal Bonuses
        {
            get => EncryptionHelpers.DecryptDecimal(BonusesEncrypted);
            set => BonusesEncrypted = EncryptionHelpers.EncryptDecimal(value);
        }

        [NotMapped]
        public decimal FinalSalary => BaseSalary - Deductions + Bonuses;

    }

}
