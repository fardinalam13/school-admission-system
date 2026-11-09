namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One admissible unit with its own seat quota. Per the brief every shift is modelled as its own
/// class, e.g. "Class 6 - Morning Shift" and "Class 6 - Day Shift" are two separate rows.
/// </summary>
public sealed class SchoolClass
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<SeatHold> Holds { get; set; } = new List<SeatHold>();
}
