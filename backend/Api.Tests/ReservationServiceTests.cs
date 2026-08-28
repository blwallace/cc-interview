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

    private static ReservationService NewService(out TestClock clock)
    {
        clock = new TestClock(Morning);
        return new ReservationService(new InMemoryStore(), clock);
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
}
