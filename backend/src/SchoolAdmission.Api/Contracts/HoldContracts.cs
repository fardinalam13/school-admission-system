namespace SchoolAdmission.Api.Contracts;

/// <summary>201 body of POST /classes/{classId}/holds. Field names are fixed by the contract.</summary>
public sealed record HoldCreatedResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTime ExpiresAt);

/// <summary>
/// Body of GET /holds/{holdId} and each item of GET /me/holds. The contract requires holdId,
/// classId, status, expiresAt (and className for /me/holds); the remaining fields are permitted
/// extras that the UI uses.
/// </summary>
public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string StudentId,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);

/// <summary>200 body of POST /holds/{holdId}/confirm. Field names are fixed by the contract.</summary>
public sealed record HoldConfirmedResponse(string HoldId, string Status);
