namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Single source of "now" for the application. Abstracted so tests can advance time instead of
/// sleeping through real hold expiries.
/// </summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
