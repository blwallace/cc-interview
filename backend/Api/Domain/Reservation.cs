namespace Api.Domain;

/// <summary>
/// A resident's booking of an amenity for a time window.
///
/// TODO(candidate): This is a starting point, not a spec. Adjust the shape to fit your design
/// (e.g. status, cancellation, capacity handling, time zones). Explain your choices in the doc.
/// </summary>
public record Reservation
{
    public required Guid Id { get; init; }
    public required Guid AmenityId { get; init; }

    /// <summary>Identifies the resident who made the booking.</summary>
    public required string ResidentId { get; init; }

    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }
}
