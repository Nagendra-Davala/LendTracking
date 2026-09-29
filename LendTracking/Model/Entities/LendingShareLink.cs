using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    /// <summary>
    /// An unguessable capability link that lets someone view a single lending record's live
    /// ledger without an account. The token maps to the record, not a snapshot, so edits show
    /// up immediately without needing to regenerate the link.
    /// </summary>
    [Table("lending_share_links")]
    public class LendingShareLink
    {
        [Column("share_link_id")]
        public int ShareLinkId { get; set; }

        [Column("lending_record_id")]
        public int LendingRecordId { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LendingRecord LendingRecord { get; set; } = null!;
    }
}
