import type { Reservation } from './api/generated/api';

/**
 * Slot arithmetic, mirroring the server's rules (docs/DESIGN.md §1 A2, §3).
 *
 * Everything here is UTC. The server validates alignment in UTC, and rendering a local grid would
 * silently misalign in zones with a :45 offset. Showing UTC is the honest simplification for a
 * slice; a real build converts for display and is explicit about the building's zone.
 */

export const SLOT_MINUTES = 30;
const DAY_START_HOUR = 6;
const DAY_END_HOUR = 22;

export function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/** All bookable slot start times for a given YYYY-MM-DD, as UTC instants. */
export function slotsForDate(dateIso: string): Date[] {
  const slots: Date[] = [];
  const perHour = 60 / SLOT_MINUTES;

  for (let hour = DAY_START_HOUR; hour < DAY_END_HOUR; hour++) {
    for (let n = 0; n < perHour; n++) {
      slots.push(new Date(`${dateIso}T${pad(hour)}:${pad(n * SLOT_MINUTES)}:00Z`));
    }
  }
  return slots;
}

export function addMinutes(instant: Date, minutes: number): Date {
  return new Date(instant.getTime() + minutes * 60_000);
}

/** "09:30" — UTC, matching how slots are built. */
export function timeLabel(instant: Date | string): string {
  const d = typeof instant === 'string' ? new Date(instant) : instant;
  return `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
}

/** "Aug 29" — UTC. The reservation list spans days, so entries must say which one. */
export function dateLabel(instant: Date | string): string {
  const d = typeof instant === 'string' ? new Date(instant) : instant;
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' });
}

/** YYYY-MM-DD in UTC, for comparing a reservation against the selected day. */
export function dateKey(instant: Date | string): string {
  const d = typeof instant === 'string' ? new Date(instant) : instant;
  return d.toISOString().slice(0, 10);
}

/**
 * How many existing bookings cover this slot. Same rule the server enforces: a booking covers a
 * slot when it starts before the slot ends and ends after the slot starts.
 */
export function occupancy(reservations: Reservation[], slotStart: Date): number {
  const slotEnd = addMinutes(slotStart, SLOT_MINUTES);

  return reservations.filter((r) => {
    const start = new Date(r.startUtc);
    const end = new Date(r.endUtc);
    return start < slotEnd && end > slotStart;
  }).length;
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
