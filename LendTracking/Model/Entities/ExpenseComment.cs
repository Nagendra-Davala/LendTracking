using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("expense_comments")]
    public class ExpenseComment
    {
        [Column("comment_id")]
        public int CommentId { get; set; }

        [Column("expense_id")]
        public int ExpenseId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("comment_text")]
        [MaxLength(1000)]
        public string CommentText { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public GroupExpense Expense { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
