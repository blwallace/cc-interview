# Amenity Reservations — Take-Home Exercise

A small, runnable starter. Your job is to **design and build a thin slice** of an amenity
reservation feature. This is a **design + AI-usage** exercise, time-boxed to **1–2 hours** —
we do **not** expect you to finish everything. See [`TASK.md`](./TASK.md) for the full brief and
what we grade.

The plumbing is already wired so you can spend your time on design and the feature, not setup:

- **Backend** — .NET 9 minimal API, in-memory store, OpenAPI on. Amenities endpoint works.
- **Frontend** — React + TypeScript + Vite, calling the API through an **orval-generated typed
  client**. One screen lists amenities.
- **Reservations is implemented** — see [`docs/DESIGN.md`](./docs/DESIGN.md) for the design and
  trade-offs. Slot-based availability, capacity-aware booking, and cancellation, with the
  double-booking mechanism covered in §3.

## Prerequisites

- [.NET SDK 9.0+](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org) and a package manager (`pnpm` recommended; `npm` works too)

## Run it (two terminals)

**1) Backend** → http://localhost:5080 (OpenAPI doc at `/openapi/v1.json`)

```bash
cd backend/Api
dotnet run
```

**2) Frontend** → http://localhost:5173

```bash
cd frontend
pnpm install      # or: npm install
pnpm dev          # or: npm run dev
```

Open http://localhost:5173 — you should see two seeded buildings' amenities, selectable from the
Building switcher. The Vite dev server proxies `/api/*` to the backend, so there's no CORS to deal
with.

> **pnpm 11+** blocks dependency build scripts by default. `pnpm-workspace.yaml` allows esbuild's
> (needed to link its platform binary) and leaves `@scarf/scarf` telemetry blocked. On npm, or on
> older pnpm, that file is inert and nothing extra is required.

**3) Tests** — 27 covering the booking rules, the concurrency guarantee, and the HTTP surface.

```bash
cd backend
dotnet test Api.Tests
```

## Regenerating the API client

The typed client lives in `frontend/src/api/generated/api.ts` and is generated from an OpenAPI
spec. A committed snapshot (`frontend/openapi.json`) is used by default so it works on a fresh clone.

```bash
cd frontend
pnpm gen:api      # regenerates the client from openapi.json
```

To pick up backend changes, refresh the snapshot from the running API first:

```bash
cd frontend
curl -s http://localhost:5080/openapi/v1.json -o openapi.json && pnpm gen:api
```

The client is generated as React Query hooks. All requests route through `src/api/fetcher.ts`, which
injects the `X-Tenant-Id` / `X-User-Id` identity headers in one place.

## Where to work

| You're doing... | Look at |
| --- | --- |
| Reservation endpoints | `backend/Api/Program.cs` |
| Booking rules & concurrency | `backend/Api/Services/ReservationService.cs` |
| Domain shape | `backend/Api/Domain/Reservation.cs`, `Amenity.cs` |
| Persistence | `backend/Api/Store/InMemoryStore.cs` |
| Building the UI | `frontend/src/App.tsx` |
| The typed client | `frontend/src/api/generated/api.ts` (generated) |

## Submitting

When you're done, **push your work to a branch and open a pull request against this repository.**
Include in the PR:

1. **A short design doc** (1–3 pages, Markdown is fine, committed in the repo) — see `TASK.md` for
   what to cover.
2. **2–3 sentences** in the PR description on what you'd do next with another 4 hours.
