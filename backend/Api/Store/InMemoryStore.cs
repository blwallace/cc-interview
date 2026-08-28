using System.Collections.Concurrent;
using Api.Domain;

namespace Api.Store;

/// <summary>Stable identifiers for the seeded amenities.</summary>
public static class SeedIds
{
    public static readonly Guid PartyRoom = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Gym = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GuestParking = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid RooftopTerrace = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Pool = Guid.Parse("55555555-5555-5555-5555-555555555555");
}

/// <summary>Well-known tenants. Two of them, so isolation is demonstrable (DESIGN.md §2).</summary>
public static class SeedTenants
{
    public const string Building101 = "building-101";
    public const string Building202 = "building-202";
}

/// <summary>
/// Tiny in-memory store so we don't have to stand up a database.
/// Registered as a singleton.
/// </summary>
public sealed class InMemoryStore
{
    private readonly List<Amenity> _amenities =
    [
        new Amenity
        {
            Id = SeedIds.PartyRoom,
            TenantId = SeedTenants.Building101,
            Name = "Party Room",
            Description = "Bookable event space on the ground floor.",
            Capacity = 1,
            MaxBookingMinutes = 240,
        },
        new Amenity
        {
            Id = SeedIds.Gym,
            TenantId = SeedTenants.Building101,
            Name = "Gym",
            Description = "Fitness room. Multiple residents at once.",
            Capacity = 2,
            MaxBookingMinutes = 90,
        },
        new Amenity
        {
            Id = SeedIds.GuestParking,
            TenantId = SeedTenants.Building101,
            Name = "Guest Parking",
            Description = "Single visitor parking spot.",
            Capacity = 1,
            MaxBookingMinutes = 1440,
        },
        new Amenity
        {
            Id = SeedIds.RooftopTerrace,
            TenantId = SeedTenants.Building202,
            Name = "Rooftop Terrace",
            Description = "Shared terrace with grills.",
            Capacity = 4,
            MaxBookingMinutes = 120,
        },
        new Amenity
        {
            Id = SeedIds.Pool,
            TenantId = SeedTenants.Building202,
            Name = "Pool",
            Description = "Indoor lap pool.",
            Capacity = 3,
            MaxBookingMinutes = 60,
        },
    ];

    private readonly ConcurrentDictionary<Guid, Reservation> _reservations = new();

    // Only tenant-scoped accessors are exposed. There is deliberately no unscoped `Amenities`
    // property for a handler to reach for — see docs/DESIGN.md §4.

    public IReadOnlyList<Amenity> AmenitiesFor(string tenantId) =>
        _amenities.Where(a => a.TenantId == tenantId).ToList();

    public Amenity? FindAmenity(string tenantId, Guid amenityId) =>
        _amenities.FirstOrDefault(a => a.Id == amenityId && a.TenantId == tenantId);

    public IReadOnlyList<Reservation> ReservationsFor(string tenantId, Guid amenityId) =>
        _reservations.Values
            .Where(r => r.TenantId == tenantId && r.AmenityId == amenityId)
            .OrderBy(r => r.StartUtc)
            .ToList();

    public Reservation? FindReservation(string tenantId, Guid reservationId) =>
        _reservations.TryGetValue(reservationId, out var r) && r.TenantId == tenantId ? r : null;

    public void Add(Reservation reservation) => _reservations[reservation.Id] = reservation;

    public bool Remove(Guid reservationId) => _reservations.TryRemove(reservationId, out _);
}
