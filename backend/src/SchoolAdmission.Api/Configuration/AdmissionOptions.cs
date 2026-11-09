namespace SchoolAdmission.Api.Configuration;

/// <summary>
/// Runtime settings that the brief requires to be controllable through environment variables.
/// </summary>
public sealed class AdmissionOptions
{
    public const string HoldDurationEnvVar = "HOLD_DURATION_SECONDS";
    public const int DefaultHoldDurationSeconds = 600;

    /// <summary>How long a seat stays held for a student before it is released automatically.</summary>
    public int HoldDurationSeconds { get; init; } = DefaultHoldDurationSeconds;

    public TimeSpan HoldDuration => TimeSpan.FromSeconds(HoldDurationSeconds);

    /// <summary>
    /// Reads HOLD_DURATION_SECONDS from configuration (environment variables are part of
    /// ASP.NET Core configuration). Falls back to 600 when unset. Fails fast on garbage values so a
    /// misconfigured deployment is noticed immediately instead of silently using the wrong expiry.
    /// </summary>
    public static AdmissionOptions FromConfiguration(IConfiguration configuration)
    {
        var raw = configuration[HoldDurationEnvVar];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AdmissionOptions();
        }

        if (!int.TryParse(raw, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException(
                $"{HoldDurationEnvVar} must be a positive integer number of seconds, but was '{raw}'.");
        }

        return new AdmissionOptions { HoldDurationSeconds = seconds };
    }
}
