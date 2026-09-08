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
        public bool IsCleared { get; set; }
        public DateOnly? ClearedDate { get; set; }
        public string? Notes { get; set; }

        /// <summary>Accrued to today, or frozen at the cleared date.</summary>
        public decimal InterestToDate { get; set; }
        public decimal TotalToDate { get; set; }
        public int DaysElapsed { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
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
}
