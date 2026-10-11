import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "./api";
import { errorMessage } from "./format";
import { ParticipantSchedule } from "./components/ParticipantSchedule";
import { WorkshopDetail } from "./components/WorkshopDetail";
import { WorkshopTable } from "./components/WorkshopTable";
import type { ParticipantDto, RegistrationDto, WorkshopDto } from "./types";

interface Message {
  kind: "success" | "error";
  text: string;
}

interface PendingRegistration {
  workshopId: number;
  participantId: number;
  key: string;
}

export default function App() {
  const [workshops, setWorkshops] = useState<WorkshopDto[]>([]);
  const [participants, setParticipants] = useState<ParticipantDto[]>([]);
  const [selectedWorkshopId, setSelectedWorkshopId] = useState<number | null>(null);
  const [registrations, setRegistrations] = useState<RegistrationDto[]>([]);
  const [scheduleParticipantId, setScheduleParticipantId] = useState<number | null>(null);
  const [schedule, setSchedule] = useState<RegistrationDto[]>([]);
  const [message, setMessage] = useState<Message | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  // Keeps the Idempotency-Key of an attempt so a retry after a network failure cannot double-register.
  const pendingRegistration = useRef<PendingRegistration | null>(null);
  const messageRef = useRef<HTMLDivElement>(null);

  const loadWorkshops = useCallback(async () => {
    setWorkshops(await api.listWorkshops());
  }, []);

  const loadRegistrations = useCallback(async (workshopId: number) => {
    setRegistrations(await api.listRegistrations(workshopId));
  }, []);

  const loadSchedule = useCallback(async (participantId: number) => {
    setSchedule(await api.schedule(participantId));
  }, []);

  useEffect(() => {
    Promise.all([api.listWorkshops(), api.listParticipants()])
      .then(([loadedWorkshops, loadedParticipants]) => {
        setWorkshops(loadedWorkshops);
        setParticipants(loadedParticipants);
        setScheduleParticipantId(loadedParticipants[0]?.id ?? null);
      })
      .catch((error: unknown) => setLoadError(errorMessage(error)));
  }, []);

  useEffect(() => {
    if (selectedWorkshopId === null) return;
    loadRegistrations(selectedWorkshopId).catch((error: unknown) =>
      setMessage({ kind: "error", text: errorMessage(error) }),
    );
  }, [selectedWorkshopId, loadRegistrations]);

  useEffect(() => {
    if (scheduleParticipantId === null) return;
    loadSchedule(scheduleParticipantId).catch((error: unknown) =>
      setMessage({ kind: "error", text: errorMessage(error) }),
    );
  }, [scheduleParticipantId, loadSchedule]);

  // Re-reads everything a mutation can change, so the screen reflects promotions and seat counts.
  const reloadAfterChange = async () => {
    await loadWorkshops();
    if (selectedWorkshopId !== null) await loadRegistrations(selectedWorkshopId);
    if (scheduleParticipantId !== null) await loadSchedule(scheduleParticipantId);
  };

  const announce = (next: Message) => {
    setMessage(next);
    // Move focus to the status message so keyboard users are not left on a button that just disappeared.
    messageRef.current?.focus();
  };

  const register = async (workshopId: number, participantId: number) => {
    const previous = pendingRegistration.current;
    const retrying = previous?.workshopId === workshopId && previous.participantId === participantId;
    const key = retrying && previous ? previous.key : crypto.randomUUID();
    pendingRegistration.current = { workshopId, participantId, key };

    setPending(true);
    try {
      const registration = await api.register(workshopId, participantId, key);
      pendingRegistration.current = null;
      const text =
        registration.status === "Confirmed"
          ? `${registration.participantName} is confirmed for ${registration.workshopTitle}.`
          : `${registration.participantName} is on the waitlist for ${registration.workshopTitle}.`;
      announce({ kind: "success", text });
    } catch (error) {
      // A definite server answer ends the attempt. A network failure keeps the key for an identical retry.
      if (error instanceof ApiError) pendingRegistration.current = null;
      announce({ kind: "error", text: errorMessage(error) });
    } finally {
      setPending(false);
    }
    await reloadAfterChange().catch(() => undefined);
  };

  const cancel = async (registration: RegistrationDto) => {
    setPending(true);
    try {
      const result = await api.cancel(registration.id);
      const text = result.promoted
        ? `${registration.participantName}'s registration was cancelled. ${result.promoted.participantName} was promoted from the waitlist.`
        : `${registration.participantName}'s registration for ${registration.workshopTitle} was cancelled.`;
      announce({ kind: "success", text });
    } catch (error) {
      announce({ kind: "error", text: errorMessage(error) });
    } finally {
      setPending(false);
    }
    await reloadAfterChange().catch(() => undefined);
  };

  const selectedWorkshop = workshops.find((w) => w.id === selectedWorkshopId) ?? null;

  if (loadError) {
    return (
      <main className="page">
        <h1>SeatFlow</h1>
        <p role="alert" className="message message-error">Could not load data: {loadError}</p>
      </main>
    );
  }

  return (
    <>
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="site-header">
        <h1>SeatFlow</h1>
        <p>Workshop registrations for the conference.</p>
        <p className="legend">
          <strong>Confirmed</strong> means a seat is held. <strong>Waitlisted</strong> means the participant is
          promoted in order when a seat frees up. <strong>Cancelled</strong> means the registration is no longer active.
        </p>
      </header>

      <div
        ref={messageRef}
        tabIndex={-1}
        role="status"
        aria-live="polite"
        className={message ? `message message-${message.kind}` : "message-empty"}
      >
        {message?.text}
      </div>

      <main id="main" className="page">
        <section aria-labelledby="workshops-heading">
          <h2 id="workshops-heading">Workshops</h2>
          <WorkshopTable
            workshops={workshops}
            selectedId={selectedWorkshopId}
            onSelect={(id) => {
              // Clear first so the detail never shows the previous workshop's registrations while loading.
              setRegistrations([]);
              setSelectedWorkshopId(id);
              setMessage(null);
            }}
          />
        </section>

        <section aria-labelledby="detail-heading">
          <h2 id="detail-heading">Selected workshop</h2>
          {selectedWorkshop ? (
            <WorkshopDetail
              key={selectedWorkshop.id}
              workshop={selectedWorkshop}
              registrations={registrations}
              participants={participants}
              pending={pending}
              onRegister={(participantId) => register(selectedWorkshop.id, participantId)}
              onCancel={cancel}
            />
          ) : (
            <p>Choose “View” on a workshop to see its confirmed registrations and waitlist.</p>
          )}
        </section>

        <section aria-labelledby="schedule-heading">
          <h2 id="schedule-heading">Participant schedule</h2>
          {scheduleParticipantId !== null ? (
            <ParticipantSchedule
              participants={participants}
              selectedId={scheduleParticipantId}
              schedule={schedule}
              onSelect={setScheduleParticipantId}
            />
          ) : (
            <p>No participants found.</p>
          )}
        </section>
      </main>
    </>
  );
}
