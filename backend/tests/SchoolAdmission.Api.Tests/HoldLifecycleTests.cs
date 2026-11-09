using System.Net;
using SchoolAdmission.Api.Tests.Support;

namespace SchoolAdmission.Api.Tests;

/// <summary>Rules 2 to 5: expiry, one active application per class, owner-only confirm, idempotency.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HoldLifecycleTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const int HoldSeconds = 600;
    private AdmissionApiFactory _api = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _api = new AdmissionApiFactory(postgres.ConnectionString, HoldSeconds);
        _client = _api.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _api.DisposeAsync();
    }

    private Task<string> NewClassAsync(int seats = 5) => _client.CreateClassAsync($"Lifecycle {Guid.NewGuid():N}", seats);

    [Fact]
    public async Task Creating_a_hold_returns_contract_fields_and_counts_a_seat()
    {
        var classId = await NewClassAsync(3);
        var student = ApiClient.StudentId(1);

        var (status, body) = await _client.HoldAsync(classId, student);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(classId, body!["classId"]!.GetValue<string>());
        Assert.Equal(student, body["studentId"]!.GetValue<string>());
        Assert.Equal("HELD", body.Status());
        var expiresAt = body["expiresAt"]!.GetValue<DateTime>().ToUniversalTime();
        Assert.Equal(_api.Clock.UtcNow.AddSeconds(HoldSeconds), expiresAt, TimeSpan.FromSeconds(1));

        Assert.Equal(new ClassSeats(3, Held: 1, Confirmed: 0, Available: 2), await _client.GetClassAsync(classId));
    }

    [Fact]
    public async Task Expired_hold_releases_its_seat_without_any_api_call_and_cannot_be_confirmed()
    {
        var classId = await NewClassAsync(1);
        var student = ApiClient.StudentId(2);
        var holdId = await _client.HoldOkAsync(classId, student);
        Assert.Equal(0, (await _client.GetClassAsync(classId)).Available);

        // Rule 2: nothing is called in between; time simply passes.
        _api.Clock.Advance(TimeSpan.FromSeconds(HoldSeconds + 1));

        var (getStatus, getBody) = await _client.GetHoldAsync(holdId);
        Assert.Equal(HttpStatusCode.OK, getStatus);
        Assert.Equal("EXPIRED", getBody.Status());
        Assert.Equal(new ClassSeats(1, 0, 0, Available: 1), await _client.GetClassAsync(classId));

        var (confirmStatus, confirmBody) = await _client.ConfirmAsync(holdId, student);
        Assert.Equal(HttpStatusCode.Conflict, confirmStatus);
        Assert.Equal("HOLD_EXPIRED", confirmBody.Code());

        // The freed seat is genuinely usable again, including by the same student.
        var (reapplyStatus, _) = await _client.HoldAsync(classId, student);
        Assert.Equal(HttpStatusCode.Created, reapplyStatus);
    }

    [Fact]
    public async Task Confirming_twice_increments_confirmed_seats_only_once()
    {
        var classId = await NewClassAsync(4);
        var student = ApiClient.StudentId(3);
        var holdId = await _client.HoldOkAsync(classId, student);

        var first = await _client.ConfirmAsync(holdId, student);
        var second = await _client.ConfirmAsync(holdId, student);
        var third = await _client.ConfirmAsync(holdId, student);

        foreach (var (status, body) in new[] { first, second, third })
        {
            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(holdId, body!["holdId"]!.GetValue<string>());
            Assert.Equal("CONFIRMED", body.Status());
        }

        Assert.Equal(new ClassSeats(4, Held: 0, Confirmed: 1, Available: 3), await _client.GetClassAsync(classId));
    }

    [Fact]
    public async Task Confirmed_admission_does_not_expire()
    {
        var classId = await NewClassAsync(2);
        var student = ApiClient.StudentId(4);
        var holdId = await _client.HoldOkAsync(classId, student);
        await _client.ConfirmAsync(holdId, student);

        _api.Clock.Advance(TimeSpan.FromDays(30));

        var (_, body) = await _client.GetHoldAsync(holdId);
        Assert.Equal("CONFIRMED", body.Status());
        Assert.Equal(new ClassSeats(2, 0, Confirmed: 1, Available: 1), await _client.GetClassAsync(classId));
    }

    [Fact]
    public async Task Only_the_owner_can_confirm_or_cancel()
    {
        var classId = await NewClassAsync();
        var owner = ApiClient.StudentId(5);
        var stranger = ApiClient.StudentId(6);
        var holdId = await _client.HoldOkAsync(classId, owner);

        var (confirmStatus, confirmBody) = await _client.ConfirmAsync(holdId, stranger);
        Assert.Equal(HttpStatusCode.Forbidden, confirmStatus);
        Assert.Equal("NOT_HOLD_OWNER", confirmBody.Code());

        var (cancelStatus, _) = await _client.CancelAsync(holdId, stranger);
        Assert.Equal(HttpStatusCode.Forbidden, cancelStatus);

        Assert.Equal("HELD", (await _client.GetHoldAsync(holdId)).Body.Status());
    }

    [Fact]
    public async Task One_student_cannot_hold_two_seats_in_the_same_class_but_can_in_another()
    {
        var classA = await NewClassAsync();
        var classB = await NewClassAsync();
        var student = ApiClient.StudentId(7);

        await _client.HoldOkAsync(classA, student);

        var (dupStatus, dupBody) = await _client.HoldAsync(classA, student);
        Assert.Equal(HttpStatusCode.Conflict, dupStatus);
        Assert.Equal("DUPLICATE_APPLICATION", dupBody.Code());

        var (otherStatus, _) = await _client.HoldAsync(classB, student);
        Assert.Equal(HttpStatusCode.Created, otherStatus);
    }

    [Fact]
    public async Task Cancel_frees_the_seat_and_is_idempotent()
    {
        var classId = await NewClassAsync(1);
        var student = ApiClient.StudentId(8);
        var holdId = await _client.HoldOkAsync(classId, student);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.CancelAsync(holdId, student)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.CancelAsync(holdId, student)).Status);

        Assert.Equal("CANCELLED", (await _client.GetHoldAsync(holdId)).Body.Status());
        Assert.Equal(new ClassSeats(1, 0, 0, Available: 1), await _client.GetClassAsync(classId));

        // Seat is free for somebody else.
        Assert.Equal(HttpStatusCode.Created, (await _client.HoldAsync(classId, ApiClient.StudentId(9))).Status);
    }

    [Fact]
    public async Task My_holds_lists_every_application_of_the_student_with_class_name_and_live_status()
    {
        var className = $"MyHolds {Guid.NewGuid():N}";
        var classId = await _client.CreateClassAsync(className, 5);
        var student = ApiClient.StudentId(10);
        var holdId = await _client.HoldOkAsync(classId, student);

        var (status, body) = await _client.MyHoldsAsync(student);

        Assert.Equal(HttpStatusCode.OK, status);
        var items = body!.AsArray();
        var mine = Assert.Single(items, i => i!["holdId"]!.GetValue<string>() == holdId);
        Assert.Equal(classId, mine!["classId"]!.GetValue<string>());
        Assert.Equal(className, mine["className"]!.GetValue<string>());
        Assert.Equal("HELD", mine.Status());
        Assert.NotNull(mine["expiresAt"]);

        // Another student sees nothing of it.
        var (_, otherBody) = await _client.MyHoldsAsync(ApiClient.StudentId(11));
        Assert.DoesNotContain(otherBody!.AsArray(), i => i!["holdId"]!.GetValue<string>() == holdId);
    }
}
