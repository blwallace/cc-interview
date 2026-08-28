using Api.Services;
using Api.Store;

namespace Api.Tests;

public class ReservationServiceTests
{
    private const string Building101 = "building-101";
    private const string ResidentA = "resident-101";
    private const string ResidentB = "resident-102";
    private const string ResidentC = "resident-103";

    /// <summary>Every test books on 2026-09-01; the clock sits at 08:00 that morning.</summary>
    private static readonly DateTimeOffset Morning = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>"09:30" -> 2026-09-01T09:30:00Z. Keeps the booking windows readable.</summary>
    private static DateTimeOffset At(string hhmm)
    {
        var parts = hhmm.Split(':');
        return new DateTimeOffset(2026, 9, 1, int.Parse(parts[0]), int.Parse(parts[1]), 0, TimeSpan.Zero);
    }

    private static ReservationService NewService(out TestClock clock) => NewService(out clock, out _);

    private static ReservationService NewService(out TestClock clock, out InMemoryStore store)
    {
        clock = new TestClock(Morning);
        store = new InMemoryStore();
        return new ReservationService(store, clock);
    }

    [Fact]
    public void Booking_an_open_slot_succeeds()
    {
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.None, result.Error);
        Assert.NotNull(result.Reservation);
        Assert.Equal(ResidentA, result.Reservation!.UserId);
        Assert.Equal(SeedIds.Gym, result.Reservation.AmenityId);
    }

    [Fact]
    public void Booking_a_slot_that_is_already_at_capacity_is_refused()
    {
        // Party Room has capacity 1, so the second booking of the same window has nowhere to go.
        var service = NewService(out _);
        service.TryCreateReservation(Building101, SeedIds.PartyRoom, ResidentA, At("09:00"), At("10:00"));

        var result = service.TryCreateReservation(
            Building101, SeedIds.PartyRoom, ResidentB, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.CapacityExceeded, result.Error);
        Assert.Null(result.Reservation);
    }

    [Fact]
    public void Booking_is_allowed_when_overlapping_bookings_do_not_overlap_each_other()
    {
        // Gym, capacity 2:
        //   A  09:00 ──── 10:00
        //   B            10:00 ──── 11:00
        //   C     09:30 ────── 10:30   <- overlaps both A and B, but occupancy never exceeds 2
        //
        // Counting bookings that overlap the *request* gives 2 and wrongly refuses C. Counting
        // per 30-minute slot gives 2 in each slot and correctly allows it. See DESIGN.md §3.
        var service = NewService(out _);
        service.TryCreateReservation(Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00"));
        service.TryCreateReservation(Building101, SeedIds.Gym, ResidentB, At("10:00"), At("11:00"));

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentC, At("09:30"), At("10:30"));

        Assert.Equal(ReservationError.None, result.Error);
    }

    [Fact]
    public void Booking_an_amenity_that_does_not_exist_is_refused()
    {
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, Guid.NewGuid(), ResidentA, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.AmenityNotFound, result.Error);
    }

    [Fact]
    public void Booking_an_amenity_belonging_to_another_building_is_refused_as_not_found()
    {
        // The Pool is building-202's. A caller scoped to building-101 must not be able to book it,
        // and must not learn that it exists — hence NotFound rather than a distinct error.
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Pool, ResidentA, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.AmenityNotFound, result.Error);
    }

    [Theory]
    [InlineData("09:15", "10:15")] // start off the 30-minute grid
    [InlineData("09:00", "10:20")] // end off the 30-minute grid
    public void Booking_that_is_not_slot_aligned_is_refused(string start, string end)
    {
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At(start), At(end));

        Assert.Equal(ReservationError.NotSlotAligned, result.Error);
    }

    [Fact]
    public void Booking_that_ends_before_it_starts_is_refused()
    {
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("10:00"), At("09:00"));

        Assert.Equal(ReservationError.InvalidRange, result.Error);
    }

    [Fact]
    public void Booking_a_window_that_has_already_started_is_refused()
    {
        // Clock is at 08:00; 07:00 is in the past.
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("07:00"), At("08:00"));

        Assert.Equal(ReservationError.StartsInPast, result.Error);
    }

    [Fact]
    public void Booking_longer_than_the_amenity_allows_is_refused()
    {
        // Gym allows 90 minutes; this asks for 120.
        var service = NewService(out _);

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("09:00"), At("11:00"));

        Assert.Equal(ReservationError.ExceedsMaxBookingLength, result.Error);
    }

    [Fact]
    public void A_resident_may_not_hold_two_active_bookings_for_the_same_amenity()
    {
        var service = NewService(out _);
        service.TryCreateReservation(Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00"));

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("14:00"), At("15:00"));

        Assert.Equal(ReservationError.DuplicateResidentBooking, result.Error);
    }

    [Fact]
    public void A_resident_may_book_again_once_their_previous_booking_has_ended()
    {
        // Pins the definition of "active": EndUtc > now. See DESIGN.md §2.
        var service = NewService(out var clock);
        service.TryCreateReservation(Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00"));

        clock.Now = At("10:30");

        var result = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("11:00"), At("12:00"));

        Assert.Equal(ReservationError.None, result.Error);
    }

    [Fact]
    public void A_resident_may_hold_bookings_for_two_different_amenities()
    {
        // The limit is per amenity, not global.
        var service = NewService(out _);
        service.TryCreateReservation(Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00"));

        var result = service.TryCreateReservation(
            Building101, SeedIds.PartyRoom, ResidentA, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.None, result.Error);
    }

    [Fact]
    public void Concurrent_bookings_of_one_slot_never_exceed_capacity()
    {
        // The load-bearing test. Gym capacity is 2; 16 residents rush the same window at once.
        // Without an atomic check-and-insert, several threads all read "1 < 2" before any of them
        // writes, and the slot ends up overbooked. See DESIGN.md §3.
        // The critical section is ~a microsecond, far shorter than thread wake-up jitter, so a
        // single rush rarely interleaves. Repeating the whole scenario makes the race reproducible
        // without putting timing hooks in production code.
        const int Racers = 8;
        const int Trials = 500;

        for (var trial = 0; trial < Trials; trial++)
        {
            var service = NewService(out _, out var store);
            var errors = new System.Collections.Concurrent.ConcurrentBag<ReservationError>();
            using var startGate = new ManualResetEventSlim(false);

            var threads = Enumerable.Range(0, Racers)
                .Select(i => new Thread(() =>
                {
                    startGate.Wait();
                    errors.Add(service.TryCreateReservation(
                        Building101, SeedIds.Gym, $"resident-{i}", At("09:00"), At("10:00")).Error);
                }))
                .ToList();

            foreach (var t in threads) t.Start();
            startGate.Set(); // release them as close to simultaneously as the scheduler allows
            foreach (var t in threads) t.Join();

            // The invariant that actually matters: the store never holds more than capacity.
            Assert.Equal(2, store.ReservationsFor(Building101, SeedIds.Gym).Count);
            Assert.Equal(2, errors.Count(e => e == ReservationError.None));
            Assert.Equal(Racers - 2, errors.Count(e => e == ReservationError.CapacityExceeded));
        }
    }

    [Fact]
    public void A_resident_can_cancel_their_own_booking()
    {
        var service = NewService(out _);
        var booking = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00")).Reservation!;

        var error = service.TryCancel(Building101, booking.Id, ResidentA);

        Assert.Equal(CancelError.None, error);
    }

    [Fact]
    public void Cancelling_frees_the_slot_for_someone_else()
    {
        // Party Room, capacity 1: booked, cancelled, then rebooked by another resident.
        var service = NewService(out _);
        var booking = service.TryCreateReservation(
            Building101, SeedIds.PartyRoom, ResidentA, At("09:00"), At("10:00")).Reservation!;
        service.TryCancel(Building101, booking.Id, ResidentA);

        var result = service.TryCreateReservation(
            Building101, SeedIds.PartyRoom, ResidentB, At("09:00"), At("10:00"));

        Assert.Equal(ReservationError.None, result.Error);
    }

    [Fact]
    public void A_resident_cannot_cancel_someone_elses_booking()
    {
        var service = NewService(out _, out var store);
        var booking = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00")).Reservation!;

        var error = service.TryCancel(Building101, booking.Id, ResidentB);

        Assert.Equal(CancelError.NotOwner, error);
        Assert.Single(store.ReservationsFor(Building101, SeedIds.Gym));
    }

    [Fact]
    public void Cancelling_a_booking_in_another_building_reports_not_found_not_forbidden()
    {
        // Existence must not leak across the tenant boundary, so this is NotFound even though the
        // reservation is real. Ownership is only considered once tenancy checks out (DESIGN.md §5).
        var service = NewService(out _);
        var booking = service.TryCreateReservation(
            Building101, SeedIds.Gym, ResidentA, At("09:00"), At("10:00")).Reservation!;

        var error = service.TryCancel(SeedTenants.Building202, booking.Id, ResidentA);

        Assert.Equal(CancelError.NotFound, error);
    }
}
