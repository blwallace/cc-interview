# Take-Home Design Exercise — Senior Engineer

## Why this exists

We build a modular property-management platform (React + TypeScript frontend, .NET backend).
You'll routinely design features in parts of the stack you haven't touched before, leaning on AI
to ramp fast. **This exercise is about your design thinking and how you use AI to move quickly in
an unfamiliar stack — not about finishing a polished feature.**

**Time box: 1–2 hours. We do not expect you to complete everything.** A big part of the signal is
*what you choose to cut, and why.*

## Using AI

Using AI (Claude, Copilot, ChatGPT, Cursor, etc.) is **expected and encouraged** — it's how we
work. We're not looking for zero AI; we're interested in how you use it. We'll talk through your
approach — including where AI helped and where it steered you wrong — during the interview, so
there's nothing to submit on this front.

## The problem

Design and partially build **Amenity Reservations**.

Residents in a building can reserve a shared amenity (gym, party room, guest parking) for a time
slot. The requirements are intentionally a little underspecified — **state your assumptions**:

- A resident can view an amenity's availability and book an open time slot.
- The same slot for the same amenity **cannot be double-booked** (respecting capacity — some
  amenities allow more than one booking at once).
- A resident can cancel their own reservation.
- Amenities may have rules (opening hours, max booking length, one active booking per resident).
  **You decide** what's in scope for your slice vs. deferred — and say why.

## Deliverables

### 1. Design doc (primary) — 1–3 pages, Markdown

- Assumptions you made and questions you'd ask a PM.
- Data model (entities + the key relationships/constraints).
- API surface (endpoints, request/response shapes).
- **How you prevent double-booking** — name the failure mode, your chosen mechanism, and one
  alternative you rejected. This is the crux.
- Multi-tenancy / isolation: how one building's data stays separate from another's.
- What you deliberately left out, and why.

### 2. A running end-to-end slice (required)

Starting from this scaffold, deliver at least:

- **one working endpoint** (create *or* list reservations), and
- **one React screen** that calls it through the generated client, running locally.

It's fine if it's ugly, partial, or full of TODOs. **A slice that runs beats a feature that
doesn't.** If you can't get it running, include your best explanation of what's broken — we still
grade the reasoning.

### 3. Next steps

2–3 sentences on what you'd do with another 4 hours.

## What we're evaluating

| Dimension | What strong looks like |
| --- | --- |
| **Problem framing** | Scopes ruthlessly, states assumptions, asks the right PM questions |
| **Design & trade-offs** | Names the double-booking failure mode; weighs ≥2 options with rationale; considers tenancy & time |
| **Verification** | Sanity-checks their own work; reads/tests to confirm it behaves as intended |
| **Communication** | A teammate could pick up the design and build it |
| **Works end-to-end** | The slice runs; data flows UI → generated client → endpoint → store and back |
| **Judgment on scope** | Cut the right things given the time box, and said why |

**How you used AI** to work through this is a major part of what we're interested in — where it
helped, where it steered you wrong, and how you recognized and corrected that. There's nothing to
submit for it; it'll be a central thread of the follow-up conversation, so come ready to walk us
through your process.

## Notes

- You may change anything in the scaffold — tell us what you changed.
- In-memory persistence is expected; **do not** spend time on a real database.
- If a specific tool fights you, swap it and note the substitution. Don't get blocked by setup.

## Submitting

When you're done, **push your work to a branch and open a pull request against this repository.**
Commit your design doc alongside the code, and use the PR description for the "next steps" note and
anything you'd want a reviewer to know.
