using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("group_members")]
    public class GroupMember
    {
        [Column("group_member_id")]
        public int GroupMemberId { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public ExpenseGroup Group { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
