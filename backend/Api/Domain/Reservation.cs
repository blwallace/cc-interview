namespace Api.Domain;

/// <summary>
/// A resident's booking of an amenity for a slot-aligned time window.
/// Times are UTC; see docs/DESIGN.md §1 (A2, A3).
/// </summary>
public record Reservation
{
    public required Guid Id { get; init; }
    public required Guid AmenityId { get; init; }

    /// <summary>
    /// The owning building. Denormalized from the amenity so authorization does not need a join —
    /// see docs/DESIGN.md §2. Never accepted from the client.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>Identifies the resident who made the booking.</summary>
    public required string UserId { get; init; }

    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
