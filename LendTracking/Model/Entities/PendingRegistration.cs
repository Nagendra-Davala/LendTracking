using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    /// <summary>
    /// Holds a signup until its email is confirmed. Nothing lands in <c>users</c> until then.
    /// </summary>
    [Table("pending_registrations")]
    public class PendingRegistration
    {
        [Column("pending_registration_id")]
        public int PendingRegistrationId { get; set; }

        [Column("email")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Column("password_hash")]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("display_name")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        // Only the hash is stored, so a database leak cannot be replayed as a live link.
        [Column("token_hash")]
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
