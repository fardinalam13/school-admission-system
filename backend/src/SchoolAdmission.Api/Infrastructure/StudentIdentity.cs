using System.Text.RegularExpressions;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// The brief has no login. A student is identified solely by the X-Student-Id request header,
/// whose value is the 17-digit birth registration number.
/// </summary>
public static partial class StudentIdentity
{
    public const string HeaderName = "X-Student-Id";

    [GeneratedRegex(@"^\d{17}$")]
    private static partial Regex SeventeenDigits();

    /// <summary>
    /// Returns the validated student id or throws a 400 <see cref="ApiException"/>.
    /// Surrounding whitespace is tolerated because it is a common copy-paste artefact.
    /// </summary>
    public static string Require(HttpRequest request)
    {
        var raw = request.Headers[HeaderName].ToString().Trim();

        if (raw.Length == 0)
        {
            throw ApiException.BadRequest(ErrorCodes.StudentIdMissing,
                $"The {HeaderName} header is required. Enter the student's 17-digit birth registration number.");
        }

        if (!SeventeenDigits().IsMatch(raw))
        {
            throw ApiException.BadRequest(ErrorCodes.StudentIdInvalid,
                "The student id must be exactly 17 digits (birth registration number).");
        }

        return raw;
    }
}
