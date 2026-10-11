import type { Participant, Registration, ScheduleItem, SeatFlowClient, Workshop } from './types';

const apiBase = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/$/, '');

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });
  if (!response.ok) {
    let message = `Request failed (${response.status}).`;
    try {
      const body = (await response.json()) as { error?: string; title?: string };
      message = body.error ?? body.title ?? message;
    } catch {
      // Keep the HTTP status message when a server response has no JSON body.
    }
    throw new Error(message);
  }
  return response.json() as Promise<T>;
}

export const api: SeatFlowClient = {
  getWorkshops: () => request<Workshop[]>('/api/workshops'),
  getParticipants: () => request<Participant[]>('/api/participants'),
  getRegistrations: (workshopId) => request<Registration[]>(`/api/workshops/${workshopId}/registrations`),
  getSchedule: (participantId) => request<ScheduleItem[]>(`/api/participants/${participantId}/schedule`),
  register: (workshopId, participantId, idempotencyKey) => request<Registration>(`/api/workshops/${workshopId}/registrations`, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify({ participantId }),
  }),
  cancel: (registrationId) => request<Registration>(`/api/registrations/${registrationId}/cancel`, { method: 'POST' }),
};
