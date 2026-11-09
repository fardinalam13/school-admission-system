using Testcontainers.PostgreSql;

namespace SchoolAdmission.Api.Tests.Support;

/// <summary>
/// One real PostgreSQL 16 container for the whole test run (Testcontainers needs Docker). Tests
/// create their own classes with unique names, so they can share one database without isolation
/// problems, and the schema is created once by the API's own startup migration.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("admission_test")
        .WithUsername("admission")
        .WithPassword("admission")
        // Same headroom as docker-compose.yml; the two-instance test opens two connection pools.
        .WithCommand("-c", "max_connections=200")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
