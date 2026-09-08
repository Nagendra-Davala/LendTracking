using LendTracking.Data;
using LendTracking.Model.Entities;
using LendTracking.Model.Lending;
using Microsoft.EntityFrameworkCore;

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
            var query = _db.LendingRecords.Where(r => r.OwnerUserId == userId);

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

            record.CounterpartyName = request.CounterpartyName.Trim();
            record.CounterpartyUserId = request.CounterpartyUserId;
            record.Direction = request.Direction;
            record.Principal = request.Principal;
            record.AnnualRatePercent = request.AnnualRatePercent;
            record.GivenDate = request.GivenDate;
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

        private Task<LendingRecord?> FindOwnedAsync(int userId, int id, CancellationToken ct) =>
            _db.LendingRecords.FirstOrDefaultAsync(r => r.LendingRecordId == id && r.OwnerUserId == userId, ct);

        private static LendingRecordResponse ToResponse(LendingRecord r)
        {
            // Interest accrues until settlement, or until today while still open.
            var end = r.ClearedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var days = Math.Max(0, end.DayNumber - r.GivenDate.DayNumber);
            var interest = Math.Round(r.Principal * r.AnnualRatePercent * days / 365m / 100m, 2);

            return new LendingRecordResponse
            {
                LendingRecordId = r.LendingRecordId,
                CounterpartyName = r.CounterpartyName,
                CounterpartyUserId = r.CounterpartyUserId,
                Direction = r.Direction,
                Principal = r.Principal,
                AnnualRatePercent = r.AnnualRatePercent,
                GivenDate = r.GivenDate,
                IsCleared = r.IsCleared,
                ClearedDate = r.ClearedDate,
                Notes = r.Notes,
                InterestToDate = interest,
                TotalToDate = r.Principal + interest,
                DaysElapsed = days,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }

        private static LendingSummaryResponse Summarise(List<LendingRecordResponse> all)
        {
            // Only open records count toward what is still owed either way.
            var active = all.Where(r => !r.IsCleared).ToList();
            var lent = active.Where(r => r.Direction == LendingDirection.Lent).ToList();
            var borrowed = active.Where(r => r.Direction == LendingDirection.Borrowed).ToList();

            var receivableInterest = lent.Sum(r => r.InterestToDate);
            var payableInterest = borrowed.Sum(r => r.InterestToDate);
            var receivablePrincipal = lent.Sum(r => r.Principal);
            var payablePrincipal = borrowed.Sum(r => r.Principal);

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
