namespace SchoolAdmission.Api.Domain;

/// <summary>
/// A student's application for one seat in one class. Created as HELD with an expiry; becomes
/// CONFIRMED when the fee is paid, EXPIRED when the deadline passes, or CANCELLED by the student.
/// </summary>
public sealed class SeatHold
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }

    /// <summary>17-digit birth registration number supplied in the X-Student-Id header.</summary>
    public string StudentId { get; set; } = string.Empty;

    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public SchoolClass Class { get; set; } = null!;

    /// <summary>
    /// A hold still occupies a seat while it is CONFIRMED, or HELD and not yet past its expiry.
    /// Expiry is evaluated lazily against the supplied clock so no background job is ever required
    /// for a seat to be released.
    /// </summary>
    public bool OccupiesSeatAt(DateTime utcNow) =>
        Status == HoldStatus.Confirmed || (Status == HoldStatus.Held && ExpiresAt > utcNow);

    /// <summary>Effective status, treating a HELD row whose expiry has passed as EXPIRED.</summary>
    public HoldStatus EffectiveStatusAt(DateTime utcNow) =>
        Status == HoldStatus.Held && ExpiresAt <= utcNow ? HoldStatus.Expired : Status;
}
