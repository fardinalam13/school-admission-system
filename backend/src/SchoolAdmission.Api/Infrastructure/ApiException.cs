namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// A business-rule or lookup failure that maps directly to a 4xx response. Thrown from the service
/// layer and translated into a ProblemDetails body by <see cref="GlobalExceptionHandler"/>.
/// The <see cref="Code"/> is a stable machine-readable identifier the frontend uses to pick a
/// user-friendly message; <see cref="Message"/> is already human-readable English.
/// </summary>
public sealed class ApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static ApiException NotFound(string code, string message) => new(StatusCodes.Status404NotFound, code, message);
    public static ApiException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    public static ApiException Forbidden(string code, string message) => new(StatusCodes.Status403Forbidden, code, message);
    public static ApiException BadRequest(string code, string message) => new(StatusCodes.Status400BadRequest, code, message);
}

/// <summary>Stable error codes exposed in the "code" field of every error response.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string StudentIdMissing = "STUDENT_ID_MISSING";
    public const string StudentIdInvalid = "STUDENT_ID_INVALID";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string NoSeatsAvailable = "NO_SEATS_AVAILABLE";
    public const string DuplicateApplication = "DUPLICATE_APPLICATION";
    public const string NotHoldOwner = "NOT_HOLD_OWNER";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldNotActive = "HOLD_NOT_ACTIVE";
    public const string InternalError = "INTERNAL_ERROR";
}
