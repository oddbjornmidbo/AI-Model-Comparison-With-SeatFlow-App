import type {
  CancelRegistrationResponse,
  ParticipantDto,
  RegistrationDto,
  WorkshopDto,
} from "./types";

/** An HTTP error from the API, carrying the server's human-readable message. */
export class ApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

interface ProblemBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

async function request<T>(
  path: string,
  method = "GET",
  body?: unknown,
  headers: Record<string, string> = {},
): Promise<T> {
  const response = await fetch(`/api${path}`, {
    method,
    headers: {
      Accept: "application/json",
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblemMessage(response));
  }
  return (await response.json()) as T;
}

async function readProblemMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as ProblemBody;
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(" ");
    }
    return problem.detail ?? problem.title ?? response.statusText;
  } catch {
    return response.statusText || `Request failed with status ${response.status}.`;
  }
}

export const api = {
  listWorkshops: () => request<WorkshopDto[]>("/workshops"),
  listParticipants: () => request<ParticipantDto[]>("/participants"),
  listRegistrations: (workshopId: number) =>
    request<RegistrationDto[]>(`/workshops/${workshopId}/registrations`),
  schedule: (participantId: number) =>
    request<RegistrationDto[]>(`/participants/${participantId}/schedule`),
  register: (workshopId: number, participantId: number, idempotencyKey: string) =>
    request<RegistrationDto>(
      `/workshops/${workshopId}/registrations`,
      "POST",
      { participantId },
      { "Idempotency-Key": idempotencyKey },
    ),
  cancel: (registrationId: number) =>
    request<CancelRegistrationResponse>(`/registrations/${registrationId}/cancel`, "POST"),
};
