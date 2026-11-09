using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Tests.Support;

/// <summary>
/// Replaces the real clock inside the API under test so expiry can be reached instantly instead of
/// waiting for HOLD_DURATION_SECONDS to elapse in real time.
/// </summary>
public sealed class FakeClock : ISystemClock
{
    private long _ticks = DateTime.UtcNow.Ticks;

    public DateTime UtcNow => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
