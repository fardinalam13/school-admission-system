namespace SchoolAdmission.Api.Domain;

/// <summary>
/// Lifecycle of a seat application. Persisted as upper-case strings (HELD, CONFIRMED, EXPIRED,
/// CANCELLED) which are also exactly the values the API contract exposes.
/// </summary>
public enum HoldStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled,
}

public static class HoldStatusExtensions
{
    public static string ToApiString(this HoldStatus status) => status switch
    {
        HoldStatus.Held => "HELD",
        HoldStatus.Confirmed => "CONFIRMED",
        HoldStatus.Expired => "EXPIRED",
        HoldStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static HoldStatus ParseApiString(string value) => value switch
    {
        "HELD" => HoldStatus.Held,
        "CONFIRMED" => HoldStatus.Confirmed,
        "EXPIRED" => HoldStatus.Expired,
        "CANCELLED" => HoldStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown hold status '{value}' in database."),
    };
}
