using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("expense_shares")]
    public class ExpenseShare
    {
        [Column("expense_share_id")]
        public int ExpenseShareId { get; set; }

        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("share_amount")]
        public decimal ShareAmount { get; set; }

        [Column("share_percent")]
        public decimal? SharePercent { get; set; }

        public GroupExpense Expense { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
