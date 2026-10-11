import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import * as api from './api';
import type { Participant, Registration, RegistrationStatus, Workshop } from './api';

const formatDate = (iso: string) =>
  new Date(iso).toLocaleString('en-GB', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' });

const formatEnd = (iso: string) =>
  new Date(iso).toLocaleTimeString('en-GB', { timeZone: 'UTC', timeStyle: 'short' });

function StatusBadge({ status }: { status: RegistrationStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{status}</span>;
}

interface RegistrationTableProps {
  rows: Registration[];
  showPosition?: boolean;
  busy: boolean;
  onCancel: (registration: Registration) => void;
}

function RegistrationTable({ rows, showPosition, busy, onCancel }: RegistrationTableProps) {
  if (rows.length === 0) return <p className="muted">None.</p>;

  return (
    <table>
      <thead>
        <tr>
          {showPosition && <th>#</th>}
          <th>Participant</th>
          <th>Email</th>
          <th>Status</th>
          <th>Registered (UTC)</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r, index) => (
          <tr key={r.id} className={r.status === 'Cancelled' ? 'cancelled' : undefined}>
            {showPosition && <td>{index + 1}</td>}
            <td>{r.participantName}</td>
            <td>{r.participantEmail}</td>
            <td>
              <StatusBadge status={r.status} />
            </td>
            <td>{formatDate(r.createdAt)}</td>
            <td>
              {r.status !== 'Cancelled' && (
                <button onClick={() => onCancel(r)} disabled={busy}>
                  Cancel
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [selectedWorkshopId, setSelectedWorkshopId] = useState<number | null>(null);
  const [registrations, setRegistrations] = useState<Registration[]>([]);
  const [registerParticipantId, setRegisterParticipantId] = useState<number | ''>('');
  const [scheduleParticipantId, setScheduleParticipantId] = useState<number | ''>('');
  const [schedule, setSchedule] = useState<Registration[]>([]);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Reused across retries of the same attempt, so a retry after a network failure cannot create a duplicate.
  const pendingAttempt = useRef<{ workshopId: number; participantId: number; key: string } | null>(null);

  const loadWorkshops = useCallback(async () => setWorkshops(await api.getWorkshops()), []);
  const loadRegistrations = useCallback(
    async (workshopId: number) => setRegistrations(await api.getWorkshopRegistrations(workshopId)),
    [],
  );
  const loadSchedule = useCallback(
    async (participantId: number) => setSchedule(await api.getSchedule(participantId)),
    [],
  );

  useEffect(() => {
    Promise.all([api.getWorkshops(), api.getParticipants()])
      .then(([w, p]) => {
        setWorkshops(w);
        setParticipants(p);
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)));
  }, []);

  useEffect(() => {
    if (selectedWorkshopId === null) {
      setRegistrations([]);
      return;
    }
    loadRegistrations(selectedWorkshopId).catch((e: unknown) => setError(String(e)));
  }, [selectedWorkshopId, loadRegistrations]);

  useEffect(() => {
    if (scheduleParticipantId === '') {
      setSchedule([]);
      return;
    }
    loadSchedule(scheduleParticipantId).catch((e: unknown) => setError(String(e)));
  }, [scheduleParticipantId, loadSchedule]);

  // Runs a user action, tracking busy state and reporting failures in the banner.
  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  // Everything that a registration or cancellation can change, including seat counts and promotions.
  const refreshAll = async () => {
    await loadWorkshops();
    if (selectedWorkshopId !== null) await loadRegistrations(selectedWorkshopId);
    if (scheduleParticipantId !== '') await loadSchedule(scheduleParticipantId);
  };

  const register = (event: FormEvent) => {
    event.preventDefault();
    if (selectedWorkshopId === null || registerParticipantId === '') return;

    const workshopId = selectedWorkshopId;
    const participantId = registerParticipantId;
    const previous = pendingAttempt.current;
    const key =
      previous && previous.workshopId === workshopId && previous.participantId === participantId
        ? previous.key
        : crypto.randomUUID();
    pendingAttempt.current = { workshopId, participantId, key };

    void run(async () => {
      try {
        const result = await api.registerParticipant(workshopId, participantId, key);
        pendingAttempt.current = null;
        setMessage(`${result.participantName} is ${result.status.toLowerCase()} for "${result.workshopTitle}".`);
      } catch (e) {
        // A network failure leaves the outcome unknown, so keep the key for a safe retry.
        // Any answer from the server is final for this attempt.
        if (!(e instanceof TypeError)) pendingAttempt.current = null;
        throw e;
      }
      await refreshAll();
    });
  };

  const cancel = (registration: Registration) =>
    void run(async () => {
      const result = await api.cancelRegistration(registration.id);
      setMessage(`Cancelled ${result.participantName}'s registration for "${result.workshopTitle}".`);
      await refreshAll();
    });

  const selectedWorkshop = workshops.find((w) => w.id === selectedWorkshopId) ?? null;
  const confirmed = registrations.filter((r) => r.status === 'Confirmed');
  const waitlisted = registrations.filter((r) => r.status === 'Waitlisted');
  const cancelled = registrations.filter((r) => r.status === 'Cancelled');

  return (
    <main>
      <h1>SeatFlow</h1>

      {error && (
        <div className="banner error" role="alert">
          {error} <button onClick={() => setError(null)}>Dismiss</button>
        </div>
      )}
      {message && <div className="banner info">{message}</div>}

      <section>
        <h2>Workshops</h2>
        <table>
          <thead>
            <tr>
              <th>Title</th>
              <th>When (UTC)</th>
              <th>Capacity</th>
              <th>Confirmed</th>
              <th>Waitlisted</th>
              <th>Remaining</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {workshops.map((w) => (
              <tr key={w.id} className={w.id === selectedWorkshopId ? 'selected' : undefined}>
                <td>{w.title}</td>
                <td>
                  {formatDate(w.startTime)} – {formatEnd(w.endTime)}
                </td>
                <td>{w.capacity}</td>
                <td>{w.confirmedCount}</td>
                <td>{w.waitlistedCount}</td>
                <td>{w.remainingCapacity}</td>
                <td>
                  <button onClick={() => setSelectedWorkshopId(w.id)}>Select</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {selectedWorkshop && (
        <section>
          <h2>{selectedWorkshop.title}</h2>

          <form className="inline-form" onSubmit={register}>
            <label>
              Participant{' '}
              <select
                value={registerParticipantId}
                onChange={(e) => setRegisterParticipantId(e.target.value === '' ? '' : Number(e.target.value))}
              >
                <option value="">Choose…</option>
                {participants.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
            </label>{' '}
            <button type="submit" disabled={busy || registerParticipantId === ''}>
              Register
            </button>
          </form>

          <h3>Confirmed ({confirmed.length})</h3>
          <RegistrationTable rows={confirmed} busy={busy} onCancel={cancel} />

          <h3>Waitlist, FIFO ({waitlisted.length})</h3>
          <RegistrationTable rows={waitlisted} showPosition busy={busy} onCancel={cancel} />

          <h3>Cancelled ({cancelled.length})</h3>
          <RegistrationTable rows={cancelled} busy={busy} onCancel={cancel} />
        </section>
      )}

      <section>
        <h2>Participant schedule</h2>
        <label>
          Participant{' '}
          <select
            value={scheduleParticipantId}
            onChange={(e) => setScheduleParticipantId(e.target.value === '' ? '' : Number(e.target.value))}
          >
            <option value="">Choose…</option>
            {participants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>

        {scheduleParticipantId !== '' &&
          (schedule.length === 0 ? (
            <p className="muted">No active registrations.</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Workshop</th>
                  <th>When (UTC)</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {schedule.map((r) => (
                  <tr key={r.id}>
                    <td>{r.workshopTitle}</td>
                    <td>
                      {formatDate(r.workshopStartTime)} – {formatEnd(r.workshopEndTime)}
                    </td>
                    <td>
                      <StatusBadge status={r.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ))}
      </section>
    </main>
  );
}
