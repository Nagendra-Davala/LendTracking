using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("users")]
    public class AppUser
    {
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("user_name")]
        [MaxLength(50)]
        public string UserName { get; set; } = string.Empty;

        [Column("display_name")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [Column("email")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Column("password_hash")]
        [MaxLength(255)]
        public string? PasswordHash { get; set; }

        [Column("google_subject_id")]
        [MaxLength(64)]
        public string? GoogleSubjectId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public ICollection<LendingRecord> LendingRecords { get; set; } = new List<LendingRecord>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    }
}
