export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled';

export interface Workshop {
  id: number;
  title: string;
  start: string;
  end: string;
  capacity: number;
  confirmedCount: number;
  waitlistedCount: number;
  remainingSeats: number;
}

export interface Participant {
  id: number;
  name: string;
  email: string;
}

export interface Registration {
  id: number;
  workshopId: number;
  participantId: number;
  status: RegistrationStatus;
  created: string;
  participant: Participant;
  workshop: Workshop;
}

export interface CancelResult {
  registration: Registration;
  promotedRegistration: Registration | null;
}

export interface WorkshopInput {
  title: string;
  start: string;
  end: string;
  capacity: number;
}

export interface PendingAttempt {
  workshopId: number;
  participantId: number;
  key: string;
}

export interface PendingWorkshopCreation {
  input: WorkshopInput;
  key: string;
}
