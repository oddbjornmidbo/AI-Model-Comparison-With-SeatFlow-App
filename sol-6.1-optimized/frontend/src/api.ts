import type { CancelResult, Participant, Registration, Workshop, WorkshopInput } from './types';

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...options?.headers },
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
    throw new ApiError(problem?.detail || problem?.title || `Request failed (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}

export const api = {
  workshops: () => request<Workshop[]>('/workshops'),
  participants: () => request<Participant[]>('/participants'),
  registrations: (id: number) => request<Registration[]>(`/workshops/${id}/registrations`),
  schedule: (id: number) => request<Registration[]>(`/participants/${id}/schedule`),
  register: (workshopId: number, participantId: number, key: string) => request<Registration>(
    `/workshops/${workshopId}/registrations`,
    { method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify({ participantId }) },
  ),
  cancel: (id: number) => request<CancelResult>(`/registrations/${id}/cancel`, { method: 'POST' }),
  createWorkshop: (input: WorkshopInput, key: string) => request<Workshop>('/workshops', {
    method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify(input),
  }),
};
