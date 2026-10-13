using System.ComponentModel.DataAnnotations;

namespace LendTracking.Model.Groups
{
    public class CreateGroupRequest
    {
        [Required(ErrorMessage = "Please give the group a name.")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<string> MemberUsernames { get; set; } = new();
    }

    public class AddGroupMemberRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;
    }

    public class ExpenseParticipantRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Dollar amount for "custom", percentage for "percentage", ignored for "equal".</summary>
        public decimal Share { get; set; }
    }

    public class CreateExpenseRequest
    {
        [Required(ErrorMessage = "Please describe the expense.")]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "Enter an amount greater than zero.")]
        public decimal Amount { get; set; }

        [Required]
        public string PaidBy { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select at least one participant.")]
        [MinLength(1, ErrorMessage = "Select at least one participant.")]
        public List<ExpenseParticipantRequest> Participants { get; set; } = new();

        [Required]
        public string SplitType { get; set; } = "equal";

        [Required]
        public string Date { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }
    }

    public class CreateCommentRequest
    {
        [Required(ErrorMessage = "Comment can't be empty.")]
        [MaxLength(1000)]
        public string Comment { get; set; } = string.Empty;
    }

    public class CreateSettlementRequest
    {
        [Required]
        public string FromUserId { get; set; } = string.Empty;

        [Required]
        public string ToUserId { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "Enter an amount greater than zero.")]
        public decimal Amount { get; set; }

        public string? SettledDate { get; set; }
    }

    public class GroupMemberResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string JoinedAt { get; set; } = string.Empty;
    }

    public class GroupResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatorId { get; set; } = string.Empty;
        public List<GroupMemberResponse> Members { get; set; } = new();
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class ExpenseParticipantResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public decimal Share { get; set; }
    }

    public class GroupExpenseResponse
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaidBy { get; set; } = string.Empty;
        public string PaidByName { get; set; } = string.Empty;
        public List<ExpenseParticipantResponse> Participants { get; set; } = new();
        public string SplitType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int CommentCount { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    public class ExpenseCommentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string ExpenseId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class GroupOverviewItemResponse
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class GroupsOverviewResponse
    {
        public decimal TotalYouOwe { get; set; }
        public decimal TotalYouAreOwed { get; set; }
        public decimal NetBalance { get; set; }
        public List<GroupOverviewItemResponse> Groups { get; set; } = new();
    }

    public class PersonBalanceResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>Positive = they owe you. Negative = you owe them.</summary>
        public decimal Balance { get; set; }
    }

    public class PeopleOverviewResponse
    {
        public List<PersonBalanceResponse> People { get; set; } = new();
    }

    public class SettlementResponse
    {
        public string Id { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string FromUserId { get; set; } = string.Empty;
        public string FromUsername { get; set; } = string.Empty;
        public string ToUserId { get; set; } = string.Empty;
        public string ToUsername { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
        public bool IsSettled { get; set; } = true;
        public string? SettledAt { get; set; }
    }

    public class BalanceResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    public class SimplifiedDebtResponse
    {
        public string From { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
