using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("settlements")]
    public class Settlement
    {
        [Column("settlement_id")]
        public int SettlementId { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("paid_by_user_id")]
        public int PaidByUserId { get; set; }

        [Column("paid_to_user_id")]
        public int PaidToUserId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("settled_date")]
        public DateOnly SettledDate { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ExpenseGroup Group { get; set; } = null!;
        public AppUser PaidBy { get; set; } = null!;
        public AppUser PaidTo { get; set; } = null!;
    }
}
