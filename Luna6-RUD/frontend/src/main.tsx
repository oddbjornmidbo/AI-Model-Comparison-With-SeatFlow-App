import React, { useCallback, useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './style.css';

type Workshop = { id: number; title: string; startsAt: string; endsAt: string; capacity: number; remainingCapacity: number };
type Participant = { id: number; name: string; email: string };
type Registration = { id: number; workshopId: number; participantId: number; participantName: string; participantEmail: string; status: Status; createdAt: string };
type Status = 'Confirmed' | 'Waitlisted' | 'Cancelled';
type Schedule = { participantId: number; participantName: string; registrations: { registrationId: number; workshopId: number; workshopTitle: string; startsAt: string; endsAt: string; status: Status }[] };
const API = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API}${path}`, init);
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error ?? `Request failed (${response.status})`);
  return data as T;
}
const date = (value: string) => new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });

function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [registrations, setRegistrations] = useState<Registration[]>([]);
  const [participantId, setParticipantId] = useState<number | ''>('');
  const [schedule, setSchedule] = useState<Schedule | null>(null);
  const [scheduleId, setScheduleId] = useState<number | ''>('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const selected = workshops.find(w => w.id === selectedId);

  const loadWorkshops = useCallback(async () => setWorkshops(await request<Workshop[]>('/api/workshops')), []);
  const loadRegistrations = useCallback(async () => {
    if (selectedId !== null) setRegistrations(await request<Registration[]>(`/api/workshops/${selectedId}/registrations`));
  }, [selectedId]);
  useEffect(() => { void Promise.all([loadWorkshops(), request<Participant[]>('/api/participants').then(setParticipants)]).catch(e => setError(e.message)); }, [loadWorkshops]);
  useEffect(() => { void loadRegistrations().catch(e => setError(e.message)); }, [loadRegistrations]);

  async function register(event: React.FormEvent) {
    event.preventDefault(); setError(''); setNotice('');
    if (selectedId === null || participantId === '') return;
    try {
      const key = crypto.randomUUID();
      const result = await request<{status: Status}>(`/api/workshops/${selectedId}/registrations`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key }, body: JSON.stringify({ participantId }) });
      setNotice(`Registration ${result.status.toLowerCase()}.`);
      await Promise.all([loadWorkshops(), loadRegistrations()]);
    } catch (e) { setError((e as Error).message); }
  }
  async function cancel(id: number) {
    setError(''); setNotice('');
    try { await request(`/api/registrations/${id}/cancel`, { method: 'POST' }); setNotice('Registration cancelled.'); await Promise.all([loadWorkshops(), loadRegistrations()]); }
    catch (e) { setError((e as Error).message); }
  }
  async function showSchedule(event: React.FormEvent) {
    event.preventDefault(); setError(''); setSchedule(null);
    if (scheduleId === '') return;
    try { setSchedule(await request<Schedule>(`/api/participants/${scheduleId}/schedule`)); } catch (e) { setError((e as Error).message); }
  }
  const confirmed = registrations.filter(r => r.status === 'Confirmed');
  const waitlisted = registrations.filter(r => r.status === 'Waitlisted');
  const cancelled = registrations.filter(r => r.status === 'Cancelled');
  return <main className="page">
    <header><h1>SeatFlow</h1><p>Workshop registrations for the conference</p></header>
    {error && <div className="alert error" role="alert">{error}</div>}{notice && <div className="alert success" role="status">{notice}</div>}
    <section aria-labelledby="workshops-heading"><h2 id="workshops-heading">Workshops</h2>
      <div className="workshop-grid">{workshops.map(w => <button type="button" className={`workshop-card ${w.id === selectedId ? 'selected' : ''}`} key={w.id} onClick={() => setSelectedId(w.id)} aria-pressed={w.id === selectedId}>
        <strong>{w.title}</strong><span>{date(w.startsAt)} – {date(w.endsAt)}</span><span>{w.remainingCapacity} of {w.capacity} seats remaining</span>
      </button>)}</div>
    </section>
    {selected && <section aria-labelledby="selected-heading"><h2 id="selected-heading">{selected.title}</h2>
      <form className="register-form" onSubmit={register}><label htmlFor="participant">Register participant</label><select id="participant" value={participantId} onChange={e => setParticipantId(e.target.value ? Number(e.target.value) : '')} required><option value="">Choose a participant</option>{participants.map(p => <option key={p.id} value={p.id}>{p.name} ({p.email})</option>)}</select><button disabled={participantId === ''} type="submit">Register</button></form>
      <div className="registration-grid">
        <RegistrationList heading="Confirmed" items={confirmed} onCancel={cancel}/>
        <RegistrationList heading="Waitlist" items={waitlisted} onCancel={cancel}/>
        {cancelled.length > 0 && <RegistrationList heading="Cancelled" items={cancelled} onCancel={cancel}/>}
      </div>
    </section>}
    <section aria-labelledby="schedule-heading"><h2 id="schedule-heading">Participant schedule</h2>
      <form className="register-form" onSubmit={showSchedule}><label htmlFor="schedule-participant">Participant</label><select id="schedule-participant" value={scheduleId} onChange={e => setScheduleId(e.target.value ? Number(e.target.value) : '')} required><option value="">Choose a participant</option>{participants.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select><button type="submit">View schedule</button></form>
      {schedule && <div className="schedule"><h3>{schedule.participantName}</h3>{schedule.registrations.length === 0 ? <p>No registrations yet.</p> : <ul>{schedule.registrations.map(r => <li key={r.registrationId}><span className={`badge ${r.status.toLowerCase()}`}>{r.status}</span> {r.workshopTitle} — {date(r.startsAt)} to {date(r.endsAt)}</li>)}</ul>}</div>}
    </section>
  </main>;
}
function RegistrationList({ heading, items, onCancel }: { heading: string; items: Registration[]; onCancel: (id: number) => void }) {
  return <div><h3>{heading}</h3>{items.length === 0 ? <p className="muted">No registrations</p> : <ul className="registration-list">{items.map(r => <li key={r.id}><span><span className={`badge ${r.status.toLowerCase()}`}>{r.status}</span> {r.participantName}</span>{r.status !== 'Cancelled' && <button type="button" className="secondary" onClick={() => onCancel(r.id)} aria-label={`Cancel ${r.participantName}'s registration`}>Cancel</button>}</li>)}</ul>}</div>;
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);
