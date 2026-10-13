using System.Security.Claims;
using LendTracking.Model.Groups;
using LendTracking.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LendTracking.Controllers
{
    [Route("api/groups")]
    [ApiController]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly GroupService _groupService;

        public GroupsController(GroupService groupService)
        {
            _groupService = groupService;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();
            return Ok(await _groupService.GetUserGroupsAsync(userId, ct));
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();
            return Ok(await _groupService.GetOverviewAsync(userId, ct));
        }

        [HttpGet("overview/people")]
        public async Task<IActionResult> GetPeopleOverview(CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();
            return Ok(await _groupService.GetPeopleOverviewAsync(userId, ct));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var group = await _groupService.GetGroupAsync(userId, id, ct);
            return group is null ? NotFound() : Ok(group);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGroupRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();
            return Ok(await _groupService.CreateGroupAsync(userId, request, ct));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var deleted = await _groupService.DeleteGroupAsync(userId, id, ct);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPost("{id:int}/members")]
        public async Task<IActionResult> AddMember(int id, [FromBody] AddGroupMemberRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var group = await _groupService.AddMemberAsync(userId, id, request.Username.Trim(), ct);
            return group is null ? NotFound(new { message = "User not found or you're not a member of this group." }) : Ok(group);
        }

        [HttpGet("{id:int}/expenses")]
        public async Task<IActionResult> GetExpenses(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var expenses = await _groupService.GetGroupExpensesAsync(userId, id, ct);
            return expenses is null ? NotFound() : Ok(expenses);
        }

        [HttpPost("{id:int}/expenses")]
        public async Task<IActionResult> CreateExpense(int id, [FromBody] CreateExpenseRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            try
            {
                var expense = await _groupService.CreateExpenseAsync(userId, id, request, ct);
                return expense is null ? NotFound() : Ok(expense);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:int}/expenses/{expenseId:int}/comments")]
        public async Task<IActionResult> GetComments(int id, int expenseId, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var comments = await _groupService.GetCommentsAsync(userId, id, expenseId, ct);
            return comments is null ? NotFound() : Ok(comments);
        }

        [HttpPost("{id:int}/expenses/{expenseId:int}/comments")]
        public async Task<IActionResult> AddComment(int id, int expenseId, [FromBody] CreateCommentRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var comment = await _groupService.AddCommentAsync(userId, id, expenseId, request.Comment, ct);
            return comment is null ? NotFound() : Ok(comment);
        }

        [HttpDelete("{id:int}/expenses/{expenseId:int}/comments/{commentId:int}")]
        public async Task<IActionResult> DeleteComment(int id, int expenseId, int commentId, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var deleted = await _groupService.DeleteCommentAsync(userId, id, expenseId, commentId, ct);
            return deleted ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/settlements")]
        public async Task<IActionResult> GetSettlements(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var settlements = await _groupService.GetGroupSettlementsAsync(userId, id, ct);
            return settlements is null ? NotFound() : Ok(settlements);
        }

        [HttpPost("{id:int}/settlements")]
        public async Task<IActionResult> CreateSettlement(int id, [FromBody] CreateSettlementRequest request, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            try
            {
                var settlement = await _groupService.CreateSettlementAsync(userId, id, request, ct);
                return settlement is null ? NotFound() : Ok(settlement);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:int}/settlements/{settlementId:int}")]
        public async Task<IActionResult> DeleteSettlement(int id, int settlementId, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var deleted = await _groupService.DeleteSettlementAsync(userId, id, settlementId, ct);
            return deleted ? NoContent() : NotFound();
        }

        [HttpGet("{id:int}/balances")]
        public async Task<IActionResult> GetBalances(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var balances = await _groupService.GetGroupBalancesAsync(userId, id, ct);
            return balances is null ? NotFound() : Ok(balances);
        }

        [HttpGet("{id:int}/debts")]
        public async Task<IActionResult> GetDebts(int id, CancellationToken ct)
        {
            if (CurrentUserId is not { } userId) return Unauthorized();

            var debts = await _groupService.GetSimplifiedDebtsAsync(userId, id, ct);
            return debts is null ? NotFound() : Ok(debts);
        }
    }
}
