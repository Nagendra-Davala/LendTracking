using System.Security.Cryptography;
using LendTracking.Data;
using LendTracking.Model.Entities;
using LendTracking.Model.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LendTracking.Services
{
    public class LendingService
    {
        private readonly LendTrackingDbContext _db;

        public LendingService(LendTrackingDbContext db)
        {
            _db = db;
        }

        public async Task<LendingListResponse> GetAllAsync(int userId, string? direction, string? status, CancellationToken ct = default)
        {
            var query = _db.LendingRecords
                .Include(r => r.PaymentEvents)
                .Where(r => r.OwnerUserId == userId);

            if (LendingDirection.IsValid(direction ?? string.Empty))
            {
                query = query.Where(r => r.Direction == direction);
            }

            if (status == "active") query = query.Where(r => !r.IsCleared);
            else if (status == "cleared") query = query.Where(r => r.IsCleared);

            var records = await query
                .OrderByDescending(r => r.GivenDate)
                .ThenByDescending(r => r.LendingRecordId)
                .ToListAsync(ct);

            var mapped = records.Select(ToResponse).ToList();

            return new LendingListResponse { Records = mapped, Summary = Summarise(mapped) };
        }

        public async Task<LendingRecordResponse?> GetByIdAsync(int userId, int id, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            return record is null ? null : ToResponse(record);
        }

        public async Task<LendingRecordResponse> CreateAsync(int userId, LendingRecordRequest request, CancellationToken ct = default)
        {
            var record = new LendingRecord
            {
                OwnerUserId = userId,
                CounterpartyName = request.CounterpartyName.Trim(),
                CounterpartyUserId = request.CounterpartyUserId,
                Direction = request.Direction,
                Principal = request.Principal,
                AnnualRatePercent = request.AnnualRatePercent,
                GivenDate = request.GivenDate,
                ProposedReturnDate = request.ProposedReturnDate,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                IsCleared = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.LendingRecords.Add(record);
            await _db.SaveChangesAsync(ct);

            return ToResponse(record);
        }

        public async Task<LendingRecordResponse?> UpdateAsync(int userId, int id, LendingRecordRequest request, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return null;

            var hasEvents = record.PaymentEvents.Count > 0;

            // Flipping Lent/Borrowed after real activity has been logged doesn't correspond to any
            // real correction — every existing event's meaning would invert with it. If it was
            // entered backwards from day one, delete and recreate instead.
            if (hasEvents && request.Direction != record.Direction)
            {
                throw new ArgumentException(
                    "Direction can't be changed once payments or other activity have been logged on this record.");
            }

            // Amount/Rate/Given date can still be corrected after activity exists (typos happen) —
            // the frontend warns that this recalculates the whole history. But the given date can
            // never move past an event that already happened under it, or that event becomes invalid.
            if (hasEvents)
            {
                var earliestEventDate = record.PaymentEvents.Min(e => e.EventDate);
                if (request.GivenDate > earliestEventDate)
                {
                    throw new ArgumentException(
                        $"The given date can't be moved later than the earliest logged event ({earliestEventDate:d MMM yyyy}).");
                }
            }

            record.CounterpartyName = request.CounterpartyName.Trim();
            record.CounterpartyUserId = request.CounterpartyUserId;
            record.Direction = request.Direction;
            record.Principal = request.Principal;
            record.AnnualRatePercent = request.AnnualRatePercent;
            record.GivenDate = request.GivenDate;
            record.ProposedReturnDate = request.ProposedReturnDate;
            record.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            record.UpdatedAt = DateTime.UtcNow;

            // Editing the start date must not leave a cleared date behind it.
            if (record.ClearedDate is { } cleared && cleared < record.GivenDate)
            {
                record.ClearedDate = record.GivenDate;
            }

            await _db.SaveChangesAsync(ct);
            return ToResponse(record);
        }

        public async Task<LendingRecordResponse?> SetClearedAsync(int userId, int id, SetClearedRequest request, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return null;

            if (request.IsCleared)
            {
                var date = request.ClearedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                record.ClearedDate = date < record.GivenDate ? record.GivenDate : date;
                record.IsCleared = true;
            }
            else
            {
                record.IsCleared = false;
                record.ClearedDate = null;
            }

            record.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return ToResponse(record);
        }

        public async Task<bool> DeleteAsync(int userId, int id, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return false;

            _db.LendingRecords.Remove(record);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<LendingLedgerResponse?> GetLedgerAsync(int userId, int id, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            return record is null ? null : ToLedgerResponse(record);
        }

        public async Task<LendingLedgerResponse?> AddPaymentEventAsync(int userId, int id, AddPaymentEventRequest request, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return null;

            if (request.EventDate < record.GivenDate)
            {
                throw new ArgumentException("The event date can't be before the record's given date.");
            }

            if (record.ClearedDate is { } clearedDate && request.EventDate > clearedDate)
            {
                throw new ArgumentException(
                    $"This record was settled on {clearedDate:d MMM yyyy} — the event date can't be after that.");
            }

            _db.LendingPaymentEvents.Add(new LendingPaymentEvent
            {
                LendingRecordId = record.LendingRecordId,
                EventDate = request.EventDate,
                AmountPaid = request.AmountPaid,
                NewProposedDate = request.NewProposedDate,
                HoldExcessAsCredit = request.HoldExcessAsCredit,
                ApplyHeldCredit = request.ApplyHeldCredit,
                CapitalizeInterest = request.CapitalizeInterest,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            });
            record.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var refreshed = await FindOwnedAsync(userId, id, ct);
            return refreshed is null ? null : ToLedgerResponse(refreshed);
        }

        public async Task<bool> DeleteEventAsync(int userId, int lendingRecordId, int eventId, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, lendingRecordId, ct);
            if (record is null) return false;

            var evt = record.PaymentEvents.FirstOrDefault(e => e.PaymentEventId == eventId);
            if (evt is null) return false;

            _db.LendingPaymentEvents.Remove(evt);
            record.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<ShareLinkResponse?> GetActiveShareLinkAsync(int userId, int id, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return null;

            var link = await ActiveLinkQuery(id).FirstOrDefaultAsync(ct);
            return link is null ? null : ToShareLinkResponse(link);
        }

        /// <summary>One active link per record — reusing it (and just updating the expiry) means editing the
        /// record never invalidates a link that's already been shared out.</summary>
        public async Task<ShareLinkResponse?> CreateOrUpdateShareLinkAsync(int userId, int id, CreateShareLinkRequest request, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return null;

            var expiresAt = request.ExpiresOn is { } d ? d.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) : (DateTime?)null;

            var link = await ActiveLinkQuery(id).FirstOrDefaultAsync(ct);
            if (link is null)
            {
                link = new LendingShareLink
                {
                    LendingRecordId = id,
                    Token = NewShareToken(),
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow
                };
                _db.LendingShareLinks.Add(link);
            }
            else
            {
                link.ExpiresAt = expiresAt;
            }

            await _db.SaveChangesAsync(ct);
            return ToShareLinkResponse(link);
        }

        public async Task<bool> RevokeShareLinkAsync(int userId, int id, CancellationToken ct = default)
        {
            var record = await FindOwnedAsync(userId, id, ct);
            if (record is null) return false;

            var link = await ActiveLinkQuery(id).FirstOrDefaultAsync(ct);
            if (link is null) return false;

            link.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>Public, unauthenticated lookup — resolves strictly by token, never by record ID, and
        /// returns nothing for a revoked or expired link so a stale/leaked link goes dead on its own.</summary>
        public async Task<PublicShareResponse?> GetPublicShareAsync(string token, CancellationToken ct = default)
        {
            var link = await _db.LendingShareLinks
                .Include(l => l.LendingRecord)
                    .ThenInclude(r => r.PaymentEvents)
                .Include(l => l.LendingRecord.Owner)
                .FirstOrDefaultAsync(l => l.Token == token, ct);

            if (link is null || link.RevokedAt is not null) return null;
            if (link.ExpiresAt is { } exp && exp < DateTime.UtcNow) return null;

            var record = link.LendingRecord;
            var ledger = ComputeLedger(record);

            return new PublicShareResponse
            {
                OwnerName = record.Owner.DisplayName,
                CounterpartyName = record.CounterpartyName,
                Direction = record.Direction,
                Principal = record.Principal,
                AnnualRatePercent = record.AnnualRatePercent,
                GivenDate = record.GivenDate,
                ProposedReturnDate = ledger.CurrentProposedDate,
                IsCleared = record.IsCleared,
                ClearedDate = record.ClearedDate,
                OutstandingPrincipal = ledger.OutstandingPrincipal,
                LastPaidDate = record.PaymentEvents.Where(e => e.AmountPaid != null).Select(e => (DateOnly?)e.EventDate).Max(),
                InterestFromDate = ledger.InterestFromDate,
                InterestToDate = ledger.InterestToDate,
                CarriedUnpaidInterest = ledger.CarriedUnpaidInterest,
                NewInterestSinceFrom = ledger.NewInterestSinceFrom,
                TotalToDate = ledger.TotalToDate,
                HeldCredit = ledger.HeldCredit,
                ExpiresAt = link.ExpiresAt,
                Entries = ledger.Entries
            };
        }

        private IQueryable<LendingShareLink> ActiveLinkQuery(int lendingRecordId) =>
            _db.LendingShareLinks
                .Where(l => l.LendingRecordId == lendingRecordId && l.RevokedAt == null)
                .OrderByDescending(l => l.CreatedAt);

        private static ShareLinkResponse ToShareLinkResponse(LendingShareLink link) => new()
        {
            Token = link.Token,
            ExpiresAt = link.ExpiresAt,
            CreatedAt = link.CreatedAt
        };

        private static string NewShareToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        private Task<LendingRecord?> FindOwnedAsync(int userId, int id, CancellationToken ct) =>
            _db.LendingRecords
                .Include(r => r.PaymentEvents)
                .FirstOrDefaultAsync(r => r.LendingRecordId == id && r.OwnerUserId == userId, ct);

        private static LendingRecordResponse ToResponse(LendingRecord r)
        {
            var ledger = ComputeLedger(r);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var end = r.ClearedDate ?? today;
            var days = DaysBetween(r.GivenDate, end);

            return new LendingRecordResponse
            {
                LendingRecordId = r.LendingRecordId,
                CounterpartyName = r.CounterpartyName,
                CounterpartyUserId = r.CounterpartyUserId,
                Direction = r.Direction,
                Principal = r.Principal,
                AnnualRatePercent = r.AnnualRatePercent,
                GivenDate = r.GivenDate,
                ProposedReturnDate = ledger.CurrentProposedDate,
                IsCleared = r.IsCleared,
                ClearedDate = r.ClearedDate,
                Notes = r.Notes,
                HasEvents = r.PaymentEvents.Count > 0,
                EarliestEventDate = r.PaymentEvents.Select(e => (DateOnly?)e.EventDate).Min(),
                OutstandingPrincipal = ledger.OutstandingPrincipal,
                LastPaidDate = r.PaymentEvents.Where(e => e.AmountPaid != null).Select(e => (DateOnly?)e.EventDate).Max(),
                InterestFromDate = ledger.InterestFromDate,
                InterestToDate = ledger.InterestToDate,
                TotalToDate = ledger.TotalToDate,
                ProjectedInterestByProposedDate = FlatInterest(r, ledger.CurrentProposedDate),
                GrossInterestToDate = FlatInterest(r.Principal, r.AnnualRatePercent, days),
                AmountPaidToDate = ledger.Entries.Sum(e => e.AmountPaid ?? 0m),
                InterestPaidToDate = ledger.Entries.Sum(e => e.InterestApplied),
                HeldCredit = ledger.HeldCredit,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }

        /// <summary>
        /// A plain reference figure — original principal times rate times the full given-to-proposed
        /// span, ignoring any payments already made. Answers "what would the interest be by the
        /// proposed date if nothing had been paid", not "what's still owed by then".
        /// </summary>
        private static decimal? FlatInterest(LendingRecord r, DateOnly? proposedDate)
        {
            if (proposedDate is not { } proposed) return null;

            var days = DaysBetween(r.GivenDate, proposed);
            return FlatInterest(r.Principal, r.AnnualRatePercent, days);
        }

        private static decimal FlatInterest(decimal principal, decimal ratePercent, int days) =>
            Math.Round(principal * ratePercent * days / 365m / 100m, 2);

        /// <summary>Whole days between two dates, clamped to never go negative.</summary>
        private static int DaysBetween(DateOnly from, DateOnly to) => Math.Max(0, to.DayNumber - from.DayNumber);

        private static LendingLedgerResponse ToLedgerResponse(LendingRecord r)
        {
            var ledger = ComputeLedger(r);
            return new LendingLedgerResponse
            {
                LendingRecordId = r.LendingRecordId,
                OriginalPrincipal = r.Principal,
                OutstandingPrincipal = ledger.OutstandingPrincipal,
                InterestFromDate = ledger.InterestFromDate,
                InterestToDate = ledger.InterestToDate,
                CarriedUnpaidInterest = ledger.CarriedUnpaidInterest,
                NewInterestSinceFrom = ledger.NewInterestSinceFrom,
                TotalToDate = ledger.TotalToDate,
                CurrentProposedDate = ledger.CurrentProposedDate,
                HeldCredit = ledger.HeldCredit,
                Entries = ledger.Entries
            };
        }

        private record LedgerResult(
            decimal OutstandingPrincipal,
            decimal InterestToDate,
            decimal TotalToDate,
            DateOnly? CurrentProposedDate,
            decimal HeldCredit,
            DateOnly InterestFromDate,
            decimal CarriedUnpaidInterest,
            decimal NewInterestSinceFrom,
            List<LedgerEntryResponse> Entries);

        /// <summary>
        /// Walks the record's payment events in date order, applying each payment interest-first
        /// then principal (like a normal loan/EMI), so the returned balances/interest reflect any
        /// partial payments and proposed-date renegotiations. Nothing here is persisted — it's all
        /// derived fresh from the raw events every time.
        ///
        /// A payment can optionally mark its leftover-after-interest as "held credit" instead of
        /// reducing principal (HoldExcessAsCredit) — some lenders don't want a payment to reduce
        /// the balance interest is charged on. That credit never drains on its own between events —
        /// it only ever changes at an explicit ApplyHeldCredit event, or gets written off against
        /// the total once the record is actually settled (ClearedDate set). That keeps the figure
        /// a stable fact anyone can check, rather than a number that quietly ages day to day.
        /// </summary>
        private static LedgerResult ComputeLedger(LendingRecord r)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var events = r.PaymentEvents.OrderBy(e => e.EventDate).ThenBy(e => e.PaymentEventId).ToList();

            var principal = r.Principal;
            var carriedInterest = 0m;
            var heldCredit = 0m;
            var cursor = r.GivenDate;
            var currentProposedDate = r.ProposedReturnDate;
            var entries = new List<LedgerEntryResponse>();

            foreach (var e in events)
            {
                var days = DaysBetween(cursor, e.EventDate);
                var periodInterest = principal * r.AnnualRatePercent * days / 365m / 100m;
                var grossInterestOwed = carriedInterest + periodInterest;

                var netInterestOwed = grossInterestOwed;
                var creditConsumed = 0m;
                if (e.ApplyHeldCredit && heldCredit > 0 && netInterestOwed > 0)
                {
                    creditConsumed = Math.Min(heldCredit, netInterestOwed);
                    netInterestOwed -= creditConsumed;
                    heldCredit -= creditConsumed;
                }

                decimal interestApplied = 0m;
                decimal principalApplied = 0m;
                decimal creditAdded = 0m;
                decimal interestCapitalized = 0m;
                if (e.AmountPaid is { } paid)
                {
                    interestApplied = Math.Min(paid, netInterestOwed);
                    var leftover = paid - interestApplied;
                    if (leftover > 0)
                    {
                        if (e.HoldExcessAsCredit)
                        {
                            heldCredit += leftover;
                            creditAdded = leftover;
                        }
                        else
                        {
                            principalApplied = leftover;
                            principal = Math.Max(0, principal - principalApplied);
                        }
                    }
                    carriedInterest = netInterestOwed - interestApplied;
                }
                else if (e.CapitalizeInterest && netInterestOwed > 0)
                {
                    interestCapitalized = netInterestOwed;
                    principal += interestCapitalized;
                    carriedInterest = 0m;
                }
                else
                {
                    carriedInterest = netInterestOwed;
                }

                var previousProposedDate = currentProposedDate;
                if (e.NewProposedDate is { } newDate)
                {
                    currentProposedDate = newDate;
                }

                entries.Add(new LedgerEntryResponse
                {
                    PaymentEventId = e.PaymentEventId,
                    EventDate = e.EventDate,
                    AmountPaid = e.AmountPaid,
                    InterestAccrued = Math.Round(grossInterestOwed, 2),
                    InterestApplied = Math.Round(interestApplied, 2),
                    PrincipalApplied = Math.Round(principalApplied, 2),
                    PrincipalBalanceAfter = Math.Round(principal, 2),
                    PreviousProposedDate = e.NewProposedDate is null ? null : previousProposedDate,
                    NewProposedDate = e.NewProposedDate,
                    Notes = e.Notes,
                    CreditAdded = Math.Round(creditAdded, 2),
                    CreditConsumed = Math.Round(creditConsumed, 2),
                    InterestCapitalized = Math.Round(interestCapitalized, 2)
                });

                cursor = e.EventDate;
            }

            var end = r.ClearedDate ?? today;
            var finalDays = DaysBetween(cursor, end);
            // No automatic credit consumption here — held credit stays exactly what it was after
            // the last logged event until a new ApplyHeldCredit event or settlement changes it.
            // Split in two for display: what was already owed going into this checkpoint (carried
            // forward, unpaid) versus what's accrued fresh since then — a single blended figure
            // next to "Interest From" misleadingly implied the whole amount started from that date.
            var carriedUnpaidInterest = carriedInterest;
            var newInterestSinceFrom = principal * r.AnnualRatePercent * finalDays / 365m / 100m;
            var finalInterestOwed = carriedUnpaidInterest + newInterestSinceFrom;

            var total = principal + finalInterestOwed;

            // Only once the record is actually settled do we write off whatever credit interest
            // never grew enough to absorb — until then it stays banked, not netted against Total.
            if (r.ClearedDate is not null && heldCredit > 0)
            {
                total = Math.Max(0, total - heldCredit);
                heldCredit = 0;
            }

            return new LedgerResult(
                Math.Round(principal, 2),
                Math.Round(finalInterestOwed, 2),
                Math.Round(total, 2),
                currentProposedDate,
                Math.Round(heldCredit, 2),
                cursor,
                Math.Round(carriedUnpaidInterest, 2),
                Math.Round(newInterestSinceFrom, 2),
                entries);
        }

        private static LendingSummaryResponse Summarise(List<LendingRecordResponse> all)
        {
            // Only open records count toward what is still owed either way.
            var active = all.Where(r => !r.IsCleared).ToList();
            var lent = active.Where(r => r.Direction == LendingDirection.Lent).ToList();
            var borrowed = active.Where(r => r.Direction == LendingDirection.Borrowed).ToList();

            var receivableInterest = lent.Sum(r => r.InterestToDate);
            var payableInterest = borrowed.Sum(r => r.InterestToDate);
            // Outstanding, not the original principal — partial payments already paid some of it off.
            var receivablePrincipal = lent.Sum(r => r.OutstandingPrincipal);
            var payablePrincipal = borrowed.Sum(r => r.OutstandingPrincipal);

            return new LendingSummaryResponse
            {
                TotalRecords = all.Count,
                ActiveCount = active.Count,
                ReceivablePrincipal = receivablePrincipal,
                ReceivableInterest = receivableInterest,
                ReceivableTotal = receivablePrincipal + receivableInterest,
                PayablePrincipal = payablePrincipal,
                PayableInterest = payableInterest,
                PayableTotal = payablePrincipal + payableInterest,
                NetPosition = (receivablePrincipal + receivableInterest) - (payablePrincipal + payableInterest)
            };
        }
    }
}
