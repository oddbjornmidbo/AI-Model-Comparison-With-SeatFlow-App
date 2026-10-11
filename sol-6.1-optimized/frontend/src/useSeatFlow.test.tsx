import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError } from './api';
import { registrationAvailability, useSeatFlow } from './useSeatFlow';
import type { Participant, Registration, Workshop, WorkshopInput } from './types';

vi.mock('./api', async () => {
  const actual = await vi.importActual<typeof import('./api')>('./api');
  return { ...actual, api: { workshops: vi.fn(), participants: vi.fn(), registrations: vi.fn(), schedule: vi.fn(), register: vi.fn(), cancel: vi.fn(), createWorkshop: vi.fn() } };
});

const participant: Participant = { id: 1, name: 'Maya Chen', email: 'maya@example.com' };
const second: Participant = { id: 2, name: 'Liam Olsen', email: 'liam@example.com' };
const workshop: Workshop = { id: 1, title: 'Designing for everyone', start: '2026-10-16T10:00:00Z', end: '2026-10-16T11:00:00Z', capacity: 1, confirmedCount: 0, waitlistedCount: 0, remainingSeats: 1 };
const registration: Registration = { id: 1, participantId: 1, workshopId: 1, participant, workshop, status: 'Confirmed', created: '2026-10-03T12:00:00Z' };

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  vi.mocked(api.workshops).mockResolvedValue([workshop]);
  vi.mocked(api.participants).mockResolvedValue([participant, second]);
  vi.mocked(api.registrations).mockResolvedValue([]);
  vi.mocked(api.register).mockResolvedValue(registration);
  vi.mocked(api.createWorkshop).mockResolvedValue(workshop);
});

async function loaded() {
  const hook = renderHook(() => useSeatFlow());
  await waitFor(() => expect(hook.result.current.loading).toBe(false));
  return hook;
}

describe('coordinated application state', () => {
  it('refreshes capacity and roster together after registration', async () => {
    const hook = await loaded();
    vi.mocked(api.workshops).mockResolvedValue([{ ...workshop, confirmedCount: 1, remainingSeats: 0 }]);
    vi.mocked(api.registrations).mockResolvedValue([registration]);
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.snapshot?.workshops[0].remainingSeats).toBe(0);
    expect(hook.result.current.snapshot?.registrations[1][0].status).toBe('Confirmed');
    expect(hook.result.current.notice?.kind).toBe('success');
    expect(hook.result.current.notice?.workshopId).toBe(1);
    expect(hook.result.current.attempts).toEqual({});
  });

  it('refreshes promoted participants along with the cancelled registration', async () => {
    const hook = await loaded();
    const cancelled = { ...registration, status: 'Cancelled' as const };
    const promoted = { ...registration, id: 2, participant: second, participantId: 2 };
    vi.mocked(api.cancel).mockResolvedValue({ registration: cancelled, promotedRegistration: promoted });
    vi.mocked(api.registrations).mockResolvedValue([cancelled, promoted]);
    await act(async () => { await hook.result.current.cancel(registration); });
    expect(hook.result.current.snapshot?.registrations[1]).toEqual([cancelled, promoted]);
    expect(hook.result.current.notice?.message).toContain('Liam Olsen moved from the waitlist');
  });

  it('reuses the key after a lost response and clears it once resolved', async () => {
    const hook = await loaded();
    vi.mocked(api.register).mockRejectedValueOnce(new TypeError('Connection lost'));
    await act(async () => { await hook.result.current.register(1, 1); });
    const firstKey = vi.mocked(api.register).mock.calls[0][2];
    expect(hook.result.current.attempts['1:1'].key).toBe(firstKey);
    expect(hook.result.current.notice?.title).toContain('uncertain');
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(vi.mocked(api.register).mock.calls[1][2]).toBe(firstKey);
    expect(hook.result.current.attempts).toEqual({});
    expect(hook.result.current.notice?.kind).toBe('success');
  });

  it('retains uncertain attempts across component remounts and participant changes', async () => {
    const hook = await loaded();
    vi.mocked(api.register).mockRejectedValueOnce(new Error('Lost response'));
    await act(async () => { await hook.result.current.register(1, 1); });
    const key = vi.mocked(api.register).mock.calls[0][2];
    hook.unmount();
    const reopened = await loaded();
    await act(async () => { await reopened.result.current.register(1, 2); });
    await act(async () => { await reopened.result.current.register(1, 1); });
    expect(vi.mocked(api.register).mock.calls[2][2]).toBe(key);
  });

  it('uses the current cancelled status after replaying an originally confirmed response', async () => {
    const hook = await loaded();
    vi.mocked(api.register).mockRejectedValueOnce(new Error('Lost response'));
    await act(async () => { await hook.result.current.register(1, 1); });
    vi.mocked(api.registrations).mockResolvedValue([{ ...registration, status: 'Cancelled' }]);
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.notice?.title).toBe('Previous attempt resolved');
    expect(hook.result.current.notice?.message).toContain('already been cancelled');
    expect(hook.result.current.attempts).toEqual({});
  });

  it('uses the current confirmed status after replaying an originally waitlisted response', async () => {
    const hook = await loaded();
    vi.mocked(api.register).mockResolvedValue({ ...registration, status: 'Waitlisted' });
    vi.mocked(api.registrations).mockResolvedValue([registration]);
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.notice?.title).toBe('Seat confirmed');
  });

  it('makes a new logical attempt after cancellation', async () => {
    const hook = await loaded();
    await act(async () => { await hook.result.current.register(1, 1); });
    const key = vi.mocked(api.register).mock.calls[0][2];
    vi.mocked(api.cancel).mockResolvedValue({ registration: { ...registration, status: 'Cancelled' }, promotedRegistration: null });
    await act(async () => { await hook.result.current.cancel(registration); });
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(vi.mocked(api.register).mock.calls[1][2]).not.toBe(key);
  });

  it('clears a definitively rejected attempt and replaces stale error feedback', async () => {
    const hook = await loaded();
    vi.mocked(api.register).mockRejectedValueOnce(new ApiError('Schedule conflict', 409));
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.attempts).toEqual({});
    const rejectedKey = vi.mocked(api.register).mock.calls[0][2];
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.notice?.title).toBe('Seat confirmed');
    expect(vi.mocked(api.register).mock.calls[1][2]).not.toBe(rejectedKey);
  });

  it('distinguishes a saved mutation from failed derived refresh and preserves the prior snapshot', async () => {
    const hook = await loaded();
    const original = hook.result.current.snapshot;
    vi.mocked(api.registrations).mockRejectedValueOnce(new Error('Roster unavailable'));
    await act(async () => { await hook.result.current.register(1, 1); });
    expect(hook.result.current.snapshot).toBe(original);
    expect(hook.result.current.notice?.kind).toBe('warning');
    expect(hook.result.current.notice?.message).toContain('The change was saved');
    expect(hook.result.current.stale).toBe(true);
    expect(hook.result.current.attempts).toEqual({});
    await act(async () => { await hook.result.current.register(1, 2); });
    expect(api.register).toHaveBeenCalledTimes(1);
    vi.mocked(api.registrations).mockResolvedValue([registration]);
    await act(async () => { await hook.result.current.reload(); });
    expect(hook.result.current.stale).toBe(false);
    expect(hook.result.current.notice).toBeNull();
    expect(hook.result.current.snapshot?.registrations[1]).toEqual([registration]);
  });

  it('blocks double submissions before React has rendered a disabled button', async () => {
    const hook = await loaded();
    const gate = deferred<Registration>();
    vi.mocked(api.register).mockReturnValue(gate.promise);
    let operation!: Promise<Registration | undefined>;
    await act(async () => {
      operation = hook.result.current.register(1, 1);
      void hook.result.current.register(1, 1);
    });
    expect(api.register).toHaveBeenCalledTimes(1);
    await act(async () => { gate.resolve(registration); await operation; });
  });

  it('prevents slower older refreshes from overwriting a newer snapshot', async () => {
    const first = deferred<Workshop[]>();
    vi.mocked(api.workshops).mockReturnValueOnce(first.promise).mockResolvedValue([{ ...workshop, title: 'New program' }]);
    const hook = renderHook(() => useSeatFlow());
    await act(async () => { await hook.result.current.reload(); });
    expect(hook.result.current.snapshot?.workshops[0].title).toBe('New program');
    await act(async () => { first.resolve([{ ...workshop, title: 'Old program' }]); });
    expect(hook.result.current.snapshot?.workshops[0].title).toBe('New program');
  });

  it('ignores an obsolete failed refresh after a newer successful load', async () => {
    const first = deferred<Workshop[]>();
    vi.mocked(api.workshops).mockReturnValueOnce(first.promise).mockResolvedValue([workshop]);
    const hook = renderHook(() => useSeatFlow());
    await act(async () => { await hook.result.current.reload(); });
    await act(async () => { first.reject(new Error('Obsolete error')); });
    expect(hook.result.current.notice).toBeNull();
    expect(hook.result.current.snapshot?.workshops).toEqual([workshop]);
  });
});

describe('workshop creation recovery', () => {
  const input: WorkshopInput = { title: workshop.title, start: workshop.start, end: workshop.end, capacity: workshop.capacity };

  it('retains original details and key after a lost response, including remount', async () => {
    const hook = await loaded();
    vi.mocked(api.createWorkshop).mockRejectedValueOnce(new TypeError('Connection lost'));
    await act(async () => { expect(await hook.result.current.createWorkshop(input)).toBe(false); });
    const key = vi.mocked(api.createWorkshop).mock.calls[0][1];
    expect(hook.result.current.pendingCreation).toEqual({ key, input });
    expect(hook.result.current.notice?.title).toBe('Workshop creation outcome is uncertain');
    hook.unmount();
    const reopened = await loaded();
    expect(reopened.result.current.pendingCreation).toEqual({ key, input });
    await act(async () => { expect(await reopened.result.current.createWorkshop(input)).toBe(true); });
    expect(vi.mocked(api.createWorkshop).mock.calls[1]).toEqual([input, key]);
    expect(reopened.result.current.pendingCreation).toBeNull();
    expect(reopened.result.current.notice?.title).toBe('Workshop created');
  });

  it('prevents replacing an unresolved request with different workshop details', async () => {
    const hook = await loaded();
    vi.mocked(api.createWorkshop).mockRejectedValueOnce(new Error('Lost response'));
    await act(async () => { await hook.result.current.createWorkshop(input); });
    await act(async () => { expect(await hook.result.current.createWorkshop({ ...input, title: 'Different workshop' })).toBe(false); });
    expect(api.createWorkshop).toHaveBeenCalledTimes(1);
    expect(hook.result.current.pendingCreation?.input).toEqual(input);
    expect(hook.result.current.notice?.title).toBe('Resolve the previous workshop first');
  });

  it.each([408, 429])('keeps a lost-response key when a retry receives transient HTTP %s', async status => {
    const hook = await loaded();
    vi.mocked(api.createWorkshop).mockRejectedValueOnce(new Error('Lost response'))
      .mockRejectedValueOnce(new ApiError('Try again shortly', status));
    await act(async () => { await hook.result.current.createWorkshop(input); });
    const originalKey = hook.result.current.pendingCreation?.key;
    await act(async () => { await hook.result.current.createWorkshop(input); });
    expect(hook.result.current.pendingCreation?.key).toBe(originalKey);
    expect(hook.result.current.notice?.kind).toBe('warning');
    await act(async () => { await hook.result.current.createWorkshop(input); });
    expect(vi.mocked(api.createWorkshop).mock.calls[2][1]).toBe(originalKey);
  });

  it('clears a definitively rejected creation and permits a new key', async () => {
    const hook = await loaded();
    vi.mocked(api.createWorkshop).mockRejectedValueOnce(new ApiError('Invalid capacity', 400));
    await act(async () => { await hook.result.current.createWorkshop(input); });
    const rejectedKey = vi.mocked(api.createWorkshop).mock.calls[0][1];
    expect(hook.result.current.pendingCreation).toBeNull();
    expect(hook.result.current.notice?.kind).toBe('error');
    await act(async () => { await hook.result.current.createWorkshop(input); });
    expect(vi.mocked(api.createWorkshop).mock.calls[1][1]).not.toBe(rejectedKey);
  });

  it('clears retry state when creation saved but subsequent refresh failed', async () => {
    const hook = await loaded();
    vi.mocked(api.workshops).mockRejectedValueOnce(new Error('Refresh unavailable'));
    await act(async () => { expect(await hook.result.current.createWorkshop(input)).toBe(true); });
    expect(hook.result.current.pendingCreation).toBeNull();
    expect(hook.result.current.stale).toBe(true);
    expect(hook.result.current.notice?.title).toBe('Workshop created · refresh needed');
    expect(hook.result.current.notice?.message).toContain(workshop.title);
  });

  it('blocks immediate double creation submissions', async () => {
    const hook = await loaded();
    const gate = deferred<Workshop>();
    vi.mocked(api.createWorkshop).mockReturnValue(gate.promise);
    let operation!: Promise<boolean>;
    await act(async () => {
      operation = hook.result.current.createWorkshop(input);
      void hook.result.current.createWorkshop(input);
    });
    expect(api.createWorkshop).toHaveBeenCalledTimes(1);
    await act(async () => { gate.resolve(workshop); await operation; });
  });

  it('ignores malformed saved creation state', async () => {
    sessionStorage.setItem('seatflow.pending-workshop.v1', JSON.stringify({ key: 'saved', input: { ...input, capacity: 0 } }));
    const hook = await loaded();
    expect(hook.result.current.pendingCreation).toBeNull();
  });
});

describe('registration guidance', () => {
  it('prevents active duplicates, but permits new registration after cancellation', () => {
    expect(registrationAvailability(workshop, participant, [registration], [], false).disabled).toBe(true);
    expect(registrationAvailability(workshop, participant, [{ ...registration, status: 'Cancelled' }], [], false).disabled).toBe(false);
  });

  it('prevents known overlapping confirmed workshops but permits touching boundaries', () => {
    const conflict = { ...registration, workshopId: 2, workshop: { ...workshop, id: 2 } };
    expect(registrationAvailability(workshop, participant, [], [conflict], false).hint).toContain('Schedule conflict');
    const touching = { ...conflict, workshop: { ...conflict.workshop, start: workshop.end, end: '2026-10-16T12:00:00Z' } };
    expect(registrationAvailability(workshop, participant, [], [touching], false).disabled).toBe(false);
  });

  it('permits waitlisting despite a confirmed conflict', () => {
    const conflict = { ...registration, workshopId: 2 };
    expect(registrationAvailability({ ...workshop, remainingSeats: 0 }, participant, [], [conflict], false)).toEqual({ disabled: false, hint: 'This workshop is full. Registration will join the waitlist, in order of arrival.' });
  });

  it('permits an unresolved idempotent retry even if a refresh finds an active registration', () => {
    expect(registrationAvailability(workshop, participant, [registration], [], true).disabled).toBe(false);
  });
});
