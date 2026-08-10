using Api.Domain;
using Api.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<InMemoryStore>();

var app = builder.Build();

// OpenAPI is always on so the frontend can regenerate its typed client:
//   openapi json -> http://localhost:5080/openapi/v1.json
app.MapOpenApi();

// --- System ---------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("getHealth")
   .WithTags("System");

// --- Amenities (worked example — read this, then do the same for reservations) ---
app.MapGet("/api/amenities", (InMemoryStore store) => Results.Ok(store.Amenities))
   .WithName("getAmenities")
   .WithTags("Amenities")
   .Produces<IReadOnlyList<Amenity>>();

// --- Reservations (YOUR TASK) ---------------------------------------------
// TODO(candidate): design and implement the reservation endpoints.
// A reasonable starting set (change it to fit your design):
//   GET    /api/amenities/{amenityId}/reservations   -> list bookings for an amenity
//   POST   /api/reservations                          -> create a booking (must not double-book)
//   DELETE /api/reservations/{id}                     -> cancel a booking
//
// Explain your design decisions in the design doc — including the ones you don't implement.

app.Run();
