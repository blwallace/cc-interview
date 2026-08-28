import { useState } from 'react';
import { useGetAmenities } from './api/generated/api';
import { AmenityBookingCard } from './AmenityBookingCard';
import { BUILDINGS, USERS, useIdentity } from './identity';
import { todayIso } from './slots';

export function App() {
  const { tenantId, userId, setTenantId, setUserId } = useIdentity();
  const [date, setDate] = useState(todayIso);

  const { data: amenities = [], isLoading, error } = useGetAmenities();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">CC</div>
          <div>
            <div className="brand-name">Condo Control</div>
            <div className="brand-sub">Property Management</div>
          </div>
        </div>

        <nav className="nav">
          <button className="nav-item is-active">
            <span className="nav-icon">▦</span> Amenities
          </button>
          {['Dashboard', 'Residents', 'Packages', 'Requests', 'Announcements'].map((item) => (
            <button key={item} className="nav-item is-muted" disabled>
              <span className="nav-icon">•</span> {item}
            </button>
          ))}
        </nav>

        <div className="sidebar-foot">
          Amenity Reservations — take-home slice. Only Amenities is implemented.
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <h1 className="topbar-title">Amenity Reservations</h1>
          <div className="topbar-spacer" />

          <div className="switcher">
            <label htmlFor="building">Building</label>
            <select id="building" value={tenantId} onChange={(e) => setTenantId(e.target.value)}>
              {BUILDINGS.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </div>

          <div className="switcher">
            <label htmlFor="user">Simulated user</label>
            <select id="user" value={userId} onChange={(e) => setUserId(e.target.value)}>
              {USERS.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.label} ({u.id})
                </option>
              ))}
            </select>
          </div>
        </header>

        <div className="mock-note">
          Identity is mocked: building and user are sent as <code>X-Tenant-Id</code> /{' '}
          <code>X-User-Id</code> headers that any caller could set. They demonstrate isolation — they
          do not enforce it. In production both come from a verified token.
        </div>

        <div className="content">
          <div className="page-head">
            <h2>Book an amenity</h2>
            <p>
              Slots are 30 minutes. Pick a start time, then a later slot to extend the booking.
            </p>
          </div>

          <div className="row">
            <div className="field">
              <label htmlFor="date">Date (UTC)</label>
              <input
                id="date"
                type="date"
                value={date}
                min={todayIso()}
                onChange={(e) => setDate(e.target.value)}
              />
            </div>
          </div>

          {error ? (
            <div className="banner">
              Couldn’t reach the API. Is the backend running on <code>http://localhost:5080</code>?
            </div>
          ) : null}

          {isLoading && <p className="empty">Loading amenities…</p>}

          {!isLoading &&
            amenities.map((amenity) => (
              <AmenityBookingCard key={amenity.id} amenity={amenity} date={date} />
            ))}

          {!isLoading && !error && amenities.length === 0 && (
            <p className="empty">No amenities in this building.</p>
          )}
        </div>
      </main>
    </div>
  );
}
