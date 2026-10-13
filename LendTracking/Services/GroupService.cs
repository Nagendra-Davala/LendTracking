using LendTracking.Data;
using LendTracking.Model.Entities;
using LendTracking.Model.Groups;
using Microsoft.EntityFrameworkCore;

namespace LendTracking.Services
{
    public class GroupService
    {
        private static readonly string[] ValidSplitTypes = { "equal", "custom", "percentage" };

        private static readonly string[] ValidCategories =
        {
            "general", "food", "groceries", "transport", "rent", "utilities",
            "entertainment", "travel", "shopping", "health", "other",
        };

        private readonly LendTrackingDbContext _db;

        public GroupService(LendTrackingDbContext db)
        {
            _db = db;
        }

        private Task<bool> IsMemberAsync(int groupId, int userId, CancellationToken ct) =>
            _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId && m.IsActive, ct);

        public async Task<List<GroupResponse>> GetUserGroupsAsync(int userId, CancellationToken ct = default)
        {
            var groups = await _db.Groups
                .Where(g => g.IsActive && g.Members.Any(m => m.UserId == userId && m.IsActive))
                .Include(g => g.Members).ThenInclude(m => m.User)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync(ct);

            return groups.Select(ToGroupResponse).ToList();
        }

        public async Task<GroupResponse?> GetGroupAsync(int userId, int groupId, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            return group is null ? null : ToGroupResponse(group);
        }

        private async Task<ExpenseGroup?> FindOwnedAsync(int userId, int groupId, CancellationToken ct)
        {
            var group = await _db.Groups
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive, ct);

            if (group is null || !group.Members.Any(m => m.UserId == userId && m.IsActive))
            {
                return null;
            }

            return group;
        }

        public async Task<GroupResponse> CreateGroupAsync(int userId, CreateGroupRequest request, CancellationToken ct = default)
        {
            var group = new ExpenseGroup
            {
                GroupName = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
            };
            group.Members.Add(new GroupMember { UserId = userId, JoinedAt = DateTime.UtcNow });

            // Unresolvable usernames are silently skipped — the UI only offers existing users as
            // suggestions, so this only happens if someone's account changed between typing and submit.
            foreach (var username in request.MemberUsernames.Distinct())
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username, ct);
                if (user is not null && user.UserId != userId)
                {
                    group.Members.Add(new GroupMember { UserId = user.UserId, JoinedAt = DateTime.UtcNow });
                }
            }

            _db.Groups.Add(group);
            await _db.SaveChangesAsync(ct);

            var saved = await FindOwnedAsync(userId, group.GroupId, ct);
            return ToGroupResponse(saved!);
        }

        public async Task<bool> DeleteGroupAsync(int userId, int groupId, CancellationToken ct = default)
        {
            var group = await _db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive, ct);
            if (group is null || group.CreatedByUserId != userId) return false;

            _db.Groups.Remove(group);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<GroupResponse?> AddMemberAsync(int userId, int groupId, string username, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            if (group is null) return null;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username, ct);
            if (user is null) return null;

            if (!group.Members.Any(m => m.UserId == user.UserId && m.IsActive))
            {
                _db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = user.UserId, JoinedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync(ct);
            }

            var refreshed = await FindOwnedAsync(userId, groupId, ct);
            return refreshed is null ? null : ToGroupResponse(refreshed);
        }

        public async Task<List<GroupExpenseResponse>?> GetGroupExpensesAsync(int userId, int groupId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return null;

            var expenses = await _db.Expenses
                .Where(e => e.GroupId == groupId)
                .Include(e => e.PaidBy)
                .Include(e => e.Shares).ThenInclude(s => s.User)
                .Include(e => e.Comments)
                .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt)
                .ToListAsync(ct);

            return expenses.Select(ToExpenseResponse).ToList();
        }

        public async Task<GroupExpenseResponse?> CreateExpenseAsync(
            int userId, int groupId, CreateExpenseRequest request, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            if (group is null) return null;

            if (!ValidSplitTypes.Contains(request.SplitType))
            {
                throw new ArgumentException("Split type must be one of: equal, custom, percentage.");
            }

            if (!int.TryParse(request.PaidBy, out var paidByUserId) ||
                !group.Members.Any(m => m.UserId == paidByUserId && m.IsActive))
            {
                throw new ArgumentException("The payer must be a member of this group.");
            }

            var participantIds = new List<int>();
            foreach (var p in request.Participants)
            {
                if (!int.TryParse(p.UserId, out var pid) || !group.Members.Any(m => m.UserId == pid && m.IsActive))
                {
                    throw new ArgumentException("Every participant must be a member of this group.");
                }
                participantIds.Add(pid);
            }

            if (request.SplitType == "custom")
            {
                var total = request.Participants.Sum(p => p.Share);
                if (Math.Abs(total - request.Amount) > 0.01m)
                {
                    throw new ArgumentException(
                        $"Shares must add up to {request.Amount:0.00} — currently {total:0.00}.");
                }
            }
            else if (request.SplitType == "percentage")
            {
                var total = request.Participants.Sum(p => p.Share);
                if (Math.Abs(total - 100m) > 0.01m)
                {
                    throw new ArgumentException($"Percentages must add up to 100% — currently {total:0.0}%.");
                }
            }

            if (!DateOnly.TryParse(request.Date, out var expenseDate))
            {
                throw new ArgumentException("Enter a valid date.");
            }

            var category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim();
            if (!ValidCategories.Contains(category))
            {
                throw new ArgumentException("Select a valid category.");
            }

            var expense = new GroupExpense
            {
                GroupId = groupId,
                PaidByUserId = paidByUserId,
                Description = request.Description.Trim(),
                Amount = request.Amount,
                Category = category,
                ExpenseDate = expenseDate,
                SplitType = request.SplitType,
                CreatedAt = DateTime.UtcNow,
            };

            foreach (var p in request.Participants)
            {
                var pid = int.Parse(p.UserId);
                var (shareAmount, sharePercent) = request.SplitType switch
                {
                    "equal" => (request.Amount / request.Participants.Count, (decimal?)null),
                    "percentage" => (request.Amount * p.Share / 100m, (decimal?)p.Share),
                    _ => (p.Share, (decimal?)null), // custom
                };

                expense.Shares.Add(new ExpenseShare
                {
                    UserId = pid,
                    ShareAmount = Math.Round(shareAmount, 2),
                    SharePercent = sharePercent,
                });
            }

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync(ct);

            var saved = await _db.Expenses
                .Include(e => e.PaidBy)
                .Include(e => e.Shares).ThenInclude(s => s.User)
                .Include(e => e.Comments)
                .FirstAsync(e => e.ExpenseId == expense.ExpenseId, ct);

            return ToExpenseResponse(saved);
        }

        public async Task<List<ExpenseCommentResponse>?> GetCommentsAsync(
            int userId, int groupId, int expenseId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return null;

            var expenseExists = await _db.Expenses.AnyAsync(e => e.ExpenseId == expenseId && e.GroupId == groupId, ct);
            if (!expenseExists) return null;

            var comments = await _db.ExpenseComments
                .Where(c => c.ExpenseId == expenseId)
                .Include(c => c.User)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            return comments.Select(ToCommentResponse).ToList();
        }

        public async Task<ExpenseCommentResponse?> AddCommentAsync(
            int userId, int groupId, int expenseId, string text, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return null;

            var expenseExists = await _db.Expenses.AnyAsync(e => e.ExpenseId == expenseId && e.GroupId == groupId, ct);
            if (!expenseExists) return null;

            var comment = new ExpenseComment
            {
                ExpenseId = expenseId,
                UserId = userId,
                CommentText = text.Trim(),
                CreatedAt = DateTime.UtcNow,
            };

            _db.ExpenseComments.Add(comment);
            await _db.SaveChangesAsync(ct);

            var saved = await _db.ExpenseComments
                .Include(c => c.User)
                .FirstAsync(c => c.CommentId == comment.CommentId, ct);

            return ToCommentResponse(saved);
        }

        public async Task<bool> DeleteCommentAsync(
            int userId, int groupId, int expenseId, int commentId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return false;

            var comment = await _db.ExpenseComments
                .FirstOrDefaultAsync(c => c.CommentId == commentId && c.ExpenseId == expenseId && c.UserId == userId, ct);
            if (comment is null) return false;

            _db.ExpenseComments.Remove(comment);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<GroupsOverviewResponse> GetOverviewAsync(int userId, CancellationToken ct = default)
        {
            var groups = await _db.Groups
                .Where(g => g.IsActive && g.Members.Any(m => m.UserId == userId && m.IsActive))
                .Include(g => g.Members).ThenInclude(m => m.User)
                .ToListAsync(ct);

            var response = new GroupsOverviewResponse();

            foreach (var group in groups)
            {
                var balances = await ComputeBalancesAsync(group.GroupId, group.Members, ct);
                var myBalance = balances.TryGetValue(userId, out var b) ? b.Balance : 0m;

                if (Math.Abs(myBalance) >= 0.01m)
                {
                    response.Groups.Add(new GroupOverviewItemResponse
                    {
                        GroupId = group.GroupId.ToString(),
                        GroupName = group.GroupName,
                        Balance = myBalance,
                    });

                    if (myBalance > 0) response.TotalYouAreOwed += myBalance;
                    else response.TotalYouOwe += -myBalance;
                }
            }

            response.NetBalance = response.TotalYouAreOwed - response.TotalYouOwe;
            return response;
        }

        /// <summary>Aggregates each group's settle-up suggestions into one net balance per
        /// counterparty, across every group shared with the current user.</summary>
        public async Task<PeopleOverviewResponse> GetPeopleOverviewAsync(int userId, CancellationToken ct = default)
        {
            var groups = await _db.Groups
                .Where(g => g.IsActive && g.Members.Any(m => m.UserId == userId && m.IsActive))
                .Include(g => g.Members).ThenInclude(m => m.User)
                .ToListAsync(ct);

            var netByPerson = new Dictionary<int, PersonBalanceResponse>();

            foreach (var group in groups)
            {
                var balances = await ComputeBalancesAsync(group.GroupId, group.Members, ct);
                var debts = SimplifyDebts(balances.Values.ToList());

                foreach (var debt in debts)
                {
                    if (debt.From == userId.ToString())
                    {
                        var toId = int.Parse(debt.To);
                        if (!netByPerson.TryGetValue(toId, out var entry))
                        {
                            entry = new PersonBalanceResponse { UserId = debt.To, Username = debt.ToName };
                            netByPerson[toId] = entry;
                        }
                        entry.Balance -= debt.Amount;
                    }
                    else if (debt.To == userId.ToString())
                    {
                        var fromId = int.Parse(debt.From);
                        if (!netByPerson.TryGetValue(fromId, out var entry))
                        {
                            entry = new PersonBalanceResponse { UserId = debt.From, Username = debt.FromName };
                            netByPerson[fromId] = entry;
                        }
                        entry.Balance += debt.Amount;
                    }
                }
            }

            var people = netByPerson.Values
                .Where(p => Math.Abs(p.Balance) >= 0.01m)
                .OrderByDescending(p => p.Balance)
                .ToList();

            foreach (var p in people)
            {
                p.Balance = Math.Round(p.Balance, 2);
            }

            return new PeopleOverviewResponse { People = people };
        }

        public async Task<List<SettlementResponse>?> GetGroupSettlementsAsync(int userId, int groupId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return null;

            var settlements = await _db.Settlements
                .Where(s => s.GroupId == groupId)
                .Include(s => s.PaidBy)
                .Include(s => s.PaidTo)
                .OrderByDescending(s => s.SettledDate).ThenByDescending(s => s.CreatedAt)
                .ToListAsync(ct);

            return settlements.Select(ToSettlementResponse).ToList();
        }

        public async Task<SettlementResponse?> CreateSettlementAsync(
            int userId, int groupId, CreateSettlementRequest request, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            if (group is null) return null;

            if (!int.TryParse(request.FromUserId, out var fromId) || !int.TryParse(request.ToUserId, out var toId))
            {
                throw new ArgumentException("Invalid member reference.");
            }

            if (fromId == toId)
            {
                throw new ArgumentException("A member can't settle up with themselves.");
            }

            if (!group.Members.Any(m => m.UserId == fromId && m.IsActive) ||
                !group.Members.Any(m => m.UserId == toId && m.IsActive))
            {
                throw new ArgumentException("Both people must be members of this group.");
            }

            var settledDate = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(request.SettledDate) && !DateOnly.TryParse(request.SettledDate, out settledDate))
            {
                throw new ArgumentException("Enter a valid date.");
            }

            var settlement = new Settlement
            {
                GroupId = groupId,
                PaidByUserId = fromId,
                PaidToUserId = toId,
                Amount = request.Amount,
                SettledDate = settledDate,
                CreatedAt = DateTime.UtcNow,
            };

            _db.Settlements.Add(settlement);
            await _db.SaveChangesAsync(ct);

            var saved = await _db.Settlements
                .Include(s => s.PaidBy)
                .Include(s => s.PaidTo)
                .FirstAsync(s => s.SettlementId == settlement.SettlementId, ct);

            return ToSettlementResponse(saved);
        }

        public async Task<bool> DeleteSettlementAsync(int userId, int groupId, int settlementId, CancellationToken ct = default)
        {
            if (!await IsMemberAsync(groupId, userId, ct)) return false;

            var settlement = await _db.Settlements
                .FirstOrDefaultAsync(s => s.SettlementId == settlementId && s.GroupId == groupId, ct);
            if (settlement is null) return false;

            _db.Settlements.Remove(settlement);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<List<BalanceResponse>?> GetGroupBalancesAsync(int userId, int groupId, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            if (group is null) return null;

            var balances = await ComputeBalancesAsync(groupId, group.Members, ct);
            return balances.Values.ToList();
        }

        public async Task<List<SimplifiedDebtResponse>?> GetSimplifiedDebtsAsync(int userId, int groupId, CancellationToken ct = default)
        {
            var group = await FindOwnedAsync(userId, groupId, ct);
            if (group is null) return null;

            var balances = await ComputeBalancesAsync(groupId, group.Members, ct);
            return SimplifyDebts(balances.Values.ToList());
        }

        /// <summary>Who paid gets credited the full amount; every participant's recorded share
        /// debits them. Settlements then move balance from payer to receiver.</summary>
        private async Task<Dictionary<int, BalanceResponse>> ComputeBalancesAsync(
            int groupId, ICollection<GroupMember> members, CancellationToken ct)
        {
            var balances = members
                .Where(m => m.IsActive)
                .ToDictionary(
                    m => m.UserId,
                    m => new BalanceResponse { UserId = m.UserId.ToString(), Username = m.User.UserName, Balance = 0m });

            var expenses = await _db.Expenses
                .Where(e => e.GroupId == groupId)
                .Include(e => e.Shares)
                .ToListAsync(ct);

            foreach (var expense in expenses)
            {
                if (balances.TryGetValue(expense.PaidByUserId, out var payer))
                {
                    payer.Balance += expense.Amount;
                }

                foreach (var share in expense.Shares)
                {
                    if (balances.TryGetValue(share.UserId, out var member))
                    {
                        member.Balance -= share.ShareAmount;
                    }
                }
            }

            var settlements = await _db.Settlements.Where(s => s.GroupId == groupId).ToListAsync(ct);
            foreach (var settlement in settlements)
            {
                if (balances.TryGetValue(settlement.PaidByUserId, out var from))
                {
                    from.Balance += settlement.Amount;
                }
                if (balances.TryGetValue(settlement.PaidToUserId, out var to))
                {
                    to.Balance -= settlement.Amount;
                }
            }

            foreach (var b in balances.Values)
            {
                b.Balance = Math.Round(b.Balance, 2);
            }

            return balances;
        }

        /// <summary>Greedy match: largest creditor against largest debtor, repeat. Minimizes the
        /// number of payments needed to clear every balance in the group.</summary>
        private static List<SimplifiedDebtResponse> SimplifyDebts(List<BalanceResponse> balances)
        {
            var creditors = balances.Where(b => b.Balance > 0.01m).OrderByDescending(b => b.Balance).ToList();
            var debtors = balances.Where(b => b.Balance < -0.01m).OrderBy(b => b.Balance).ToList();

            var debts = new List<SimplifiedDebtResponse>();
            var i = 0;
            var j = 0;

            while (i < creditors.Count && j < debtors.Count)
            {
                var creditor = creditors[i];
                var debtor = debtors[j];
                var amount = Math.Min(creditor.Balance, -debtor.Balance);

                debts.Add(new SimplifiedDebtResponse
                {
                    From = debtor.UserId,
                    FromName = debtor.Username,
                    To = creditor.UserId,
                    ToName = creditor.Username,
                    Amount = Math.Round(amount, 2),
                });

                creditor.Balance -= amount;
                debtor.Balance += amount;

                if (Math.Abs(creditor.Balance) < 0.01m) i++;
                if (Math.Abs(debtor.Balance) < 0.01m) j++;
            }

            return debts;
        }

        private static GroupResponse ToGroupResponse(ExpenseGroup g) => new()
        {
            Id = g.GroupId.ToString(),
            Name = g.GroupName,
            Description = g.Description,
            CreatorId = g.CreatedByUserId.ToString(),
            Members = g.Members
                .Where(m => m.IsActive)
                .Select(m => new GroupMemberResponse
                {
                    UserId = m.UserId.ToString(),
                    Username = m.User.UserName,
                    JoinedAt = m.JoinedAt.ToString("o"),
                })
                .ToList(),
            CreatedAt = g.CreatedAt.ToString("o"),
            UpdatedAt = g.CreatedAt.ToString("o"),
        };

        private static GroupExpenseResponse ToExpenseResponse(GroupExpense e) => new()
        {
            Id = e.ExpenseId.ToString(),
            GroupId = e.GroupId.ToString(),
            Description = e.Description,
            Amount = e.Amount,
            PaidBy = e.PaidByUserId.ToString(),
            PaidByName = e.PaidBy.UserName,
            Participants = e.Shares.Select(s => new ExpenseParticipantResponse
            {
                UserId = s.UserId.ToString(),
                Username = s.User.UserName,
                Share = e.SplitType == "percentage" ? (s.SharePercent ?? 0) : e.SplitType == "custom" ? s.ShareAmount : 0,
            }).ToList(),
            SplitType = e.SplitType,
            Date = e.ExpenseDate.ToString("yyyy-MM-dd"),
            Category = e.Category,
            CommentCount = e.Comments.Count,
            CreatedAt = e.CreatedAt.ToString("o"),
            UpdatedAt = e.CreatedAt.ToString("o"),
        };

        private static ExpenseCommentResponse ToCommentResponse(ExpenseComment c) => new()
        {
            Id = c.CommentId.ToString(),
            ExpenseId = c.ExpenseId.ToString(),
            UserId = c.UserId.ToString(),
            Username = c.User.UserName,
            Comment = c.CommentText,
            CreatedAt = c.CreatedAt.ToString("o"),
        };

        private static SettlementResponse ToSettlementResponse(Settlement s) => new()
        {
            Id = s.SettlementId.ToString(),
            GroupId = s.GroupId.ToString(),
            FromUserId = s.PaidByUserId.ToString(),
            FromUsername = s.PaidBy.UserName,
            ToUserId = s.PaidToUserId.ToString(),
            ToUsername = s.PaidTo.UserName,
            Amount = s.Amount,
            Date = s.CreatedAt.ToString("o"),
            IsSettled = true,
            SettledAt = s.SettledDate.ToString("yyyy-MM-dd"),
        };
    }
}
