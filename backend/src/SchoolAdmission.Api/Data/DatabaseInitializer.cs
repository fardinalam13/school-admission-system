using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Applies EF Core migrations at startup.
///
/// Two concerns are handled here:
///  1. In docker compose the API usually starts before PostgreSQL accepts connections, so we retry
///     for a while instead of crashing.
///  2. The bonus requirement runs two API processes against one database. EF migrations are not
///     safe to run concurrently, so a PostgreSQL session-level advisory lock serialises them: the
///     second process waits, then finds nothing left to apply.
/// </summary>
public static class DatabaseInitializer
{
    // Arbitrary constant; only needs to be identical in every process that migrates this database.
    private const long MigrationLockKey = 7_245_001;

    public static async Task MigrateWithRetryAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        const int maxAttempts = 30;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

                // Open the connection ourselves so the advisory lock and Migrate() share one session.
                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
                    try
                    {
                        await db.Database.MigrateAsync(cancellationToken);
                    }
                    finally
                    {
                        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", cancellationToken);
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                logger.LogInformation("Database schema is up to date.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                logger.LogWarning(
                    "Database not ready (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s...",
                    attempt, maxAttempts, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException or TimeoutException or SocketException
        || ex.InnerException is NpgsqlException or SocketException;
}
