using System.Net;
using SchoolAdmission.Api.Tests.Support;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Rule 1: held + confirmed must never exceed totalSeats no matter how many requests arrive at
/// once. These tests fire many hold requests in parallel and count the outcomes.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConcurrencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private AdmissionApiFactory _api = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _api = new AdmissionApiFactory(postgres.ConnectionString);
        _client = _api.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task Parallel_hold_requests_never_oversubscribe_a_class()
    {
        const int seats = 10;
        const int applicants = 150;
        var classId = await _client.CreateClassAsync($"Concurrency {Guid.NewGuid():N}", seats);

        // Release all requests at the same instant so they genuinely contend for the class row.
        var gate = new TaskCompletionSource();
        var attempts = Enumerable.Range(0, applicants).Select(async i =>
        {
            await gate.Task;
            return await _client.HoldAsync(classId, ApiClient.StudentId(i));
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        var created = results.Count(r => r.Status == HttpStatusCode.Created);
        var rejected = results.Count(r => r.Status == HttpStatusCode.Conflict);

        Assert.Equal(seats, created);
        Assert.Equal(applicants - seats, rejected);
        Assert.All(results.Where(r => r.Status == HttpStatusCode.Conflict),
            r => Assert.Equal("NO_SEATS_AVAILABLE", r.Body.Code()));

        var summary = await _client.GetClassAsync(classId);
        Assert.Equal(new ClassSeats(seats, Held: seats, Confirmed: 0, Available: 0), summary);
    }

    [Fact]
    public async Task Two_backend_instances_sharing_one_database_still_respect_the_seat_limit()
    {
        // Bonus scenario from the brief: two API processes on different ports, one database.
        // A second WebApplicationFactory is a second, fully independent host (own DI container,
        // own connection pool) pointed at the same PostgreSQL, so any in-memory locking would fail
        // this test. Only database-level locking can pass it.
        await using var secondApi = new AdmissionApiFactory(postgres.ConnectionString);
        using var secondClient = secondApi.CreateClient();

        const int seats = 7;
        const int applicants = 120;
        var classId = await _client.CreateClassAsync($"TwoProcess {Guid.NewGuid():N}", seats);

        var gate = new TaskCompletionSource();
        var attempts = Enumerable.Range(0, applicants).Select(async i =>
        {
            await gate.Task;
            var client = i % 2 == 0 ? _client : secondClient;
            return await client.HoldAsync(classId, ApiClient.StudentId(1_000 + i));
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(seats, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(applicants - seats, results.Count(r => r.Status == HttpStatusCode.Conflict));

        // Both instances must report the same, correct picture.
        Assert.Equal(new ClassSeats(seats, seats, 0, 0), await _client.GetClassAsync(classId));
        Assert.Equal(new ClassSeats(seats, seats, 0, 0), await secondClient.GetClassAsync(classId));
    }

    [Fact]
    public async Task Same_student_hammering_one_class_gets_exactly_one_hold()
    {
        // Rule 3 under contention: double-clicks and retries must not create duplicate holds.
        var classId = await _client.CreateClassAsync($"SameStudent {Guid.NewGuid():N}", 50);
        var studentId = ApiClient.StudentId(99_999);

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => _client.HoldAsync(classId, studentId)));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(39, results.Count(r => r.Status == HttpStatusCode.Conflict));
        Assert.All(results.Where(r => r.Status == HttpStatusCode.Conflict),
            r => Assert.Equal("DUPLICATE_APPLICATION", r.Body.Code()));

        var summary = await _client.GetClassAsync(classId);
        Assert.Equal(1, summary.Held);
    }
}
