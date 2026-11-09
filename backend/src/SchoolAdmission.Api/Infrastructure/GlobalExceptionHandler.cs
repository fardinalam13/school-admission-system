using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Converts every unhandled exception into a JSON ProblemDetails body so clients never see an
/// HTML error page or an empty 500. Business failures (<see cref="ApiException"/>) keep their
/// intended 4xx status. Anything unexpected becomes a generic 500 without leaking internals.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ApiException api => Problem(api.StatusCode, api.Code, api.Message),

            // Safety net for the partial unique index on holds (one active application per student
            // per class). Normal flow never reaches this because of the class row lock.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Problem(StatusCodes.Status409Conflict, ErrorCodes.DuplicateApplication,
                    "This student already has an active application for this class."),

            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
                "Something went wrong on the server. Please try again.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    public static ProblemDetails Problem(int status, string code, string detail) => new()
    {
        Status = status,
        Title = ReasonPhrase(status),
        Detail = detail,
        Extensions = { ["code"] = code },
    };

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error",
    };
}
