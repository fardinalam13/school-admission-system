using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.Contracts;

/// <summary>Body of POST /classes.</summary>
public sealed class CreateClassRequest
{
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Class name must be between 1 and 200 characters.")]
    public string? Name { get; init; }

    [Required(ErrorMessage = "totalSeats is required.")]
    [Range(1, 100_000, ErrorMessage = "totalSeats must be a whole number between 1 and 100000.")]
    public int? TotalSeats { get; init; }
}

/// <summary>201 body of POST /classes. Field names are fixed by the assignment contract.</summary>
public sealed record ClassCreatedResponse(string Id, string Name, int TotalSeats);

/// <summary>Body of GET /classes items and GET /classes/{id}. Field names are fixed by the contract.</summary>
public sealed record ClassSeatsResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);
