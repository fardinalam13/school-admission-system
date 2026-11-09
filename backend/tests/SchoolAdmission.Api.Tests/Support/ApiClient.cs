using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SchoolAdmission.Api.Tests.Support;

/// <summary>Thin helpers over HttpClient that mirror the eight endpoints of the brief.</summary>
public static class ApiClient
{
    public const string StudentHeader = "X-Student-Id";

    /// <summary>Generates a distinct, valid 17-digit student id for index <paramref name="i"/>.</summary>
    public static string StudentId(int i) => $"2{i:D16}";

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(
        this HttpClient client, HttpMethod method, string path, string? studentId = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        // TryAddWithoutValidation so tests can deliberately send an empty header value.
        if (studentId is not null) request.Headers.TryAddWithoutValidation(StudentHeader, studentId);
        if (body is not null) request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var node = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        return (response.StatusCode, node);
    }

    public static async Task<string> CreateClassAsync(this HttpClient client, string name, int totalSeats)
    {
        var (status, body) = await client.SendAsync(HttpMethod.Post, "/classes", body: new { name, totalSeats });
        Assert.Equal(HttpStatusCode.Created, status);
        return body!["id"]!.GetValue<string>();
    }

    public static Task<(HttpStatusCode Status, JsonNode? Body)> HoldAsync(this HttpClient client, string classId, string studentId) =>
        client.SendAsync(HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public static async Task<string> HoldOkAsync(this HttpClient client, string classId, string studentId)
    {
        var (status, body) = await client.HoldAsync(classId, studentId);
        Assert.Equal(HttpStatusCode.Created, status);
        return body!["holdId"]!.GetValue<string>();
    }

    public static Task<(HttpStatusCode Status, JsonNode? Body)> ConfirmAsync(this HttpClient client, string holdId, string studentId) =>
        client.SendAsync(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);

    public static Task<(HttpStatusCode Status, JsonNode? Body)> CancelAsync(this HttpClient client, string holdId, string studentId) =>
        client.SendAsync(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    public static Task<(HttpStatusCode Status, JsonNode? Body)> GetHoldAsync(this HttpClient client, string holdId) =>
        client.SendAsync(HttpMethod.Get, $"/holds/{holdId}");

    public static Task<(HttpStatusCode Status, JsonNode? Body)> MyHoldsAsync(this HttpClient client, string studentId) =>
        client.SendAsync(HttpMethod.Get, "/me/holds", studentId);

    public static async Task<ClassSeats> GetClassAsync(this HttpClient client, string classId)
    {
        var (status, body) = await client.SendAsync(HttpMethod.Get, $"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, status);
        return new ClassSeats(
            body!["totalSeats"]!.GetValue<int>(),
            body["heldSeats"]!.GetValue<int>(),
            body["confirmedSeats"]!.GetValue<int>(),
            body["availableSeats"]!.GetValue<int>());
    }

    public static string Code(this JsonNode? body) => body?["code"]?.GetValue<string>() ?? "<no code>";
    public static string Status(this JsonNode? body) => body?["status"]?.GetValue<string>() ?? "<no status>";
}

public sealed record ClassSeats(int Total, int Held, int Confirmed, int Available);
