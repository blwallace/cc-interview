# Amenity Reservations — thin slice

Design doc: [`docs/DESIGN.md`](./DESIGN.md). The crux (double-booking) is §3.

Running end to end: React UI → generated client → .NET 9 minimal API → in-memory store.
**27 tests pass** (`dotnet test backend/Api.Tests`), `pnpm build` is clean.

```bash
cd backend/Api && dotnet run          # http://localhost:5080
cd frontend && pnpm install && pnpm dev   # http://localhost:5173
```

## What's here

- Per-slot capacity checking on a 30-minute grid, capacity-aware (gym holds 2, party room 1)
- Amenity-keyed lock making check-and-insert atomic
- Tenant scoping where the store exposes **no unscoped collection** for a handler to reach for
- `GET /api/amenities`, `GET|POST /api/amenities/{id}/reservations`, `DELETE /api/reservations/{id}`
- UI: availability grid, booking, cancel-your-own, building switcher, simulated-user switcher,
  and distinct feedback for 409 vs. 400 driven by a machine-readable `code`, not message text

## The two things I'd want reviewed

**1. Counting overlapping bookings is wrong when capacity > 1.** The natural rule — count bookings
overlapping the request, reject at capacity — over-rejects, because bookings that overlap *the
request* need not overlap *each other*. Gym at capacity 2 with `09:00–10:00` and `10:00–11:00`
booked: a request for `09:30–10:30` gets counted as 2 and refused, though occupancy never exceeds 2.

I implemented the naive version first specifically to watch it fail, then fixed it by evaluating
capacity per 30-minute slot, where the count *is* the peak concurrency. That's the main reason time
is discretized.

**2. The concurrency test passed before the lock existed.** 16 threads rushing one slot didn't
interleave — the critical section is ~a microsecond, shorter than thread wake-up jitter. Repeating
the scenario 500× made it fail every run: **all 8 racers booked a capacity-2 amenity.** I'd rather
flag this than quietly ship the passing version, because a one-shot test would have read as proof of
the most important claim in the design while proving nothing.

I also mutation-checked the endpoint tests (they went compile-error → green without a behavioural
red): breaking the 409 mapping and breaking tenant scoping each failed exactly the right test.

## Deliberately not built

- **EF Core + Postgres RLS is designed, not coded** (§4) — the brief says not to spend time on a
  database. The production concurrency answer (§3) is a `UNIQUE (amenity_id, slot_start, seat_index)`
  constraint that *retires* the in-process lock rather than distributing it.
- **Real auth.** `X-Tenant-Id` / `X-User-Id` are a mock with **no security value** — any caller can
  set them. They demonstrate isolation; they don't enforce it. The shape matches where a verified
  token's claims would land so the swap is contained.
- Opening hours, recurring bookings, payments, waitlists, manager overrides — reasoning in §1.

On Redis: I'd avoid Redlock for correctness (lock and data in separate systems). Redis is sound here
as the *atomic arbiter* — a Lua script that checks and increments slot counters in one step — sitting
in front of the database constraint, not replacing it. §3 has the script and the caveats.

## Next 4 hours

Persistence with the unique constraint that makes the lock unnecessary; tests for `frontend/src/slots.ts`,
which reimplements the server's occupancy rule to draw the grid and currently has none; then swapping
the header mock for real auth before more code accumulates assuming identity is free to assert.

## Notes for the reviewer

- Scaffold changes: added `BuildingId`/`TenantId` to the domain, replaced the store's public
  collections with tenant-scoped accessors, switched orval from the plain `fetch` client to
  `react-query` with a custom fetcher that injects identity headers in one place, and added
  `pnpm-workspace.yaml` (pnpm 11 blocks esbuild's postinstall by default; `@scarf/scarf` telemetry
  stays blocked).
- Times are UTC everywhere including the UI — a local grid misaligns 30-minute boundaries in `:45`
  offset zones. Called out in §1 A3 as a slice-level simplification.
- `docs/DESIGN.md` §6 records what was verified and how, including what the tests initially got wrong.
