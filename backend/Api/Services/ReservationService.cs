using Api.Domain;
using Api.Store;

namespace Api.Services;

/// <summary>Why a reservation request was refused. Mapped to HTTP at the endpoint, not here.</summary>
public enum ReservationError
{
    None = 0,
    AmenityNotFound,
    NotSlotAligned,
    InvalidRange,
    StartsInPast,
    ExceedsMaxBookingLength,
    DuplicateResidentBooking,
    CapacityExceeded,
}

public readonly record struct CreateReservationResult(Reservation? Reservation, ReservationError Error);

public sealed class ReservationService(InMemoryStore store, TimeProvider clock)
{
    /// <summary>Bookings are aligned to this grid; capacity is evaluated per slot (DESIGN.md §1, A2).</summary>
    public const int SlotMinutes = 30;

    public CreateReservationResult TryCreateReservation(
        string tenantId, Guid amenityId, string userId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        // Scoped lookup: an amenity in another tenant is indistinguishable from one that does not
        // exist, so cross-tenant probing cannot confirm existence (DESIGN.md §5).
        var amenity = store.FindAmenity(tenantId, amenityId);
        if (amenity is null) return new CreateReservationResult(null, ReservationError.AmenityNotFound);

        var existing = store.ReservationsFor(tenantId, amenityId);

        // Capacity is evaluated per slot, not per request. Counting the bookings that overlap the
        // request overestimates peak occupancy, because those bookings need not overlap each other
        // — it refuses legal bookings. Within one slot every covering booking is concurrent for the
        // whole slot, so the count is exact. See DESIGN.md §3.
        for (var slot = startUtc; slot < endUtc; slot = slot.AddMinutes(SlotMinutes))
        {
            var slotEnd = slot.AddMinutes(SlotMinutes);
            var occupancy = existing.Count(r => r.StartUtc < slotEnd && r.EndUtc > slot);

            if (occupancy >= amenity.Capacity)
                return new CreateReservationResult(null, ReservationError.CapacityExceeded);
        }

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            AmenityId = amenityId,
            TenantId = tenantId,
            UserId = userId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAt = clock.GetUtcNow(),
        };

        store.Add(reservation);
        return new CreateReservationResult(reservation, ReservationError.None);
    }
}
