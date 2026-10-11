export type RegistrationStatus = "Confirmed" | "Waitlisted" | "Cancelled";

export interface WorkshopDto {
  id: number;
  title: string;
  startsAt: string;
  endsAt: string;
  capacity: number;
  confirmedCount: number;
  waitlistCount: number;
  remainingCapacity: number;
}

export interface ParticipantDto {
  id: number;
  name: string;
  email: string;
}

export interface RegistrationDto {
  id: number;
  workshopId: number;
  workshopTitle: string;
  workshopStartsAt: string;
  workshopEndsAt: string;
  participantId: number;
  participantName: string;
  status: RegistrationStatus;
  createdAt: string;
}

export interface CancelRegistrationResponse {
  registration: RegistrationDto;
  promoted: RegistrationDto | null;
}
