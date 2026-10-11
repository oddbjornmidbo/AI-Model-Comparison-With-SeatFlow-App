import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { register, request } from './api';
import type { Participant, Registration, RegistrationStatus, ScheduleItem, Workshop } from './api';

const dateFormatter = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', weekday: 'short' });
const timeFormatter = new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' });
const fullFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

function timeRange(start: string, end: string) {
  const from = new Date(start);
  const to = new Date(end);
  const sameDay = from.toDateString() === to.toDateString();
  return `${dateFormatter.format(from)} · ${timeFormatter.format(from)}–${sameDay ? '' : `${dateFormatter.format(to)} `}${timeFormatter.format(to)}`;
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Something went wrong. Please retry.';
}

interface Notice { kind: 'success' | 'error'; text: string }

function RegistrationGroup({ title, status, rows, busy, onCancel }: {
  title: string;
  status: RegistrationStatus;
  rows: Registration[];
  busy: boolean;
  onCancel: (registration: Registration) => void;
}) {
  const registrations = rows.filter(row => row.status === status).sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id - b.id);
  return (
    <section className="registration-group">
      <div className="section-heading"><h3>{title}</h3><span className="count">{registrations.length}</span></div>
      {registrations.length === 0 ? <p className="empty-small">No {status.toLowerCase()} registrations.</p> : (
        <ul className="registration-list">
          {registrations.map((row, index) => (
            <li key={row.id}>
              <div className="registration-person">
                {status === 'Waitlisted' && <span className="queue-position" title="Waitlist position">{index + 1}</span>}
                <div><strong>{row.participantName}</strong><span className="secondary">{row.participantEmail}</span></div>
              </div>
              <div className="registration-actions">
                <span className={`badge ${status.toLowerCase()}`}>{status}</span>
                {status !== 'Cancelled' && <button className="button-text danger" disabled={busy} onClick={() => onCancel(row)} aria-label={`Cancel registration for ${row.participantName}`}>Cancel</button>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function WorkshopForm({ busy, onSubmit, onClose }: {
  busy: boolean;
  onSubmit: (data: { title: string; startTime: string; endTime: string; capacity: number }) => Promise<void>;
  onClose: () => void;
}) {
  const [title, setTitle] = useState('');
  const [startTime, setStartTime] = useState('');
  const [endTime, setEndTime] = useState('');
  const [capacity, setCapacity] = useState(10);
  const [error, setError] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (new Date(endTime) <= new Date(startTime)) {
      setError('End time must be after start time.');
      return;
    }
    setError('');
    await onSubmit({ title: title.trim(), startTime: new Date(startTime).toISOString(), endTime: new Date(endTime).toISOString(), capacity });
  }

  return (
    <form className="panel workshop-form" onSubmit={submit}>
      <div className="section-heading"><h2>Add workshop</h2><button className="button-text" type="button" onClick={onClose} disabled={busy}>Close</button></div>
      <label>Title<input required maxLength={200} value={title} onChange={event => setTitle(event.target.value)} placeholder="Workshop title" /></label>
      <div className="form-grid">
        <label>Starts<input type="datetime-local" required value={startTime} onChange={event => setStartTime(event.target.value)} /></label>
        <label>Ends<input type="datetime-local" required value={endTime} onChange={event => setEndTime(event.target.value)} /></label>
        <label>Capacity<input type="number" min={1} max={10000} required value={capacity} onChange={event => setCapacity(Number(event.target.value))} /></label>
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="form-footer"><span className="muted">Times use your local time zone.</span><button className="button-primary" disabled={busy || !title.trim()}>{busy ? 'Saving…' : 'Create workshop'}</button></div>
    </form>
  );
}

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [selectedWorkshopId, setSelectedWorkshopId] = useState<number | null>(null);
  const [participantId, setParticipantId] = useState<number | null>(null);
  const [registrations, setRegistrations] = useState<Registration[]>([]);
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const [initialLoading, setInitialLoading] = useState(true);
  const [registrationsLoading, setRegistrationsLoading] = useState(false);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [registrationsError, setRegistrationsError] = useState('');
  const [scheduleError, setScheduleError] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [showWorkshopForm, setShowWorkshopForm] = useState(false);
  const [refreshVersion, setRefreshVersion] = useState(0);

  const selectedWorkshop = workshops.find(workshop => workshop.id === selectedWorkshopId);
  const participant = participants.find(person => person.id === participantId);
  const activeRegistration = registrations.find(row => row.participantId === participantId && row.status !== 'Cancelled');

  const loadCatalog = useCallback(async () => {
    const [nextWorkshops, nextParticipants] = await Promise.all([
      request<Workshop[]>('/workshops'),
      request<Participant[]>('/participants'),
    ]);
    setWorkshops(nextWorkshops);
    setParticipants(nextParticipants);
    setSelectedWorkshopId(current => nextWorkshops.some(workshop => workshop.id === current) ? current : nextWorkshops[0]?.id ?? null);
    setParticipantId(current => nextParticipants.some(person => person.id === current) ? current : nextParticipants[0]?.id ?? null);
  }, []);

  useEffect(() => {
    let active = true;
    loadCatalog().catch(error => {
      if (active) setNotice({ kind: 'error', text: errorMessage(error) });
    }).finally(() => {
      if (active) setInitialLoading(false);
    });
    return () => { active = false; };
  }, [loadCatalog]);

  useEffect(() => {
    if (selectedWorkshopId === null) { setRegistrations([]); return; }
    const controller = new AbortController();
    setRegistrations([]);
    setRegistrationsError('');
    setRegistrationsLoading(true);
    request<Registration[]>(`/workshops/${selectedWorkshopId}/registrations`, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setRegistrations(data); })
      .catch(error => { if (!controller.signal.aborted) setRegistrationsError(errorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setRegistrationsLoading(false); });
    return () => controller.abort();
  }, [selectedWorkshopId, refreshVersion]);

  useEffect(() => {
    if (participantId === null) { setSchedule([]); return; }
    const controller = new AbortController();
    setSchedule([]);
    setScheduleError('');
    setScheduleLoading(true);
    request<ScheduleItem[]>(`/participants/${participantId}/schedule`, { signal: controller.signal })
      .then(data => { if (!controller.signal.aborted) setSchedule(data); })
      .catch(error => { if (!controller.signal.aborted) setScheduleError(errorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setScheduleLoading(false); });
    return () => controller.abort();
  }, [participantId, refreshVersion]);

  async function refresh() {
    try {
      await loadCatalog();
      setRefreshVersion(current => current + 1);
    } catch (error) {
      setNotice({ kind: 'error', text: `Your change was saved, but refreshing the display failed. ${errorMessage(error)}` });
    }
  }

  async function reload() {
    setBusy(true);
    setNotice(null);
    try {
      await loadCatalog();
      setRefreshVersion(current => current + 1);
    } catch (error) {
      setNotice({ kind: 'error', text: errorMessage(error) });
    } finally { setBusy(false); }
  }

  async function submitRegistration() {
    if (!selectedWorkshopId || !participantId) return;
    setBusy(true);
    setNotice(null);
    try {
      const result = await register(selectedWorkshopId, participantId);
      setNotice({ kind: 'success', text: result.status === 'Confirmed' ? `${result.participantName} is confirmed for this workshop.` : result.status === 'Waitlisted' ? `${result.participantName} has joined the waitlist.` : 'This registration has already been cancelled. Refresh and register again to make a new request.' });
      await refresh();
    } catch (error) {
      setNotice({ kind: 'error', text: errorMessage(error) });
    } finally { setBusy(false); }
  }

  async function cancelRegistration(registration: Registration) {
    setBusy(true);
    setNotice(null);
    try {
      await request<Registration>(`/registrations/${registration.id}/cancel`, { method: 'POST' });
      setNotice({ kind: 'success', text: `Registration for ${registration.participantName} was cancelled. Any eligible waitlisted participant is promoted automatically.` });
      await refresh();
    } catch (error) {
      setNotice({ kind: 'error', text: errorMessage(error) });
    } finally { setBusy(false); }
  }

  async function createWorkshop(data: { title: string; startTime: string; endTime: string; capacity: number }) {
    setBusy(true);
    setNotice(null);
    try {
      const created = await request<Workshop>('/workshops', { method: 'POST', body: JSON.stringify(data) });
      setSelectedWorkshopId(created.id);
      setShowWorkshopForm(false);
      setNotice({ kind: 'success', text: `${created.title} was created.` });
      await refresh();
    } catch (error) {
      setNotice({ kind: 'error', text: errorMessage(error) });
    } finally { setBusy(false); }
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand"><span className="brand-mark" aria-hidden="true">S</span><div><h1>SeatFlow</h1><p>Workshop registrations, without the juggling.</p></div></div>
        <div className="header-actions"><button className="button-secondary" onClick={reload} disabled={busy || initialLoading}>{busy ? 'Working…' : 'Refresh'}</button><button className="button-primary" onClick={() => setShowWorkshopForm(current => !current)} disabled={busy}>Add workshop</button></div>
      </header>

      {notice && <div className={`notice ${notice.kind}`} role={notice.kind === 'error' ? 'alert' : 'status'}><span>{notice.text}</span><button aria-label="Dismiss message" onClick={() => setNotice(null)}>×</button></div>}
      {showWorkshopForm && <WorkshopForm busy={busy} onSubmit={createWorkshop} onClose={() => setShowWorkshopForm(false)} />}

      {initialLoading ? <div className="panel loading">Loading workshops and participants…</div> : (
        <>
          <div className="participant-toolbar">
            <label htmlFor="participant">Manage registration for</label>
            <select id="participant" value={participantId ?? ''} disabled={busy || participants.length === 0} onChange={event => { setParticipantId(Number(event.target.value)); setNotice(null); }}>
              {participants.length === 0 && <option value="">No participants available</option>}
              {participants.map(person => <option key={person.id} value={person.id}>{person.name} · {person.email}</option>)}
            </select>
            <span className="muted">All times shown in your local time zone.</span>
          </div>

          <main className="workspace">
            <aside className="workshops-column">
              <div className="section-heading"><h2>Workshops</h2><span className="count">{workshops.length}</span></div>
              {workshops.length === 0 ? <div className="panel empty-small">No workshops yet. Add a workshop to get started.</div> : <div className="workshop-list">{workshops.map(workshop => (
                <button key={workshop.id} className={`workshop-card ${workshop.id === selectedWorkshopId ? 'selected' : ''}`} onClick={() => { setSelectedWorkshopId(workshop.id); setNotice(null); }} disabled={busy} aria-pressed={workshop.id === selectedWorkshopId}>
                  <h3>{workshop.title}</h3>
                  <p>{timeRange(workshop.startTime, workshop.endTime)}</p>
                  <div className="seat-count"><span className={workshop.remainingCapacity > 0 ? 'available' : 'full'}>{workshop.remainingCapacity > 0 ? `${workshop.remainingCapacity} ${workshop.remainingCapacity === 1 ? 'seat' : 'seats'} available` : 'Full · waitlist open'}</span><span>{workshop.confirmedCount}/{workshop.capacity}</span></div>
                  <div className="capacity-track" aria-hidden="true"><span style={{ width: `${workshop.capacity ? Math.min(100, workshop.confirmedCount / workshop.capacity * 100) : 0}%` }} /></div>
                  {workshop.waitlistedCount > 0 && <span className="waitlist-summary">{workshop.waitlistedCount} on waitlist</span>}
                </button>
              ))}</div>}
            </aside>

            <div className="details-column">
              {selectedWorkshop && <section className="panel workshop-detail">
                <div className="detail-heading"><div><span className="eyebrow">Selected workshop</span><h2>{selectedWorkshop.title}</h2><p className="muted">{timeRange(selectedWorkshop.startTime, selectedWorkshop.endTime)}</p></div><span className="capacity-total">{selectedWorkshop.capacity} seats</span></div>
                <div className="registration-callout"><div><strong>{activeRegistration ? `${participant?.name} is ${activeRegistration.status.toLowerCase()}.` : `Register ${participant?.name ?? 'a participant'}`}</strong><p>{activeRegistration ? 'Cancel the current registration below before registering again.' : selectedWorkshop.remainingCapacity > 0 ? 'Available seats are confirmed when there is no schedule conflict.' : 'This workshop is full. New registrations join the waitlist.'}</p></div><button className="button-primary" disabled={busy || registrationsLoading || !!registrationsError || !participant || !!activeRegistration} onClick={submitRegistration}>{busy ? 'Working…' : selectedWorkshop.remainingCapacity > 0 ? 'Register participant' : 'Join waitlist'}</button></div>
                {registrationsLoading ? <p className="loading">Loading registrations…</p> : registrationsError ? <p className="form-error" role="alert">{registrationsError} Use Refresh to retry.</p> : <>
                  <RegistrationGroup title="Confirmed" status="Confirmed" rows={registrations} busy={busy} onCancel={cancelRegistration} />
                  <RegistrationGroup title="Waitlist · first in, first out" status="Waitlisted" rows={registrations} busy={busy} onCancel={cancelRegistration} />
                  <p className="waitlist-note">When a seat opens, the first participant without an overlapping confirmed workshop is promoted. Skipped participants keep their place.</p>
                  <RegistrationGroup title="Cancelled" status="Cancelled" rows={registrations} busy={busy} onCancel={cancelRegistration} />
                </>}
              </section>}

              <section className="panel schedule-panel">
                <div className="section-heading"><h2>{participant ? `${participant.name}’s schedule` : 'Participant schedule'}</h2><span className="badge confirmed">Confirmed only</span></div>
                {scheduleLoading ? <p className="loading">Loading schedule…</p> : scheduleError ? <p className="form-error" role="alert">{scheduleError} Use Refresh to retry.</p> : schedule.length === 0 ? <p className="empty-small">{participant ? 'No confirmed workshops yet.' : 'Select a participant to see their schedule.'}</p> : (
                  <ul className="schedule-list">{[...schedule].sort((a, b) => a.startTime.localeCompare(b.startTime) || a.workshopId - b.workshopId).map(item => (
                    <li key={item.registrationId}><div><strong>{item.title}</strong><time dateTime={item.startTime}>{timeRange(item.startTime, item.endTime)}</time></div><button className="button-text" disabled={busy} onClick={() => setSelectedWorkshopId(item.workshopId)}>View workshop →</button></li>
                  ))}</ul>
                )}
              </section>
            </div>
          </main>
        </>
      )}
      <footer className="app-footer">SeatFlow · Confirmation follows workshop capacity and participant availability.<span title={fullFormatter.format(new Date())}>Local conference manager</span></footer>
    </div>
  );
}
