using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("groups")]
    public class ExpenseGroup
    {
        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("group_name")]
        [MaxLength(100)]
        public string GroupName { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(500)]
        public string? Description { get; set; }

        [Column("created_by_user_id")]
        public int CreatedByUserId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public AppUser CreatedBy { get; set; } = null!;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public ICollection<GroupExpense> Expenses { get; set; } = new List<GroupExpense>();
        public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
    }
}
