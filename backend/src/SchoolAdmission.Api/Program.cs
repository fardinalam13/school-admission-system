using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------------------------
// Database connection: environment variable ConnectionStrings__Default (standard ASP.NET Core
// mapping to GetConnectionString("Default")). Hold duration: HOLD_DURATION_SECONDS, default 600.
var rawConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Database connection string missing. Set the ConnectionStrings__Default environment variable.");
var connectionString = ConnectionStringDefaults.Apply(rawConnectionString);

var admissionOptions = AdmissionOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(admissionOptions);
builder.Services.AddSingleton<ISystemClock, SystemClock>();

// --- Data access -------------------------------------------------------------------------------
builder.Services.AddDbContext<AdmissionDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Application -------------------------------------------------------------------------------
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddHostedService<HoldExpirySweeper>();

// --- Web ---------------------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(apiOptions =>
    {
        // Rule 7: bad input is always a 4xx with a readable message. Turn framework model-binding
        // and DataAnnotations failures into the same ProblemDetails shape our own errors use.
        apiOptions.InvalidModelStateResponseFactory = context =>
        {
            var problem = GlobalExceptionHandler.Problem(
                StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, DescribeModelError(context.ModelState));
            return new BadRequestObjectResult(problem);
        };
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The React frontend is served from a different origin (its own container/port), so the browser
// needs CORS permission to call this API. No credentials are used, so a permissive policy is fine.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Create or upgrade the schema before accepting traffic. Retries while PostgreSQL is still booting.
await DatabaseInitializer.MigrateWithRetryAsync(app.Services);

// --- Pipeline ----------------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Logger.LogInformation("Hold duration: {Seconds}s", admissionOptions.HoldDurationSeconds);

app.Run();

// Exposed so the integration test project can host the app in-process with WebApplicationFactory.
public partial class Program
{
    /// <summary>
    /// Picks one human-readable sentence out of a failed ModelState. DataAnnotations messages are
    /// used verbatim; JSON parsing and type-mismatch failures (whose raw messages mention line
    /// numbers and parser internals) are replaced with plain wording.
    /// </summary>
    private static string DescribeModelError(ModelStateDictionary modelState)
    {
        var failed = modelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .ToList();

        // System.Text.Json failures surface under "$" (whole body) or "$.field" keys. When one of
        // those exists it is the real cause; the framework also adds a generic "request field is
        // required" entry that would otherwise hide it, so check the JSON keys first.
        var jsonFailure = failed.FirstOrDefault(kv => kv.Key.StartsWith('$'));
        if (jsonFailure.Key is not null)
        {
            var field = jsonFailure.Key.TrimStart('$', '.');
            return field.Length == 0
                ? "The request body is not valid JSON."
                : $"The value of '{field}' is not valid.";
        }

        var message = failed
            .Select(kv => kv.Value!.Errors[0].ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));

        return message ?? "The request body is invalid.";
    }
}
