export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled';

export interface Workshop {
  id: number;
  title: string;
  startTime: string;
  endTime: string;
  capacity: number;
  confirmedCount: number;
  waitlistedCount: number;
  remainingCapacity: number;
}

export interface Participant {
  id: number;
  name: string;
  email: string;
}

export interface Registration {
  id: number;
  workshopId: number;
  workshopTitle: string;
  workshopStartTime: string;
  workshopEndTime: string;
  participantId: number;
  participantName: string;
  participantEmail: string;
  status: RegistrationStatus;
  createdAt: string;
}

export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      // ProblemDetails: `detail` for business errors, `errors` for validation errors.
      const body = await response.json();
      if (body.errors) {
        message = Object.values(body.errors).flat().join(' ');
      } else if (body.detail) {
        message = body.detail;
      }
    } catch {
      // Keep the status-based message when the body is not JSON.
    }
    throw new ApiError(message, response.status);
  }

  return response.json() as Promise<T>;
}

export const getWorkshops = () => request<Workshop[]>('/api/workshops');

export const getParticipants = () => request<Participant[]>('/api/participants');

export const getWorkshopRegistrations = (workshopId: number) =>
  request<Registration[]>(`/api/workshops/${workshopId}/registrations`);

export const registerParticipant = (workshopId: number, participantId: number, idempotencyKey: string) =>
  request<Registration>(`/api/workshops/${workshopId}/registrations`, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify({ participantId }),
  });

export const cancelRegistration = (registrationId: number) =>
  request<Registration>(`/api/registrations/${registrationId}/cancel`, { method: 'POST' });

export const getSchedule = (participantId: number) =>
  request<Registration[]>(`/api/participants/${participantId}/schedule`);
