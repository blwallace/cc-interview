import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  getGetAmenityReservationsQueryKey,
  useCancelReservation,
  useCreateReservation,
  useGetAmenityReservations,
  type Amenity,
} from './api/generated/api';
import { ApiError } from './api/fetcher';
import { useIdentity } from './identity';
import { useToast } from './toasts';
import { SLOT_MINUTES, addMinutes, dateKey, dateLabel, occupancy, slotsForDate, timeLabel } from './slots';

export function AmenityBookingCard({ amenity, date }: { amenity: Amenity; date: string }) {
  const { userId } = useIdentity();
  const toast = useToast();
  const queryClient = useQueryClient();

  const capacity = amenity.capacity ?? 1;
  const maxMinutes = amenity.maxBookingMinutes ?? 120;

  const [startSlot, setStartSlot] = useState<Date | null>(null);
  const [endSlot, setEndSlot] = useState<Date | null>(null); // exclusive end

  const reservationsKey = getGetAmenityReservationsQueryKey(amenity.id);
  const { data: reservations = [], isLoading } = useGetAmenityReservations(amenity.id);

  const refresh = () => queryClient.invalidateQueries({ queryKey: reservationsKey });
  const clearSelection = () => {
    setStartSlot(null);
    setEndSlot(null);
  };

  const createReservation = useCreateReservation({
    mutation: {
      onSuccess: () => {
        toast({ kind: 'success', title: 'Reservation confirmed' });
        clearSelection();
        refresh();
      },
      onError: (error) => {
        // The server's machine-readable `code` drives this, not the message text.
        if (error instanceof ApiError && error.status === 409) {
          toast({ kind: 'error', title: 'Slot fully booked!', body: error.message });
        } else if (error instanceof ApiError && error.code === 'DuplicateResidentBooking') {
          toast({ kind: 'warn', title: 'You already have an active reservation.', body: error.message });
        } else if (error instanceof ApiError) {
          toast({ kind: 'warn', title: "That booking isn't allowed", body: error.message });
        } else {
          toast({ kind: 'error', title: 'Something went wrong' });
        }
        refresh();
      },
    },
  });

  const cancelReservation = useCancelReservation({
    mutation: {
      onSuccess: () => {
        toast({ kind: 'success', title: 'Reservation cancelled' });
        refresh();
      },
      onError: (error) => {
        toast({
          kind: 'error',
          title: 'Could not cancel',
          body: error instanceof ApiError ? error.message : undefined,
        });
        refresh();
      },
    },
  });

  const slots = slotsForDate(date);

  function selectSlot(slot: Date) {
    // First click sets a single slot. A later click extends the window; clicking at or before the
    // start resets, which is simpler to reason about than trying to guess intent.
    if (!startSlot || slot <= startSlot) {
      setStartSlot(slot);
      setEndSlot(addMinutes(slot, SLOT_MINUTES));
      return;
    }
    setEndSlot(addMinutes(slot, SLOT_MINUTES));
  }

  const selectedMinutes =
    startSlot && endSlot ? (endSlot.getTime() - startSlot.getTime()) / 60_000 : 0;
  const tooLong = selectedMinutes > maxMinutes;

  const myActiveBooking = reservations.find(
    (r) => r.userId === userId && new Date(r.endUtc) > new Date(),
  );

  return (
    <section className="card">
      <div className="card-head">
        <div>
          <h3 className="card-title">{amenity.name}</h3>
          {amenity.description && <p className="card-desc">{amenity.description}</p>}
        </div>
        <div className="badges">
          <span className="badge badge-brand">
            {capacity === 1 ? 'Exclusive' : `Capacity ${capacity}`}
          </span>
          <span className="badge">Max {maxMinutes} min</span>
        </div>
      </div>

      <div className="card-body">
        <div className="section-label">Availability — {date} (UTC)</div>

        {isLoading ? (
          <p className="empty">Loading availability…</p>
        ) : (
          <div className="slots">
            {slots.map((slot) => {
              const taken = occupancy(reservations, slot);
              const free = capacity - taken;
              // The server rejects past bookings; offering them as clickable only teaches the user
              // that by refusing them.
              const isPast = slot < new Date();
              const isStart = startSlot?.getTime() === slot.getTime();
              const inRange =
                !!startSlot && !!endSlot && slot >= startSlot && slot < endSlot && !isStart;

              return (
                <button
                  key={slot.toISOString()}
                  type="button"
                  className={`slot${isStart ? ' is-selected' : ''}${inRange ? ' is-in-range' : ''}`}
                  disabled={free <= 0 || isPast}
                  onClick={() => selectSlot(slot)}
                  title={
                    isPast ? 'Already passed' : free > 0 ? `${free} of ${capacity} available` : 'Fully booked'
                  }
                >
                  <span>{timeLabel(slot)}</span>
                  <span className="slot-free">
                    {isPast ? 'past' : free > 0 ? `${free} free` : 'full'}
                  </span>
                </button>
              );
            })}
          </div>
        )}

        <div className="row">
          <div>
            {startSlot && endSlot ? (
              <>
                Selected <strong>{timeLabel(startSlot)}–{timeLabel(endSlot)}</strong> ({selectedMinutes} min)
                {tooLong && (
                  <span style={{ color: 'var(--err-ink)' }}> — over the {maxMinutes} min limit</span>
                )}
              </>
            ) : (
              <span className="empty">Pick a start time, then a later slot to extend.</span>
            )}
          </div>

          <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
            {startSlot && (
              <button type="button" className="btn btn-ghost" onClick={clearSelection}>
                Clear
              </button>
            )}
            <button
              type="button"
              className="btn btn-primary"
              disabled={!startSlot || !endSlot || tooLong || createReservation.isPending}
              onClick={() =>
                startSlot &&
                endSlot &&
                createReservation.mutate({
                  amenityId: amenity.id,
                  data: { startUtc: startSlot.toISOString(), endUtc: endSlot.toISOString() },
                })
              }
            >
              {createReservation.isPending ? 'Booking…' : 'Book'}
            </button>
          </div>
        </div>

        {myActiveBooking && (
          <p className="empty" style={{ marginTop: 0 }}>
            You already hold an active booking here — cancel it before booking again.
          </p>
        )}

        <div className="section-label">Reservations</div>
        {reservations.length === 0 ? (
          <p className="empty">No bookings yet.</p>
        ) : (
          <ul className="res-list">
            {reservations.map((r) => {
              const mine = r.userId === userId;
              const otherDay = dateKey(r.startUtc) !== date;
              return (
                <li key={r.id} className="res-item">
                  <span className="res-when">
                    {timeLabel(r.startUtc)}–{timeLabel(r.endUtc)}
                  </span>
                  <span className={otherDay ? 'badge' : 'res-who'}>{dateLabel(r.startUtc)}</span>
                  <span className="res-who">{r.userId}</span>
                  {mine && <span className="res-mine">You</span>}
                  <span className="res-actions">
                    {mine && (
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        disabled={cancelReservation.isPending}
                        onClick={() => cancelReservation.mutate({ reservationId: r.id })}
                      >
                        Cancel
                      </button>
                    )}
                  </span>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </section>
  );
}
