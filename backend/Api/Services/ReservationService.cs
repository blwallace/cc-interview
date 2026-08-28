using System.Collections.Concurrent;
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

    private static readonly long SlotTicks = TimeSpan.FromMinutes(SlotMinutes).Ticks;

    /// <summary>One gate per amenity — the real contention domain, since conflicts never cross amenities.</summary>
    private readonly ConcurrentDictionary<Guid, object> _amenityGates = new();

    /// <summary>Normalizes to UTC first, so an offset time lands on the grid only if it truly does.</summary>
    private static bool IsSlotAligned(DateTimeOffset instant) => instant.UtcDateTime.Ticks % SlotTicks == 0;

    public CreateReservationResult TryCreateReservation(
        string tenantId, Guid amenityId, string userId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        // Scoped lookup: an amenity in another tenant is indistinguishable from one that does not
        // exist, so cross-tenant probing cannot confirm existence (DESIGN.md §5).
        var amenity = store.FindAmenity(tenantId, amenityId);
        if (amenity is null) return new CreateReservationResult(null, ReservationError.AmenityNotFound);

        if (!IsSlotAligned(startUtc) || !IsSlotAligned(endUtc))
            return new CreateReservationResult(null, ReservationError.NotSlotAligned);

        if (endUtc <= startUtc)
            return new CreateReservationResult(null, ReservationError.InvalidRange);

        if (startUtc < clock.GetUtcNow())
            return new CreateReservationResult(null, ReservationError.StartsInPast);

        if ((endUtc - startUtc).TotalMinutes > amenity.MaxBookingMinutes)
            return new CreateReservationResult(null, ReservationError.ExceedsMaxBookingLength);

        // Everything above is cheap and side-effect free, so it stays outside the gate to keep the
        // hold short. Note the amenity lookup precedes GetOrAdd deliberately: creating a gate for an
        // unvalidated id would let anyone grow this dictionary by posting random GUIDs.
        var gate = _amenityGates.GetOrAdd(amenityId, _ => new object());

        // Check-and-insert must be one atomic step. A thread-safe collection alone is not enough —
        // it makes each operation safe while leaving the *sequence* interleavable, which is the bug.
        // Keyed per amenity because conflicts only exist within one amenity; only one gate is ever
        // held at a time and they are never nested, so deadlock is unreachable. (DESIGN.md §3)
        lock (gate)
        {
            var existing = store.ReservationsFor(tenantId, amenityId);

            // Read-then-write, exactly like the capacity check below — it races the same way and so
            // must sit inside this section, despite reading like ordinary validation.
            if (existing.Any(r => r.UserId == userId && r.EndUtc > clock.GetUtcNow()))
                return new CreateReservationResult(null, ReservationError.DuplicateResidentBooking);

            // Capacity is evaluated per slot, not per request. Counting the bookings that overlap
            // the request overestimates peak occupancy, because those bookings need not overlap each
            // other — it refuses legal bookings. Within one slot every covering booking is
            // concurrent for the whole slot, so the count is exact. See DESIGN.md §3.
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
}
