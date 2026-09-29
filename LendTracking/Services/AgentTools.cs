using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using LendTracking.Model;
using Microsoft.SemanticKernel;

namespace LendTracking.Services
{
    /// <summary>
    /// Tools exposed to the agent for one authenticated user. Read tools call the services
    /// directly; anything that writes data only drafts a proposal (see ProposeAddLending /
    /// ProposeSetCleared) — AgentService turns that into a confirm card, the actual write
    /// happens only after the user confirms via AgentController's confirm endpoint.
    /// </summary>
    public class AgentTools(int userId, LendingService lendingService, SimpleInterestService simpleInterestService)
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        [KernelFunction, Description("Get a summary of the current user's lending position: total receivable (money lent out) and payable (money borrowed), including accrued interest, and net position.")]
        public async Task<string> GetOutstandingSummary()
        {
            var result = await lendingService.GetAllAsync(userId, null, null);
            return JsonSerializer.Serialize(result.Summary, JsonOptions);
        }

        [KernelFunction, Description("List the current user's lending records, optionally filtered. direction is 'Lent' or 'Borrowed'. status is 'active' or 'cleared'.")]
        public async Task<string> ListLendingRecords(string? direction = null, string? status = null)
        {
            var result = await lendingService.GetAllAsync(userId, direction, status);
            var compact = result.Records.Select(r => new
            {
                r.LendingRecordId,
                r.CounterpartyName,
                r.Direction,
                r.Principal,
                r.AnnualRatePercent,
                r.GivenDate,
                r.IsCleared,
                r.InterestToDate,
                r.TotalToDate
            });
            return JsonSerializer.Serialize(compact, JsonOptions);
        }

        [KernelFunction, Description("Calculate simple interest for a given principal, annual interest rate percentage, start date and end date (dates as yyyy-MM-dd). Does not read or write any records.")]
        public string CalculateSimpleInterest(decimal principal, decimal annualRatePercent, string startDate, string endDate)
        {
            var model = new SimpleInterestModel
            {
                Principal = principal,
                Rate = annualRatePercent,
                StartDate = DateOnly.Parse(startDate, CultureInfo.InvariantCulture),
                EndDate = DateOnly.Parse(endDate, CultureInfo.InvariantCulture)
            };
            var response = simpleInterestService.CalculateSimpleInterest(model);
            return JsonSerializer.Serialize(response, JsonOptions);
        }

        [KernelFunction, Description("Draft a new lending record for the user to review and confirm. This does NOT save anything yet — it only prepares a proposal the user must explicitly confirm in the UI. direction must be 'Lent' (user gave money) or 'Borrowed' (user received money). givenDate as yyyy-MM-dd.")]
        public string ProposeAddLending(string counterpartyName, string direction, decimal principal, decimal annualRatePercent, string givenDate, string? notes = null)
        {
            if (direction is not ("Lent" or "Borrowed"))
            {
                throw new ArgumentException("direction must be 'Lent' or 'Borrowed'.");
            }

            var payload = new
            {
                counterpartyName = counterpartyName.Trim(),
                direction,
                principal,
                annualRatePercent,
                givenDate = DateOnly.Parse(givenDate, CultureInfo.InvariantCulture),
                notes
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        [KernelFunction, Description("Draft marking an existing lending record (by its id) as cleared or reopened, for the user to confirm. Does NOT save anything yet. Resolve lendingRecordId yourself by calling ListLendingRecords and matching on counterparty/amount/date/status — never ask the user to state the numeric id.")]
        public string ProposeSetCleared(int lendingRecordId, bool isCleared)
        {
            var payload = new { lendingRecordId, isCleared };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        [KernelFunction, Description("Draft permanently deleting an existing lending record (by its id), for the user to confirm. Does NOT delete anything yet. Resolve lendingRecordId yourself by calling ListLendingRecords and matching on counterparty/amount/date/status — never ask the user to state the numeric id. This is the correct tool when the user says 'delete' or 'remove', as opposed to ProposeSetCleared which just marks a record settled without removing it.")]
        public string ProposeDeleteLending(int lendingRecordId)
        {
            var payload = new { lendingRecordId };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }
    }
}
