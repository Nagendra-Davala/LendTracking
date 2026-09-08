using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("expenses")]
    public class GroupExpense
    {
        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Column("group_id")]
        public int GroupId { get; set; }

        [Column("paid_by_user_id")]
        public int PaidByUserId { get; set; }

        [Column("description")]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("category")]
        [MaxLength(50)]
        public string? Category { get; set; }

        [Column("expense_date")]
        public DateOnly ExpenseDate { get; set; }

        [Column("split_type")]
        [MaxLength(20)]
        public string SplitType { get; set; } = "equal"; // equal | custom | percentage

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ExpenseGroup Group { get; set; } = null!;
        public AppUser PaidBy { get; set; } = null!;
        public ICollection<ExpenseShare> Shares { get; set; } = new List<ExpenseShare>();
    }
}
