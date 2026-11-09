using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Housekeeping only. Seat availability never depends on this job: every read and every write
/// already treats a HELD row past its expiry as EXPIRED. This sweeper simply persists that fact so
/// the stored status matches what the API reports and the partial unique index stays tidy.
/// The UPDATE is idempotent, so several API processes can run it concurrently without harm.
/// </summary>
public sealed class HoldExpirySweeper(
    IServiceScopeFactory scopeFactory,
    ISystemClock clock,
    IConfiguration configuration,
    ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    public const string IntervalEnvVar = "HOLD_SWEEP_INTERVAL_SECONDS";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            int.TryParse(configuration[IntervalEnvVar], out var s) && s > 0 ? s : 30);

        logger.LogInformation("Hold expiry sweeper running every {Interval}s", interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
                var now = clock.UtcNow;

                var flipped = await db.Holds
                    .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= now)
                    .ExecuteUpdateAsync(set => set.SetProperty(h => h.Status, HoldStatus.Expired), stoppingToken);

                if (flipped > 0)
                {
                    logger.LogInformation("Marked {Count} expired hold(s)", flipped);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never let housekeeping take the API down; just try again next tick.
                logger.LogWarning(ex, "Hold expiry sweep failed; will retry");
            }
        }
    }
}
