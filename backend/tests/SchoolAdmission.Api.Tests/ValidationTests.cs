using System.Net;
using System.Text;
using SchoolAdmission.Api.Tests.Support;

namespace SchoolAdmission.Api.Tests;

/// <summary>Rule 7: invalid input is always a 4xx with a readable body, never a 500.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ValidationTests(PostgresFixture postgres) : IAsyncLifetime
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

    [Theory]
    [InlineData("""{"name":"","totalSeats":5}""")]
    [InlineData("""{"name":"   ","totalSeats":5}""")]
    [InlineData("""{"totalSeats":5}""")]
    [InlineData("""{"name":"Class 6"}""")]
    [InlineData("""{"name":"Class 6","totalSeats":0}""")]
    [InlineData("""{"name":"Class 6","totalSeats":-1}""")]
    [InlineData("""{"name":"Class 6","totalSeats":"many"}""")]
    [InlineData("""{"name":"Class 6","totalSeats":2.5}""")]
    [InlineData("""{"name":"Class 6","totalSeats":99999999999}""")]
    [InlineData("""{not json""")]
    [InlineData("")]
    public async Task Creating_a_class_with_bad_input_returns_400_with_a_message(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/classes", content);
        var body = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", body.Code());
        Assert.False(string.IsNullOrWhiteSpace(body?["detail"]?.GetValue<string>()));
    }

    [Theory]
    [InlineData(null, "STUDENT_ID_MISSING")]
    [InlineData("", "STUDENT_ID_MISSING")]
    [InlineData("1234", "STUDENT_ID_INVALID")]
    [InlineData("2015269123456789", "STUDENT_ID_INVALID")]       // 16 digits
    [InlineData("201526912345678901", "STUDENT_ID_INVALID")]     // 18 digits
    [InlineData("2015269123456789a", "STUDENT_ID_INVALID")]      // letter
    public async Task Missing_or_malformed_student_id_is_rejected_with_400(string? header, string expectedCode)
    {
        var classId = await _client.CreateClassAsync($"Validation {Guid.NewGuid():N}", 5);

        var (holdStatus, holdBody) = await _client.HoldAsync(classId, header!);
        Assert.Equal(HttpStatusCode.BadRequest, holdStatus);
        Assert.Equal(expectedCode, holdBody.Code());

        var (mineStatus, _) = await _client.MyHoldsAsync(header!);
        Assert.Equal(HttpStatusCode.BadRequest, mineStatus);

        Assert.Equal(new ClassSeats(5, 0, 0, 5), await _client.GetClassAsync(classId));
    }

    [Fact]
    public async Task Unknown_ids_return_404_not_500()
    {
        var missing = Guid.NewGuid().ToString();
        var student = ApiClient.StudentId(12);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(HttpMethod.Get, $"/classes/{missing}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.HoldAsync(missing, student)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetHoldAsync(missing)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.ConfirmAsync(missing, student)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.CancelAsync(missing, student)).Status);

        // Ids in a format we do not use (like the brief's examples) are also a clean 404.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(HttpMethod.Get, "/classes/c_123")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.ConfirmAsync("h_789", student)).Status);
    }

    [Fact]
    public async Task Class_list_and_detail_expose_exactly_the_contract_fields()
    {
        var classId = await _client.CreateClassAsync($"Contract {Guid.NewGuid():N}", 120);

        var (listStatus, listBody) = await _client.SendAsync(HttpMethod.Get, "/classes");
        Assert.Equal(HttpStatusCode.OK, listStatus);
        var item = Assert.Single(listBody!.AsArray(), c => c!["id"]!.GetValue<string>() == classId);

        foreach (var field in new[] { "id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats" })
        {
            Assert.NotNull(item![field]);
        }
        Assert.Equal(120, item!["totalSeats"]!.GetValue<int>());
        Assert.Equal(120, item["availableSeats"]!.GetValue<int>());
    }
}
