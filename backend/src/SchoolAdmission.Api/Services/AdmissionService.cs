using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// All admission business rules live here. Controllers only translate HTTP to and from this class.
/// </summary>
public sealed class AdmissionService(AdmissionDbContext db, ISystemClock clock, AdmissionOptions options)
{
    // ---------------------------------------------------------------------------------------------
    // Classes
    // ---------------------------------------------------------------------------------------------

    public async Task<ClassCreatedResponse> CreateClassAsync(string name, int totalSeats, CancellationToken ct)
    {
        var entity = new SchoolClass
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            TotalSeats = totalSeats,
            CreatedAt = clock.UtcNow,
        };

        db.Classes.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ClassCreatedResponse(entity.Id.ToString(), entity.Name, entity.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassSeatsResponse>> ListClassesAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var rows = await ProjectSeatSummary(db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt), now)
            .ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ClassSeatsResponse> GetClassAsync(Guid classId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var row = await ProjectSeatSummary(db.Classes.AsNoTracking().Where(c => c.Id == classId), now)
            .SingleOrDefaultAsync(ct)
            ?? throw ApiException.NotFound(ErrorCodes.ClassNotFound, "No class exists with that id.");

        return ToResponse(row);
    }

    /// <summary>
    /// Seat accounting. A hold occupies a seat when it is CONFIRMED, or HELD with an expiry still in
    /// the future. Comparing against "now" inside the query is what makes expiry automatic: the
    /// moment the deadline passes, the seat is counted as free again without any job running.
    /// Filtering/ordering must be applied to <paramref name="classes"/> before this projection:
    /// EF Core cannot translate operators placed after a constructor-based Select.
    /// </summary>
    private static IQueryable<SeatSummaryRow> ProjectSeatSummary(IQueryable<SchoolClass> classes, DateTime now) =>
        classes.Select(c => new SeatSummaryRow(
            c.Id,
            c.Name,
            c.TotalSeats,
            c.CreatedAt,
            c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
            c.Holds.Count(h => h.Status == HoldStatus.Confirmed)));

    private static ClassSeatsResponse ToResponse(SeatSummaryRow row) => new(
        row.Id.ToString(),
        row.Name,
        row.TotalSeats,
        row.Held,
        row.Confirmed,
        Math.Max(0, row.TotalSeats - row.Held - row.Confirmed));

    private sealed record SeatSummaryRow(Guid Id, string Name, int TotalSeats, DateTime CreatedAt, int Held, int Confirmed);

    // ---------------------------------------------------------------------------------------------
    // Holds (applications)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Rule 1 (held + confirmed never exceeds totalSeats) is enforced here.
    ///
    /// Every seat allocation for a class runs inside one database transaction that first locks the
    /// class row with SELECT ... FOR UPDATE. PostgreSQL then queues every other allocation for the
    /// same class behind this one, so the "count active, compare, insert" sequence can never
    /// interleave with another request. Because the lock lives in PostgreSQL rather than in process
    /// memory, it holds across any number of API instances sharing the database (the bonus case).
    /// Different classes use different rows, so they do not block each other.
    /// </summary>
    public async Task<HoldCreatedResponse> CreateHoldAsync(Guid classId, string studentId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var schoolClass = await LockClassRowAsync(classId, ct)
            ?? throw ApiException.NotFound(ErrorCodes.ClassNotFound, "No class exists with that id.");

        // Lazy expiry under the lock: persist EXPIRED for any stale HELD rows of this class so the
        // partial unique index (one active application per student) does not trip on a hold whose
        // deadline has already passed.
        await db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(h => h.Status, HoldStatus.Expired), ct);

        // Rule 3: a student may have only one active application or confirmed admission per class.
        var alreadyApplied = await db.Holds.AnyAsync(h =>
            h.ClassId == classId
            && h.StudentId == studentId
            && (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)), ct);

        if (alreadyApplied)
        {
            throw ApiException.Conflict(ErrorCodes.DuplicateApplication,
                "This student already has an active application or confirmed admission for this class.");
        }

        // Rule 1: compare against the live occupancy while holding the lock.
        var occupied = await db.Holds.CountAsync(h =>
            h.ClassId == classId
            && (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)), ct);

        if (occupied >= schoolClass.TotalSeats)
        {
            throw ApiException.Conflict(ErrorCodes.NoSeatsAvailable,
                "No seats are available in this class right now. A seat may open up if another application expires.");
        }

        var hold = new SeatHold
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now + options.HoldDuration,
        };

        db.Holds.Add(hold);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new HoldCreatedResponse(
            hold.Id.ToString(),
            hold.ClassId.ToString(),
            hold.StudentId,
            hold.Status.ToApiString(),
            hold.ExpiresAt);
    }

    /// <summary>
    /// Rules 4 and 5: only the owner may confirm, only while HELD and unexpired, and confirming an
    /// already CONFIRMED hold is a no-op that returns the same answer (idempotent).
    /// </summary>
    public async Task<HoldConfirmedResponse> ConfirmHoldAsync(Guid holdId, string studentId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var hold = await LockHoldRowAsync(holdId, ct)
            ?? throw ApiException.NotFound(ErrorCodes.HoldNotFound, "No application exists with that id.");

        EnsureOwner(hold, studentId);

        switch (hold.EffectiveStatusAt(now))
        {
            case HoldStatus.Confirmed:
                // Second (or later) confirm: nothing changes, confirmedSeats is not incremented again.
                await tx.CommitAsync(ct);
                return new HoldConfirmedResponse(hold.Id.ToString(), HoldStatus.Confirmed.ToApiString());

            case HoldStatus.Cancelled:
                throw ApiException.Conflict(ErrorCodes.HoldCancelled,
                    "This application was cancelled, so it can no longer be confirmed.");

            case HoldStatus.Expired:
                await PersistExpiryAsync(hold, ct);
                await tx.CommitAsync(ct);
                throw ApiException.Conflict(ErrorCodes.HoldExpired,
                    "The time limit for this application has passed. Please apply again if seats are available.");

            case HoldStatus.Held:
                hold.Status = HoldStatus.Confirmed;
                hold.ConfirmedAt = now;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new HoldConfirmedResponse(hold.Id.ToString(), hold.Status.ToApiString());

            default:
                throw new InvalidOperationException($"Unexpected hold status {hold.Status}");
        }
    }

    /// <summary>
    /// Cancel is allowed for HELD and CONFIRMED applications (both release the seat), is idempotent
    /// for already CANCELLED ones, and is rejected for EXPIRED ones because there is nothing left
    /// to cancel. See ASSUMPTIONS.md.
    /// </summary>
    public async Task CancelHoldAsync(Guid holdId, string studentId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var hold = await LockHoldRowAsync(holdId, ct)
            ?? throw ApiException.NotFound(ErrorCodes.HoldNotFound, "No application exists with that id.");

        EnsureOwner(hold, studentId);

        switch (hold.EffectiveStatusAt(now))
        {
            case HoldStatus.Cancelled:
                await tx.CommitAsync(ct);
                return;

            case HoldStatus.Expired:
                await PersistExpiryAsync(hold, ct);
                await tx.CommitAsync(ct);
                throw ApiException.Conflict(ErrorCodes.HoldExpired,
                    "This application has already expired; there is nothing to cancel.");

            case HoldStatus.Held:
            case HoldStatus.Confirmed:
                hold.Status = HoldStatus.Cancelled;
                hold.CancelledAt = now;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;

            default:
                throw new InvalidOperationException($"Unexpected hold status {hold.Status}");
        }
    }

    public async Task<HoldResponse> GetHoldAsync(Guid holdId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hold = await db.Holds.AsNoTracking()
            .Include(h => h.Class)
            .SingleOrDefaultAsync(h => h.Id == holdId, ct)
            ?? throw ApiException.NotFound(ErrorCodes.HoldNotFound, "No application exists with that id.");

        return ToResponse(hold, now);
    }

    public async Task<IReadOnlyList<HoldResponse>> ListMyHoldsAsync(string studentId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var holds = await db.Holds.AsNoTracking()
            .Include(h => h.Class)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        return holds.Select(h => ToResponse(h, now)).ToList();
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    /// <summary>Row lock on the class. Blocks until any concurrent allocation for it has committed.</summary>
    private async Task<SchoolClass?> LockClassRowAsync(Guid classId, CancellationToken ct)
    {
        var rows = await db.Classes
            .FromSqlInterpolated($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    /// <summary>Row lock on the hold so confirm/cancel cannot race each other or the sweeper.</summary>
    private async Task<SeatHold?> LockHoldRowAsync(Guid holdId, CancellationToken ct)
    {
        var rows = await db.Holds
            .FromSqlInterpolated($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    private static void EnsureOwner(SeatHold hold, string studentId)
    {
        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            throw ApiException.Forbidden(ErrorCodes.NotHoldOwner,
                "This application belongs to a different student.");
        }
    }

    private async Task PersistExpiryAsync(SeatHold hold, CancellationToken ct)
    {
        if (hold.Status == HoldStatus.Held)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync(ct);
        }
    }

    private static HoldResponse ToResponse(SeatHold hold, DateTime now) => new(
        hold.Id.ToString(),
        hold.ClassId.ToString(),
        hold.Class.Name,
        hold.StudentId,
        hold.EffectiveStatusAt(now).ToApiString(),
        hold.ExpiresAt,
        hold.CreatedAt,
        hold.ConfirmedAt);
}
