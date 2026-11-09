namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Turns <see cref="ApiException"/> (expected business outcomes such as "class is full" or
/// "not the owner") into their 4xx ProblemDetails response directly. Handling them here, before
/// the framework's exception handler, keeps them out of the error log: a seat being full is a
/// normal event, not a server fault. Everything else still flows to <see cref="GlobalExceptionHandler"/>.
/// </summary>
public sealed class ApiExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex) when (!context.Response.HasStarted)
        {
            var problem = GlobalExceptionHandler.Problem(ex.StatusCode, ex.Code, ex.Message);
            context.Response.Clear();
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }
}
