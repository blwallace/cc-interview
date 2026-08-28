namespace Api.Domain;

/// <summary>
/// A shared amenity in a building that residents can reserve (gym, party room, guest parking, ...).
/// </summary>
public record Amenity
{
    public required Guid Id { get; init; }

    /// <summary>The owning building. An amenity belongs to exactly one tenant (DESIGN.md §1, A6).</summary>
    public required string TenantId { get; init; }

    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>How many concurrent reservations a single time slot can hold. 1 == exclusive.</summary>
    public int Capacity { get; init; } = 1;

    /// <summary>Upper bound on a single booking length, in minutes.</summary>
    public int MaxBookingMinutes { get; init; } = 120;
}
