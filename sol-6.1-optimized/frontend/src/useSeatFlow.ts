import { useCallback, useEffect, useRef, useState } from 'react';
import { api, ApiError } from './api';
import type { Participant, PendingAttempt, PendingWorkshopCreation, Registration, Workshop, WorkshopInput } from './types';

export interface Snapshot {
  workshops: Workshop[];
  participants: Participant[];
  registrations: Record<number, Registration[]>;
}

export interface Notice {
  kind: 'success' | 'error' | 'warning';
  title: string;
  message: string;
  workshopId?: number;
  operation?: 'creation';
}

const attemptStorageKey = 'seatflow.pending-attempts.v1';
const creationStorageKey = 'seatflow.pending-workshop.v1';
const pairKey = (workshopId: number, participantId: number) => `${workshopId}:${participantId}`;

function loadAttempts(): Record<string, PendingAttempt> {
  try {
    const parsed: unknown = JSON.parse(sessionStorage.getItem(attemptStorageKey) || '{}');
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};
    return Object.fromEntries(Object.entries(parsed).filter(([pair, value]) => {
      const item = value as Partial<PendingAttempt> | null;
      return item && Number.isInteger(item.workshopId) && Number.isInteger(item.participantId)
        && typeof item.key === 'string' && item.key.length > 0
        && pair === pairKey(item.workshopId!, item.participantId!);
    })) as Record<string, PendingAttempt>;
  } catch { return {}; }
}

function loadPendingCreation(): PendingWorkshopCreation | null {
  try {
    const parsed = JSON.parse(sessionStorage.getItem(creationStorageKey) || 'null') as Partial<PendingWorkshopCreation> | null;
    const input = parsed?.input;
    if (!input || typeof parsed?.key !== 'string' || !parsed.key.length
      || typeof input.title !== 'string' || !input.title.trim() || input.title.length > 200
      || typeof input.start !== 'string' || typeof input.end !== 'string'
      || !Number.isFinite(Date.parse(input.start)) || !(Date.parse(input.end) > Date.parse(input.start))
      || !Number.isInteger(input.capacity) || input.capacity < 1 || input.capacity > 10000) return null;
    return { key: parsed.key, input };
  } catch { return null; }
}

function sameWorkshopInput(left: WorkshopInput, right: WorkshopInput) {
  return left.title.trim() === right.title.trim() && left.capacity === right.capacity
    && Date.parse(left.start) === Date.parse(right.start) && Date.parse(left.end) === Date.parse(right.end);
}

export function useSeatFlow() {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [stale, setStale] = useState(false);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [attempts, setAttempts] = useState(loadAttempts);
  const attemptsRef = useRef(attempts);
  const [pendingCreation, setPendingCreation] = useState(loadPendingCreation);
  const pendingCreationRef = useRef(pendingCreation);
  const generation = useRef(0);
  const mounted = useRef(true);
  // React state updates are asynchronous: a ref also blocks double submission
  // from events dispatched before the disabled button has rendered.
  const mutationInFlight = useRef(false);

  const saveAttempts = useCallback((next: Record<string, PendingAttempt>) => {
    attemptsRef.current = next;
    setAttempts(next);
    try { sessionStorage.setItem(attemptStorageKey, JSON.stringify(next)); } catch { /* Storage may be unavailable. */ }
  }, []);

  const savePendingCreation = useCallback((next: PendingWorkshopCreation | null) => {
    pendingCreationRef.current = next;
    setPendingCreation(next);
    try {
      if (next) sessionStorage.setItem(creationStorageKey, JSON.stringify(next));
      else sessionStorage.removeItem(creationStorageKey);
    } catch { /* Storage may be unavailable; the in-memory attempt still supports retry. */ }
  }, []);

  const refresh = useCallback(async () => {
    const requestGeneration = ++generation.current;
    try {
      const [workshops, participants] = await Promise.all([api.workshops(), api.participants()]);
      const rosters = await Promise.all(workshops.map(async workshop => [workshop.id, await api.registrations(workshop.id)] as const));
      if (!mounted.current || requestGeneration !== generation.current) return null;
      // Replace the complete snapshot only when every derived view has loaded.
      // Selection is local, so old selection requests cannot overwrite a new one.
      const latest = { workshops, participants, registrations: Object.fromEntries(rosters) };
      setSnapshot(latest);
      setStale(false);
      return latest;
    } catch (error) {
      if (!mounted.current || requestGeneration !== generation.current) return null;
      throw error;
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    const initialLoad = refresh();
    const initialGeneration = generation.current;
    void initialLoad.catch(error => {
      if (mounted.current && initialGeneration === generation.current) setNotice({ kind: 'error', title: 'Unable to load the conference', message: errorMessage(error) });
    }).finally(() => { if (mounted.current && initialGeneration === generation.current) setLoading(false); });
    return () => { mounted.current = false; ++generation.current; };
  }, [refresh]);

  const reload = useCallback(async () => {
    if (mutationInFlight.current) return;
    setLoading(true);
    setNotice(null);
    try { await refresh(); }
    catch (error) {
      setStale(true);
      setNotice({ kind: 'error', title: 'Latest information could not be loaded', message: `${errorMessage(error)} ${snapshot ? 'The displayed information may be out of date. Refresh before making changes.' : 'Check that the local API is running, then try again.'}` });
    } finally { setLoading(false); }
  }, [refresh, snapshot]);

  const afterMutation = useCallback(async (success: Notice, currentNotice?: (latest: Snapshot) => Notice) => {
    try {
      const latest = await refresh();
      if (latest) setNotice(currentNotice ? currentNotice(latest) : success);
    } catch (error) {
      setStale(true);
      setNotice({
        ...success,
        kind: 'warning',
        title: `${success.title} · refresh needed`,
        message: `${success.message} The change was saved, but the latest lists and schedules could not be loaded. ${errorMessage(error)} Refresh to continue; do not repeat the saved action.`,
      });
    }
  }, [refresh]);

  const register = useCallback(async (workshopId: number, participantId: number) => {
    if (mutationInFlight.current || stale) return;
    mutationInFlight.current = true;
    setBusy(true);
    setNotice(null);
    const pair = pairKey(workshopId, participantId);
    const attempt = attemptsRef.current[pair] || { workshopId, participantId, key: crypto.randomUUID() };
    saveAttempts({ ...attemptsRef.current, [pair]: attempt });
    try {
      const result = await api.register(workshopId, participantId, attempt.key);
      const next = { ...attemptsRef.current };
      delete next[pair];
      saveAttempts(next);
      await afterMutation({ kind: 'success', workshopId, title: 'Registration saved', message: `${result.participant.name}'s registration for ${result.workshop.title} was saved.` }, latest => {
        // Idempotent replay intentionally returns the original response. Another
        // request may have promoted/cancelled it since then; use the live row.
        const current = latest.registrations[workshopId]?.find(item => item.id === result.id) || result;
        return {
          kind: 'success',
          workshopId,
          title: current.status === 'Confirmed' ? 'Seat confirmed' : current.status === 'Waitlisted' ? 'Added to the waitlist' : 'Previous attempt resolved',
          message: current.status === 'Confirmed' ? `${current.participant.name} is registered for ${current.workshop.title}.`
            : current.status === 'Waitlisted' ? `${current.participant.name} joined the waitlist for ${current.workshop.title}. When a confirmed registration is cancelled, the first waiting participant without a schedule conflict receives the seat.`
            : `${current.participant.name}'s registration for ${current.workshop.title} has already been cancelled. You can now make a new registration.`,
        };
      });
      return result;
    } catch (error) {
      const definitive = definitiveRejection(error);
      if (definitive) {
        const next = { ...attemptsRef.current }; delete next[pair]; saveAttempts(next);
      }
      setNotice({
        workshopId,
        kind: definitive ? 'error' : 'warning',
        title: definitive ? 'Registration could not be completed' : 'Registration outcome is uncertain',
        message: definitive ? errorMessage(error) : `${errorMessage(error)} Retry this registration to safely check the same attempt.`,
      });
    } finally { mutationInFlight.current = false; setBusy(false); }
  }, [afterMutation, saveAttempts, stale]);

  const cancel = useCallback(async (registration: Registration) => {
    if (mutationInFlight.current || stale) return;
    mutationInFlight.current = true;
    setBusy(true);
    setNotice(null);
    try {
      const result = await api.cancel(registration.id);
      const next = { ...attemptsRef.current };
      delete next[pairKey(registration.workshopId, registration.participantId)];
      saveAttempts(next);
      await afterMutation({
        workshopId: registration.workshopId,
        kind: 'success', title: 'Registration cancelled',
        message: `${registration.participant.name}'s registration for ${registration.workshop.title} was cancelled.${result.promotedRegistration ? ` ${result.promotedRegistration.participant.name} moved from the waitlist into the available seat.` : ''}`,
      });
    } catch (error) {
      setNotice({ workshopId: registration.workshopId, kind: 'error', title: 'Cancellation could not be verified', message: `${errorMessage(error)} You can safely retry cancellation for ${registration.workshop.title}; cancelling twice does not free another seat.` });
    } finally { mutationInFlight.current = false; setBusy(false); }
  }, [afterMutation, saveAttempts, stale]);

  const createWorkshop = useCallback(async (input: WorkshopInput) => {
    if (mutationInFlight.current || stale) return false;
    const previous = pendingCreationRef.current;
    if (previous && !sameWorkshopInput(previous.input, input)) {
      setNotice({ operation: 'creation', kind: 'warning', title: 'Resolve the previous workshop first', message: `Retry creation of ${previous.input.title} to check its saved outcome before starting another workshop.` });
      return false;
    }
    mutationInFlight.current = true;
    setBusy(true);
    setNotice(null);
    const attempt = previous || { input, key: crypto.randomUUID() };
    savePendingCreation(attempt);
    try {
      const result = await api.createWorkshop(attempt.input, attempt.key);
      savePendingCreation(null);
      await afterMutation({ operation: 'creation', kind: 'success', title: 'Workshop created', message: `${result.title} is ready for registrations.` });
      return true;
    } catch (error) {
      const definitive = definitiveRejection(error);
      if (definitive) savePendingCreation(null);
      setNotice({
        operation: 'creation', kind: definitive ? 'error' : 'warning',
        title: definitive ? 'Workshop could not be created' : 'Workshop creation outcome is uncertain',
        message: definitive ? errorMessage(error) : `${errorMessage(error)} Retry creation of ${attempt.input.title} to safely check this same attempt. Its details are kept, even after a refresh.`,
      });
      return false;
    } finally { mutationInFlight.current = false; setBusy(false); }
  }, [afterMutation, savePendingCreation, stale]);

  return { snapshot, loading, busy, stale, notice, attempts, pendingCreation, reload, register, cancel, createWorkshop, dismissNotice: () => setNotice(null) };
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Please check your connection and try again.';
}

function definitiveRejection(error: unknown) {
  // A gateway timeout or rate limit does not settle an earlier lost response.
  return error instanceof ApiError && error.status >= 400 && error.status < 500
    && error.status !== 408 && error.status !== 429;
}

export function registrationAvailability(workshop: Workshop, participant: Participant | undefined, roster: Registration[], schedule: Registration[], pending: boolean) {
  if (!participant) return { disabled: true, hint: 'Choose a participant to register for this workshop.' };
  if (pending) return { disabled: false, hint: 'The previous response was lost. Retry safely to resolve that same registration attempt.' };
  const existing = roster.find(item => item.participantId === participant.id && item.status !== 'Cancelled');
  if (existing) return { disabled: true, hint: existing.status === 'Confirmed' ? `${participant.name} already has a confirmed seat.` : `${participant.name} is already on this waitlist.` };
  if (workshop.remainingSeats === 0) return { disabled: false, hint: 'This workshop is full. Registration will join the waitlist, in order of arrival.' };
  const conflict = schedule.find(item => item.status === 'Confirmed' && item.workshopId !== workshop.id
    && Date.parse(item.workshop.start) < Date.parse(workshop.end) && Date.parse(workshop.start) < Date.parse(item.workshop.end));
  if (conflict) return { disabled: true, hint: `Schedule conflict with ${conflict.workshop.title}. Cancel that registration before reserving this seat.` };
  return { disabled: false, hint: 'A seat is available. Registration will be confirmed immediately.' };
}
