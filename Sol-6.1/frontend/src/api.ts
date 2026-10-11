export interface Workshop {
  id: number;
  title: string;
  startTime: string;
  endTime: string;
  capacity: number;
  confirmedCount: number;
  remainingCapacity: number;
  waitlistedCount: number;
}

export interface Participant {
  id: number;
  name: string;
  email: string;
}

export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled';

export interface Registration {
  id: number;
  workshopId: number;
  participantId: number;
  participantName: string;
  participantEmail: string;
  status: RegistrationStatus;
  createdAt: string;
}

export interface ScheduleItem {
  registrationId: number;
  workshopId: number;
  title: string;
  startTime: string;
  endTime: string;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

export async function request<T>(path: string, options?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/api${path}`, {
      ...options,
      headers: {
        Accept: 'application/json',
        ...(options?.body ? { 'Content-Type': 'application/json' } : {}),
        ...options?.headers,
      },
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new ApiError('Unable to reach SeatFlow. Check that the backend is running, then retry.', 0);
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemDetails | null;
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new ApiError(validation || problem?.detail || problem?.title || `Request failed (${response.status}).`, response.status);
  }

  return response.json() as Promise<T>;
}

const attemptsStorageKey = 'seatflow.pending-registration-keys';

function readAttempts(): Record<string, string> {
  try {
    const value: unknown = JSON.parse(sessionStorage.getItem(attemptsStorageKey) || '{}');
    if (typeof value !== 'object' || value === null || Array.isArray(value)) return {};
    return Object.fromEntries(Object.entries(value).filter((entry): entry is [string, string] => typeof entry[1] === 'string'));
  } catch {
    return {};
  }
}

// Keep a key until the outcome is known, including through a page reload.
// If storage is disabled the same retry behavior still works for this page's lifetime.
const pendingAttempts = readAttempts();

function saveAttempts() {
  try {
    sessionStorage.setItem(attemptsStorageKey, JSON.stringify(pendingAttempts));
  } catch {
    // Registration can still proceed when browser storage is unavailable.
  }
}

export async function register(workshopId: number, participantId: number): Promise<Registration> {
  const attemptId = `${workshopId}:${participantId}`;
  const key = pendingAttempts[attemptId] ?? crypto.randomUUID();
  pendingAttempts[attemptId] = key;
  saveAttempts();

  try {
    const result = await request<Registration>(`/workshops/${workshopId}/registrations`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({ participantId }),
    });
    delete pendingAttempts[attemptId];
    saveAttempts();
    return result;
  } catch (error) {
    // A client error is definitive. Network/server failures may have happened after a commit.
    if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
      delete pendingAttempts[attemptId];
      saveAttempts();
    }
    throw error;
  }
}
