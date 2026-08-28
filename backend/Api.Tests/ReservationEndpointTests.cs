using System.Net;
using System.Net.Http.Json;
using Api.Store;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests;

/// <summary>
/// Exercises the HTTP surface: tenant/user resolution from headers, and the mapping from service
/// results to status codes. The booking rules themselves are covered in ReservationServiceTests.
/// </summary>
public class ReservationEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// A fresh server — and therefore a fresh singleton store — so tests don't share bookings.
    /// Clients taken from the same server DO share one, which two-resident tests depend on.
    /// </summary>
    private WebApplicationFactory<Program> NewServer() => factory.WithWebHostBuilder(_ => { });

    private static HttpClient As(WebApplicationFactory<Program> server, string tenantId, string userId)
    {
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    /// <summary>Far-future so the "no booking in the past" rule never trips on the real clock.</summary>
    private static object Window(string start, string end) => new
    {
        startUtc = $"2099-09-01T{start}:00Z",
        endUtc = $"2099-09-01T{end}:00Z",
    };

    private static string Reservations(Guid amenityId) => $"/api/amenities/{amenityId}/reservations";

    [Fact]
    public async Task Creating_a_reservation_returns_201()
    {
        var client = As(NewServer(), SeedTenants.Building101, "resident-101");

        var response = await client.PostAsJsonAsync(Reservations(SeedIds.Gym), Window("09:00", "10:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Double_booking_an_exclusive_amenity_returns_409()
    {
        var server = NewServer();
        var first = As(server, SeedTenants.Building101, "resident-101");
        var second = As(server, SeedTenants.Building101, "resident-102");

        await first.PostAsJsonAsync(Reservations(SeedIds.PartyRoom), Window("09:00", "10:00"));
        var response = await second.PostAsJsonAsync(Reservations(SeedIds.PartyRoom), Window("09:00", "10:00"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_second_booking_by_the_same_resident_returns_400()
    {
        var client = As(NewServer(), SeedTenants.Building101, "resident-101");
        await client.PostAsJsonAsync(Reservations(SeedIds.Gym), Window("09:00", "10:00"));

        var response = await client.PostAsJsonAsync(Reservations(SeedIds.Gym), Window("14:00", "15:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Booking_another_buildings_amenity_returns_404()
    {
        // The Pool belongs to building-202.
        var client = As(NewServer(), SeedTenants.Building101, "resident-101");

        var response = await client.PostAsJsonAsync(Reservations(SeedIds.Pool), Window("09:00", "10:00"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_request_without_a_tenant_header_is_rejected()
    {
        var client = NewServer().CreateClient(); // no X-Tenant-Id

        var response = await client.GetAsync("/api/amenities");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Amenities_are_scoped_to_the_calling_building()
    {
        var client = As(NewServer(), SeedTenants.Building202, "resident-201");

        var amenities = await client.GetFromJsonAsync<List<AmenityDto>>("/api/amenities");

        Assert.NotNull(amenities);
        Assert.Equal(["Pool", "Rooftop Terrace"], amenities!.Select(a => a.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Listing_reservations_is_scoped_to_the_calling_building()
    {
        var server = NewServer();
        await As(server, SeedTenants.Building101, "resident-101")
            .PostAsJsonAsync(Reservations(SeedIds.Gym), Window("09:00", "10:00"));

        // building-202 asking about building-101's gym must not see it — and must not learn it exists.
        var outsider = As(server, SeedTenants.Building202, "resident-201");
        var response = await outsider.GetAsync(Reservations(SeedIds.Gym));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_your_own_reservation_returns_204()
    {
        var client = As(NewServer(), SeedTenants.Building101, "resident-101");
        var created = await client.PostAsJsonAsync(Reservations(SeedIds.Gym), Window("09:00", "10:00"));
        var booking = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var response = await client.DeleteAsync($"/api/reservations/{booking!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Cancelling_another_residents_reservation_returns_403()
    {
        var server = NewServer();
        var owner = As(server, SeedTenants.Building101, "resident-101");
        var created = await owner.PostAsJsonAsync(Reservations(SeedIds.Gym), Window("09:00", "10:00"));
        var booking = await created.Content.ReadFromJsonAsync<ReservationDto>();

        var interloper = As(server, SeedTenants.Building101, "resident-999");
        var response = await interloper.DeleteAsync($"/api/reservations/{booking!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record AmenityDto(Guid Id, string Name);
    private sealed record ReservationDto(Guid Id, Guid AmenityId, string UserId);
}
