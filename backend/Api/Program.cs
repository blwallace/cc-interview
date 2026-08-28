using Api.Contracts;
using Api.Domain;
using Api.Services;
using Api.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<InMemoryStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReservationService>();

var app = builder.Build();

// OpenAPI is always on so the frontend can regenerate its typed client:
//   openapi json -> http://localhost:5080/openapi/v1.json
app.MapOpenApi();

// --- System ---------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("getHealth")
   .WithTags("System");

// --- Amenities ------------------------------------------------------------
// TenantContext binds from headers; a request without X-Tenant-Id is rejected with 400 before the
// handler runs, so there is no code path that reads unscoped data.
app.MapGet("/api/amenities", (InMemoryStore store, TenantContext ctx) =>
        Results.Ok(store.AmenitiesFor(ctx.TenantId)))
   .WithName("getAmenities")
   .WithTags("Amenities")
   .Produces<IReadOnlyList<Amenity>>();

// --- Reservations ---------------------------------------------------------
app.MapGet("/api/amenities/{amenityId:guid}/reservations",
        (Guid amenityId, InMemoryStore store, TenantContext ctx) =>
            store.FindAmenity(ctx.TenantId, amenityId) is null
                ? Results.NotFound()
                : Results.Ok(store.ReservationsFor(ctx.TenantId, amenityId)))
   .WithName("getAmenityReservations")
   .WithTags("Reservations")
   .Produces<IReadOnlyList<Reservation>>()
   .Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/amenities/{amenityId:guid}/reservations",
        (Guid amenityId, CreateReservationRequest body, ReservationService reservations, TenantContext ctx) =>
        {
            var result = reservations.TryCreateReservation(
                ctx.TenantId, amenityId, ctx.UserId, body.StartUtc, body.EndUtc);

            // Status codes are mapped here, not in the service — see docs/DESIGN.md §5.
            return result.Error switch
            {
                ReservationError.None =>
                    Results.Created($"/api/reservations/{result.Reservation!.Id}", result.Reservation),

                ReservationError.AmenityNotFound => Results.NotFound(),

                ReservationError.CapacityExceeded => Problem(
                    StatusCodes.Status409Conflict, "Slot fully booked",
                    "Every slot in this window is already at capacity.", result.Error),

                ReservationError.DuplicateResidentBooking => Problem(
                    StatusCodes.Status400BadRequest, "Already booked",
                    "You already have an active reservation for this amenity.", result.Error),

                ReservationError.NotSlotAligned => Problem(
                    StatusCodes.Status400BadRequest, "Invalid time slot",
                    $"Bookings must start and end on {ReservationService.SlotMinutes}-minute boundaries.",
                    result.Error),

                ReservationError.StartsInPast => Problem(
                    StatusCodes.Status400BadRequest, "Invalid time slot",
                    "That time is in the past.", result.Error),

                ReservationError.ExceedsMaxBookingLength => Problem(
                    StatusCodes.Status400BadRequest, "Booking too long",
                    "This booking is longer than the amenity allows.", result.Error),

                _ => Problem(
                    StatusCodes.Status400BadRequest, "Invalid time slot",
                    "The requested window is not valid.", result.Error),
            };
        })
   .WithName("createReservation")
   .WithTags("Reservations")
   .Produces<Reservation>(StatusCodes.Status201Created)
   .ProducesProblem(StatusCodes.Status400BadRequest)
   .Produces(StatusCodes.Status404NotFound)
   .ProducesProblem(StatusCodes.Status409Conflict);

app.MapDelete("/api/reservations/{reservationId:guid}",
        (Guid reservationId, ReservationService reservations, TenantContext ctx) =>
            reservations.TryCancel(ctx.TenantId, reservationId, ctx.UserId) switch
            {
                CancelError.None => Results.NoContent(),
                CancelError.NotFound => Results.NotFound(),
                CancelError.NotOwner => Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Not your reservation",
                    detail: "Only the resident who made a booking can cancel it."),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            })
   .WithName("cancelReservation")
   .WithTags("Reservations")
   .Produces(StatusCodes.Status204NoContent)
   .Produces(StatusCodes.Status404NotFound)
   .ProducesProblem(StatusCodes.Status403Forbidden);

app.Run();

// RFC 7807 problem responses carry a machine-readable `code` so the UI can branch on the reason
// rather than string-matching the message.
static IResult Problem(int status, string title, string detail, ReservationError code) =>
    Results.Problem(
        statusCode: status,
        title: title,
        detail: detail,
        extensions: new Dictionary<string, object?> { ["code"] = code.ToString() });

/// <summary>Exposed so WebApplicationFactory&lt;Program&gt; can host the app in tests.</summary>
public partial class Program;
