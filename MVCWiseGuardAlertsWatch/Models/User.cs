using System.ComponentModel.DataAnnotations;

namespace MVCWiseGuardAlertsWatch.Models
{
    /// <summary>Registered account (Base44 User entity); standalone cookie auth.</summary>
    public class User
    {
        public int Id { get; set; }
        [Required, MaxLength(120)]
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        [MaxLength(120)]
        public string? FullName { get; set; }
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
    }
}
