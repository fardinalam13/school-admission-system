using SchoolAdmission.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------------------------
// Database connection: environment variable ConnectionStrings__Default (standard ASP.NET Core
// mapping to GetConnectionString("Default")). Hold duration: HOLD_DURATION_SECONDS, default 600.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Database connection string missing. Set the ConnectionStrings__Default environment variable.");

var admissionOptions = AdmissionOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(admissionOptions);

// --- Services ----------------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The React frontend is served from a different origin (its own container/port), so the browser
// needs CORS permission to call this API. No credentials are used, so a permissive policy is fine.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// --- Pipeline ----------------------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Logger.LogInformation("Hold duration: {Seconds}s", admissionOptions.HoldDurationSeconds);

app.Run();

// Exposed so the integration test project can host the app in-process with WebApplicationFactory.
public partial class Program;
