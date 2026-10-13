using System.ComponentModel.DataAnnotations;

namespace LendTracking.Model.Lending
{
    public class LendingRecordRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Please enter the other person's name.")]
        [StringLength(100)]
        public string CounterpartyName { get; set; } = string.Empty;

        public int? CounterpartyUserId { get; set; }

        [Required]
        public string Direction { get; set; } = "Lent";

        [Range(0.01, 999999999999999.99, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Principal { get; set; }

        [Range(0, 10000, ErrorMessage = "Interest rate cannot be negative.")]
        public decimal AnnualRatePercent { get; set; }

        [Required(ErrorMessage = "Please pick a date.")]
        public DateOnly GivenDate { get; set; }

        /// <summary>The repayment date the counterparty proposed when the money was given. Optional.</summary>
        public DateOnly? ProposedReturnDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext _)
        {
            if (Direction is not ("Lent" or "Borrowed"))
            {
                yield return new ValidationResult("Direction must be either Lent or Borrowed.", [nameof(Direction)]);
            }

            if (GivenDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult("The date cannot be in the future.", [nameof(GivenDate)]);
            }

            if (ProposedReturnDate is { } proposed && proposed < GivenDate)
            {
                yield return new ValidationResult("The proposed date can't be before the given date.", [nameof(ProposedReturnDate)]);
            }
        }
    }

    public class SetClearedRequest
    {
        public bool IsCleared { get; set; }

        /// <summary>Defaults to today when marking as cleared.</summary>
        public DateOnly? ClearedDate { get; set; }
    }

    public class LendingRecordResponse
    {
        public int LendingRecordId { get; set; }
        public string CounterpartyName { get; set; } = string.Empty;
        public int? CounterpartyUserId { get; set; }
        public string Direction { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal AnnualRatePercent { get; set; }
        public DateOnly GivenDate { get; set; }

        /// <summary>The currently-agreed proposed date — may have been renegotiated via a payment event.</summary>
        public DateOnly? ProposedReturnDate { get; set; }
        public bool IsCleared { get; set; }
        public DateOnly? ClearedDate { get; set; }
        public string? Notes { get; set; }

        /// <summary>True once any payment event has been logged. Direction can't change after this; editing Amount/Rate/Given date still can, but recalculates the whole history.</summary>
        public bool HasEvents { get; set; }

        /// <summary>The earliest logged event's date, if any — Given date can never be moved later than this.</summary>
        public DateOnly? EarliestEventDate { get; set; }

        /// <summary>What's actually left after any partial payments (Principal minus principal paid off so far).</summary>
        public decimal OutstandingPrincipal { get; set; }

        /// <summary>Date of the most recent event that actually logged an amount paid — not affected by proposed-date-only changes. Null if no payment has ever been logged.</summary>
        public DateOnly? LastPaidDate { get; set; }

        /// <summary>The date the current Outstanding/Interest/Total figures are calculated from — the most recent logged event of any kind, or GivenDate if none yet. Advances on every event, including capitalizing interest or applying credit.</summary>
        public DateOnly InterestFromDate { get; set; }

        /// <summary>Ledger-based: accrued on the outstanding balance to today, or frozen at the cleared date.</summary>
        public decimal InterestToDate { get; set; }
        public decimal TotalToDate { get; set; }

        /// <summary>Projected interest if settled exactly on ProposedReturnDate, given payments so far. Null if no proposed date.</summary>
        public decimal? ProjectedInterestByProposedDate { get; set; }

        /// <summary>Plain reference figure — original principal over the full given-to-today/cleared span, ignoring any payments made. Same idea as ProjectedInterestByProposedDate, but as of today.</summary>
        public decimal GrossInterestToDate { get; set; }

        /// <summary>Sum of every payment logged against this record.</summary>
        public decimal AmountPaidToDate { get; set; }

        /// <summary>Portion of AmountPaidToDate that went to interest rather than principal.</summary>
        public decimal InterestPaidToDate { get; set; }

        /// <summary>Credit banked from a payment marked "don't reduce principal", not yet consumed by interest or written off. See LendingPaymentEvent.HoldExcessAsCredit.</summary>
        public decimal HeldCredit { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AddPaymentEventRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Please pick a date.")]
        public DateOnly EventDate { get; set; }

        [Range(0.01, 999999999999999.99, ErrorMessage = "Amount must be greater than zero.")]
        public decimal? AmountPaid { get; set; }

        public DateOnly? NewProposedDate { get; set; }

        /// <summary>Only meaningful alongside AmountPaid. See LendingPaymentEvent.HoldExcessAsCredit.</summary>
        public bool HoldExcessAsCredit { get; set; }

        /// <summary>Can stand alone or accompany AmountPaid/NewProposedDate. See LendingPaymentEvent.ApplyHeldCredit.</summary>
        public bool ApplyHeldCredit { get; set; }

        /// <summary>Mutually exclusive with AmountPaid. See LendingPaymentEvent.CapitalizeInterest.</summary>
        public bool CapitalizeInterest { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext _)
        {
            if (AmountPaid is null && NewProposedDate is null && !ApplyHeldCredit && !CapitalizeInterest)
            {
                yield return new ValidationResult(
                    "Enter an amount paid, a new proposed date, or apply/capitalize credit.", [nameof(AmountPaid)]);
            }

            if (CapitalizeInterest && AmountPaid is not null)
            {
                yield return new ValidationResult(
                    "Capitalizing interest and logging a payment on the same entry doesn't make sense — log them separately.",
                    [nameof(CapitalizeInterest)]);
            }

            if (EventDate > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                yield return new ValidationResult("The date cannot be in the future.", [nameof(EventDate)]);
            }
        }
    }

    public class LedgerEntryResponse
    {
        public int PaymentEventId { get; set; }
        public DateOnly EventDate { get; set; }
        public decimal? AmountPaid { get; set; }

        /// <summary>Total interest owed as of this event (this period's accrual plus anything carried from before), regardless of how much was actually paid.</summary>
        public decimal InterestAccrued { get; set; }
        public decimal InterestApplied { get; set; }
        public decimal PrincipalApplied { get; set; }
        public decimal PrincipalBalanceAfter { get; set; }
        public DateOnly? PreviousProposedDate { get; set; }
        public DateOnly? NewProposedDate { get; set; }
        public string? Notes { get; set; }

        /// <summary>Leftover from this payment that was held as credit instead of reducing principal.</summary>
        public decimal CreditAdded { get; set; }

        /// <summary>How much held credit this event explicitly applied toward interest.</summary>
        public decimal CreditConsumed { get; set; }

        /// <summary>Interest folded straight into principal at this event, instead of being carried forward unpaid.</summary>
        public decimal InterestCapitalized { get; set; }
    }

    public class LendingLedgerResponse
    {
        public int LendingRecordId { get; set; }
        public decimal OriginalPrincipal { get; set; }
        public decimal OutstandingPrincipal { get; set; }
        public DateOnly InterestFromDate { get; set; }
        public decimal InterestToDate { get; set; }

        /// <summary>Unpaid interest that already existed going into the InterestFromDate checkpoint — not newly accrued since then.</summary>
        public decimal CarriedUnpaidInterest { get; set; }

        /// <summary>Interest accrued fresh since InterestFromDate, on top of CarriedUnpaidInterest. Together they add up to InterestToDate.</summary>
        public decimal NewInterestSinceFrom { get; set; }
        public decimal TotalToDate { get; set; }
        public DateOnly? CurrentProposedDate { get; set; }

        /// <summary>Held credit not yet consumed by interest. Written off against Total only once the record is settled.</summary>
        public decimal HeldCredit { get; set; }
        public List<LedgerEntryResponse> Entries { get; set; } = [];
    }

    public class LendingSummaryResponse
    {
        public int TotalRecords { get; set; }
        public int ActiveCount { get; set; }

        public decimal ReceivablePrincipal { get; set; }
        public decimal ReceivableInterest { get; set; }
        public decimal ReceivableTotal { get; set; }

        public decimal PayablePrincipal { get; set; }
        public decimal PayableInterest { get; set; }
        public decimal PayableTotal { get; set; }

        /// <summary>Positive means you are owed overall.</summary>
        public decimal NetPosition { get; set; }
    }

    public class LendingListResponse
    {
        public List<LendingRecordResponse> Records { get; set; } = [];
        public LendingSummaryResponse Summary { get; set; } = new();
    }

    public class CreateShareLinkRequest
    {
        /// <summary>Optional. Null means the link never expires on its own (still revocable anytime).</summary>
        public DateOnly? ExpiresOn { get; set; }
    }

    public class ShareLinkResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>What an unauthenticated viewer sees through a share link — read-only, no ownership/user IDs.</summary>
    public class PublicShareResponse
    {
        public string OwnerName { get; set; } = string.Empty;
        public string CounterpartyName { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal AnnualRatePercent { get; set; }
        public DateOnly GivenDate { get; set; }
        public DateOnly? ProposedReturnDate { get; set; }
        public bool IsCleared { get; set; }
        public DateOnly? ClearedDate { get; set; }
        public decimal OutstandingPrincipal { get; set; }
        public DateOnly? LastPaidDate { get; set; }
        public DateOnly InterestFromDate { get; set; }
        public decimal InterestToDate { get; set; }
        public decimal CarriedUnpaidInterest { get; set; }
        public decimal NewInterestSinceFrom { get; set; }
        public decimal TotalToDate { get; set; }
        public decimal HeldCredit { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<LedgerEntryResponse> Entries { get; set; } = [];
    }
}
