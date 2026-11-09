using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Tests.Support;

/// <summary>
/// Hosts the real API in-process (real controllers, real EF Core, real PostgreSQL) with two
/// substitutions: the connection string points at the test container and the clock is fake.
/// Creating two factories against the same connection string simulates two backend processes
/// sharing one database, which is exactly the bonus scenario in the brief.
/// </summary>
public sealed class AdmissionApiFactory(string connectionString, int holdDurationSeconds = 600)
    : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdDurationSeconds.ToString());
        // Keep the housekeeping sweeper out of the way; correctness must not depend on it anyway.
        builder.UseSetting("HOLD_SWEEP_INTERVAL_SECONDS", "3600");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISystemClock>();
            services.AddSingleton<ISystemClock>(Clock);
        });
    }
}
