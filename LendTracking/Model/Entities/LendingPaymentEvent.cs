using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LendTracking.Model.Entities
{
    /// <summary>
    /// A raw fact: a payment received and/or a renegotiated proposed return date, logged against
    /// a lending record. Interest/principal breakdowns are derived on read, not stored here.
    /// </summary>
    [Table("lending_payment_events")]
    public class LendingPaymentEvent
    {
        [Column("payment_event_id")]
        public int PaymentEventId { get; set; }

        [Column("lending_record_id")]
        public int LendingRecordId { get; set; }

        [Column("event_date")]
        public DateOnly EventDate { get; set; }

        [Column("amount_paid")]
        public decimal? AmountPaid { get; set; }

        [Column("new_proposed_date")]
        public DateOnly? NewProposedDate { get; set; }

        /// <summary>
        /// If true, any part of AmountPaid beyond the interest owed at this event is NOT applied
        /// to principal — it's banked as credit instead. The credit never drains on its own; it
        /// only ever changes at an explicit ApplyHeldCredit event or at final settlement.
        /// </summary>
        [Column("hold_excess_as_credit")]
        public bool HoldExcessAsCredit { get; set; }

        /// <summary>
        /// If true, this event explicitly applies whatever held credit is available toward the
        /// interest owed as of this event's date — a deliberate, dated, visible action rather than
        /// a silent background deduction. Can stand alone or accompany a payment/date change.
        /// </summary>
        [Column("apply_held_credit")]
        public bool ApplyHeldCredit { get; set; }

        /// <summary>
        /// If true, whatever interest is owed as of this event (after any ApplyHeldCredit) is
        /// folded straight into the principal instead of being carried forward as unpaid interest
        /// — a deliberate "restart the clock" action. Mutually exclusive with AmountPaid.
        /// </summary>
        [Column("capitalize_interest")]
        public bool CapitalizeInterest { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LendingRecord LendingRecord { get; set; } = null!;
    }
}
