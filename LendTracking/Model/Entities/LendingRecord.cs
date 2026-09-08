using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    [Table("lending_records")]
    public class LendingRecord
    {
        [Column("lending_record_id")]
        public int LendingRecordId { get; set; }

        [Column("owner_user_id")]
        public int OwnerUserId { get; set; }

        [Column("counterparty_name")]
        [MaxLength(100)]
        public string CounterpartyName { get; set; } = string.Empty;

        /// <summary>Set when the other person also has an account; the name is kept either way.</summary>
        [Column("counterparty_user_id")]
        public int? CounterpartyUserId { get; set; }

        [Column("direction")]
        [MaxLength(10)]
        public string Direction { get; set; } = LendingDirection.Lent;

        [Column("principal")]
        public decimal Principal { get; set; }

        [Column("annual_rate_percent")]
        public decimal AnnualRatePercent { get; set; }

        [Column("given_date")]
        public DateOnly GivenDate { get; set; }

        [Column("is_cleared")]
        public bool IsCleared { get; set; }

        /// <summary>Interest stops accruing here once settled.</summary>
        [Column("cleared_date")]
        public DateOnly? ClearedDate { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public AppUser Owner { get; set; } = null!;
        public AppUser? CounterpartyUser { get; set; }
    }

    public static class LendingDirection
    {
        public const string Lent = "Lent";
        public const string Borrowed = "Borrowed";

        public static bool IsValid(string value) => value is Lent or Borrowed;
    }
}
