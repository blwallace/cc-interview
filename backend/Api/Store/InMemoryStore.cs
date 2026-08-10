using Api.Domain;

namespace Api.Store;

/// <summary>
/// Tiny in-memory store so you don't have to stand up a database.
/// Registered as a singleton.
/// </summary>
public sealed class InMemoryStore
{
    public List<Amenity> Amenities { get; } = new()
    {
        new Amenity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Party Room",
            Description = "Bookable event space on the ground floor.",
            Capacity = 1,
            MaxBookingMinutes = 240,
        },
        new Amenity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Gym",
            Description = "Fitness room. Multiple residents at once.",
            Capacity = 2,
            MaxBookingMinutes = 90,
        },
        new Amenity
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Guest Parking",
            Description = "Single visitor parking spot.",
            Capacity = 1,
            MaxBookingMinutes = 1440,
        },
    };

    /// <summary>TODO(candidate): use this (or replace it) for reservation persistence.</summary>
    public List<Reservation> Reservations { get; } = new();
}
