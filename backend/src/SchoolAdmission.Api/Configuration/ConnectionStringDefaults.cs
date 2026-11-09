using Npgsql;

namespace SchoolAdmission.Api.Configuration;

/// <summary>
/// Applies pool settings suited to a burst of simultaneous applications, unless the supplied
/// connection string already sets them explicitly.
///
/// Why this matters: every application for a class waits in line on the class row lock, and each
/// waiter holds one pooled connection while it waits. With Npgsql's defaults (100 connections,
/// 15 s wait) a burst of a few hundred requests exhausts the pool and the overflow fails with a
/// 500 instead of queuing briefly and receiving its proper 201 or 409. A bounded pool per API
/// instance also leaves PostgreSQL headroom when several instances share one database.
/// </summary>
public static class ConnectionStringDefaults
{
    public const int DefaultMaxPoolSize = 50;
    public const int DefaultPoolWaitTimeoutSeconds = 60;

    public static string Apply(string connectionString)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var explicitKeys = ExplicitKeys(connectionString);

        if (!explicitKeys.Contains("maximum pool size") && !explicitKeys.Contains("max pool size"))
        {
            csb.MaxPoolSize = DefaultMaxPoolSize;
        }

        // Npgsql's "Timeout" is also how long a request waits for a free pooled connection.
        if (!explicitKeys.Contains("timeout"))
        {
            csb.Timeout = DefaultPoolWaitTimeoutSeconds;
        }

        return csb.ConnectionString;
    }

    private static HashSet<string> ExplicitKeys(string connectionString) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)[0].Trim().ToLowerInvariant())
            .ToHashSet();
}
