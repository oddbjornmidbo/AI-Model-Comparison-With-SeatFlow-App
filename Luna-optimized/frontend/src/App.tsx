import { useEffect, useRef, useState } from 'react';
import { api } from './api';
import { ParticipantSchedule } from './components/ParticipantSchedule';
import { RegistrationList } from './components/RegistrationList';
import { formatWorkshopTime, WorkshopCard } from './components/WorkshopCard';
import type { Participant, Registration, ScheduleItem, SeatFlowClient, Workshop } from './types';

export interface AppProps {
  client?: SeatFlowClient;
}

function messageFrom(error: unknown) {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.';
}

function newRequestKey() {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `seatflow-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export function App({ client = api }: AppProps) {
  const [workshops, setWorkshops] = useState<Workshop[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [registrations, setRegistrations] = useState<Registration[]>([]);
  const [registrationsForWorkshopId, setRegistrationsForWorkshopId] = useState<string | null>(null);
  const [knownActiveRegistration, setKnownActiveRegistration] = useState<Registration | null>(null);
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const [selectedWorkshopId, setSelectedWorkshopId] = useState('');
  const [selectedParticipantId, setSelectedParticipantId] = useState('');
  const [workshopsLoading, setWorkshopsLoading] = useState(true);
  const [participantsLoading, setParticipantsLoading] = useState(true);
  const [registrationsLoading, setRegistrationsLoading] = useState(false);
  const [scheduleLoading, setScheduleLoading] = useState(false);
  const [workshopsError, setWorkshopsError] = useState<string | null>(null);
  const [participantsError, setParticipantsError] = useState<string | null>(null);
  const [registrationsError, setRegistrationsError] = useState<string | null>(null);
  const [scheduleError, setScheduleError] = useState<string | null>(null);
  const [busyRegistrationId, setBusyRegistrationId] = useState<string | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [dataWarning, setDataWarning] = useState<string | null>(null);

  const selectedWorkshopRef = useRef('');
  const selectedParticipantRef = useRef('');
  const registrationSequence = useRef(0);
  const scheduleSequence = useRef(0);
  const workshopSequence = useRef(0);
  const participantSequence = useRef(0);
  const idempotencyAttempt = useRef<{ key: string; workshopId: string; participantId: string } | null>(null);

  async function loadWorkshops() {
    const sequence = ++workshopSequence.current;
    setWorkshopsLoading(true);
    setWorkshopsError(null);
    try {
      const result = await client.getWorkshops();
      if (sequence !== workshopSequence.current) return false;
      setWorkshops(result);
      if (!selectedWorkshopRef.current || !result.some((item) => item.id === selectedWorkshopRef.current)) {
        const first = result[0]?.id ?? '';
        selectedWorkshopRef.current = first;
        setSelectedWorkshopId(first);
      }
      setWorkshopsLoading(false);
      return true;
    } catch (error) {
      if (sequence !== workshopSequence.current) return false;
      setWorkshops([]);
      setWorkshopsError(messageFrom(error));
      setWorkshopsLoading(false);
      return false;
    }
  }

  async function loadParticipants() {
    const sequence = ++participantSequence.current;
    setParticipantsLoading(true);
    setParticipantsError(null);
    try {
      const result = await client.getParticipants();
      if (sequence !== participantSequence.current) return false;
      setParticipants(result);
      if (!selectedParticipantRef.current || !result.some((item) => item.id === selectedParticipantRef.current)) {
        const first = result[0]?.id ?? '';
        selectedParticipantRef.current = first;
        setSelectedParticipantId(first);
      }
      setParticipantsLoading(false);
      return true;
    } catch (error) {
      if (sequence !== participantSequence.current) return false;
      setParticipants([]);
      setParticipantsError(messageFrom(error));
      setParticipantsLoading(false);
      return false;
    }
  }

  async function loadRegistrations(workshopId: string) {
    const sequence = ++registrationSequence.current;
    if (!workshopId) {
      setRegistrations([]);
      setRegistrationsForWorkshopId(null);
      setRegistrationsLoading(false);
      setRegistrationsError(null);
      return true;
    }
    setRegistrationsLoading(true);
    setRegistrationsError(null);
    setRegistrationsForWorkshopId(null);
    try {
      const result = await client.getRegistrations(workshopId);
      if (sequence !== registrationSequence.current) return false;
      setRegistrations(result);
      setRegistrationsForWorkshopId(workshopId);
      setKnownActiveRegistration((known) => known?.workshopId === workshopId && !result.some((item) => item.id === known.id && item.status !== 'Cancelled') ? null : known);
      setRegistrationsLoading(false);
      return true;
    } catch (error) {
      if (sequence !== registrationSequence.current) return false;
      setRegistrations([]);
      setRegistrationsError(messageFrom(error));
      setRegistrationsLoading(false);
      return false;
    }
  }

  async function loadSchedule(participantId: string) {
    const sequence = ++scheduleSequence.current;
    if (!participantId) {
      setSchedule([]);
      setScheduleLoading(false);
      setScheduleError(null);
      return true;
    }
    setScheduleLoading(true);
    setScheduleError(null);
    try {
      const result = await client.getSchedule(participantId);
      if (sequence !== scheduleSequence.current) return false;
      setSchedule(result);
      setScheduleLoading(false);
      return true;
    } catch (error) {
      if (sequence !== scheduleSequence.current) return false;
      setSchedule([]);
      setScheduleError(messageFrom(error));
      setScheduleLoading(false);
      return false;
    }
  }

  useEffect(() => {
    void loadWorkshops();
    void loadParticipants();
    // The client instance is stable for the lifetime of this screen.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => { void loadRegistrations(selectedWorkshopId); }, [selectedWorkshopId]);
  useEffect(() => { void loadSchedule(selectedParticipantId); }, [selectedParticipantId]);

  const selectedWorkshop = workshops.find((item) => item.id === selectedWorkshopId);
  const selectedParticipant = participants.find((item) => item.id === selectedParticipantId);
  const activeRegistration = (registrationsForWorkshopId === selectedWorkshopId
    ? registrations.find((item) => item.workshopId === selectedWorkshopId && item.participantId === selectedParticipantId && item.status !== 'Cancelled')
    : undefined)
    ?? (knownActiveRegistration?.workshopId === selectedWorkshopId && knownActiveRegistration.participantId === selectedParticipantId && knownActiveRegistration.status !== 'Cancelled'
      ? knownActiveRegistration
      : undefined);
  const scheduleConflict = Boolean(!scheduleLoading && selectedWorkshop && selectedWorkshop.remainingCapacity > 0 && schedule.some((item) =>
    new Date(selectedWorkshop.startTime) < new Date(item.endTime) && new Date(selectedWorkshop.endTime) > new Date(item.startTime)));

  function chooseWorkshop(id: string) {
    selectedWorkshopRef.current = id;
    setSelectedWorkshopId(id);
    setActionError(null);
    setActionMessage(null);
    setDataWarning(null);
  }

  function chooseParticipant(id: string) {
    selectedParticipantRef.current = id;
    setSelectedParticipantId(id);
    setActionError(null);
    setActionMessage(null);
    setDataWarning(null);
  }

  async function refreshVisibleData() {
    const workshopsOk = await loadWorkshops();
    const [registrationsOk, scheduleOk] = await Promise.all([
      loadRegistrations(selectedWorkshopRef.current),
      loadSchedule(selectedParticipantRef.current),
    ]);
    return workshopsOk && registrationsOk && scheduleOk;
  }

  async function handleRegister() {
    const workshopId = selectedWorkshopRef.current;
    const participantId = selectedParticipantRef.current;
    if (!workshopId || !participantId || !selectedWorkshop) return;
    setActionError(null);
    setActionMessage(null);
    setDataWarning(null);
    setActionBusy(true);
    const current = idempotencyAttempt.current;
    if (!current || current.workshopId !== workshopId || current.participantId !== participantId) {
      idempotencyAttempt.current = { key: newRequestKey(), workshopId, participantId };
    }
    try {
      const registration = await client.register(workshopId, participantId, idempotencyAttempt.current!.key);
      idempotencyAttempt.current = null;
      if (registration.status !== 'Cancelled') setKnownActiveRegistration(registration);
      const refreshed = await refreshVisibleData();
      if (refreshed) {
        const status = registration.status === 'Confirmed' ? 'confirmed' : 'added to the waitlist';
        setActionMessage(`${selectedParticipant?.name ?? 'Participant'} is ${status} for ${selectedWorkshop.title}.`);
      } else {
        setDataWarning('The registration was saved, but one or more views could not refresh. The affected panel shows its refresh error.');
      }
    } catch (error) {
      setActionError(messageFrom(error));
    } finally {
      setActionBusy(false);
    }
  }

  async function handleCancel(registration: Registration) {
    setActionError(null);
    setActionMessage(null);
    setDataWarning(null);
    setActionBusy(true);
    setBusyRegistrationId(registration.id);
    try {
      await client.cancel(registration.id);
      setKnownActiveRegistration((known) => known?.id === registration.id ? null : known);
      const refreshed = await refreshVisibleData();
      if (refreshed) setActionMessage(registration.status === 'Confirmed'
        ? `${registration.participantName}'s registration was cancelled. Any available seat has been offered to the waitlist.`
        : `${registration.participantName}'s waitlist place was cancelled.`);
      else setDataWarning('The cancellation was saved, but one or more views could not refresh. The affected panel shows its refresh error.');
    } catch (error) {
      setActionError(messageFrom(error));
    } finally {
      setActionBusy(false);
      setBusyRegistrationId(null);
    }
  }

  async function handleRefresh() {
    setActionError(null);
    setActionMessage(null);
    setDataWarning(null);
    setActionBusy(true);
    try {
      const workshopsOk = await loadWorkshops();
      const participantsOk = await loadParticipants();
      const [registrationsOk, scheduleOk] = await Promise.all([
        loadRegistrations(selectedWorkshopRef.current),
        loadSchedule(selectedParticipantRef.current),
      ]);
      if ([workshopsOk, participantsOk, registrationsOk, scheduleOk].every(Boolean)) setActionMessage('Everything is up to date.');
      else setDataWarning('Some information could not be refreshed. Check the affected panel and try again.');
    } finally {
      setActionBusy(false);
    }
  }

  const registerDisabledReason = activeRegistration
    ? `${selectedParticipant?.name ?? 'This participant'} already has an active registration for this workshop.`
    : scheduleLoading && selectedWorkshop && selectedWorkshop.remainingCapacity > 0
      ? 'Checking this participant’s schedule for overlapping workshops…'
    : scheduleConflict
      ? 'This workshop overlaps a confirmed session on this participant’s schedule.'
      : null;

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#top" aria-label="SeatFlow home"><span className="brand-mark" aria-hidden="true">S</span><span>seatflow</span></a>
        <div className="topbar-actions">
          <span className="conference-label"><span className="live-dot" aria-hidden="true" /> Conference desk</span>
          <button className="button button-quiet refresh-button" type="button" onClick={() => void handleRefresh()} disabled={actionBusy}>
            <span aria-hidden="true">↻</span> Refresh
          </button>
        </div>
      </header>

      <main id="top" className="page-content">
        <section className="intro-row" aria-labelledby="page-title">
          <div>
            <p className="eyebrow">Workshop registration</p>
            <h1 id="page-title">Make room for a good idea.</h1>
            <p className="intro-copy">Find a session, manage your seat, and keep everyone’s day in sync.</p>
          </div>
          <div className="date-chip"><span className="date-chip-icon" aria-hidden="true">✳</span><span>Plan the day<br /><strong>One seat at a time</strong></span></div>
        </section>

        {(actionError || actionMessage || dataWarning) && <div className={`action-feedback${actionError ? ' feedback-error' : dataWarning ? ' feedback-warning' : ''}`} role={actionError || dataWarning ? 'alert' : 'status'}>
          <span aria-hidden="true">{actionError ? '!' : dataWarning ? '↻' : '✓'}</span>
          <span>{actionError ?? dataWarning ?? actionMessage}</span>
          <button type="button" className="feedback-dismiss" aria-label="Dismiss message" onClick={() => { setActionError(null); setActionMessage(null); setDataWarning(null); }}>×</button>
        </div>}

        <div className="workspace-grid">
          <section className="panel workshop-panel" aria-labelledby="workshops-heading">
            <div className="panel-heading">
              <div><p className="eyebrow">Explore sessions</p><h2 id="workshops-heading">Workshops <span className="heading-count">{workshops.length}</span></h2></div>
              <span className="panel-caption">Select one to manage seats</span>
            </div>
            {workshopsLoading ? <p className="inline-state" role="status">Loading workshops…</p>
              : workshopsError ? <p className="inline-state inline-state-error" role="alert">{workshopsError}</p>
                : workshops.length === 0 ? <p className="empty-state">No workshops are available yet.</p>
                : <div className="workshop-list">{workshops.map((workshop) => <WorkshopCard key={workshop.id} workshop={workshop} selected={workshop.id === selectedWorkshopId} disabled={actionBusy} onSelect={chooseWorkshop} />)}</div>}
            {participantsError && <p className="subtle-error" role="alert">Participants could not load: {participantsError}</p>}
          </section>

          <div className="detail-column">
            <section className="panel details-panel" aria-labelledby="selected-workshop-heading">
              {selectedWorkshop ? <>
                <div className="detail-heading">
                  <div><p className="eyebrow">Selected workshop</p><h2 id="selected-workshop-heading">{selectedWorkshop.title}</h2><p className="detail-time">{formatWorkshopTime(selectedWorkshop.startTime, selectedWorkshop.endTime)}</p></div>
                  <div className="capacity-pill"><strong>{selectedWorkshop.remainingCapacity}</strong><span>of {selectedWorkshop.capacity} seats left</span></div>
                </div>
                <div className="registration-form-row">
                  <div className="field-wrap">
                    <label className="field-label" htmlFor="registration-participant">Register a participant</label>
                    <select id="registration-participant" value={selectedParticipantId} onChange={(event) => chooseParticipant(event.target.value)} disabled={actionBusy || participantsLoading || participants.length === 0}>
                      {participantsLoading && <option value="">Loading participants…</option>}
                      {!participantsLoading && participants.length === 0 && <option value="">No participants available</option>}
                      {participants.map((participant) => <option key={participant.id} value={participant.id}>{participant.name}</option>)}
                    </select>
                  </div>
                <button className="button button-primary register-button" type="button" onClick={() => void handleRegister()} disabled={actionBusy || workshopsLoading || registrationsLoading || participantsLoading || (scheduleLoading && selectedWorkshop.remainingCapacity > 0) || !selectedParticipantId || Boolean(registerDisabledReason)}>
                    {actionBusy ? 'Saving…' : selectedWorkshop.remainingCapacity === 0 ? 'Join waitlist' : 'Register'}
                    <span aria-hidden="true">→</span>
                  </button>
                </div>
                {registerDisabledReason ? <p className="form-hint form-hint-warning">{registerDisabledReason}</p>
                  : selectedWorkshop.remainingCapacity === 0 ? <p className="form-hint">This session is full. You can join the waitlist and keep your place in order.</p>
                    : <p className="form-hint">Choose a participant to reserve one of the remaining seats.</p>}
                <div className="section-divider" />
                <div className="registrations-heading"><div><h3>Attendees</h3><p>Confirmed seats, followed by the waitlist.</p></div><span className="attendee-total">{registrations.filter((item) => item.status !== 'Cancelled').length} active</span></div>
                <RegistrationList registrations={registrations} loading={registrationsLoading} error={registrationsError} busyRegistrationId={busyRegistrationId} mutationsDisabled={actionBusy} onCancel={(registration) => void handleCancel(registration)} />
              </> : <div className="detail-placeholder"><span className="placeholder-mark" aria-hidden="true">↗</span><h2 id="selected-workshop-heading">Choose a workshop</h2><p>Pick a session to see its attendees and manage registrations.</p></div>}
            </section>

            <ParticipantSchedule participants={participants} selectedParticipantId={selectedParticipantId} schedule={schedule} loading={scheduleLoading} error={scheduleError} onParticipantChange={chooseParticipant} />
          </div>
        </div>
        <footer className="page-footer"><span>SeatFlow</span><span>Workshop registration, kept in step.</span></footer>
      </main>
    </div>
  );
}
