export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled';

export interface Workshop {
  id: string;
  title: string;
  startTime: string;
  endTime: string;
  capacity: number;
  confirmedCount: number;
  remainingCapacity: number;
}

export interface Participant {
  id: string;
  name: string;
  email: string;
}

export interface Registration {
  id: string;
  workshopId: string;
  participantId: string;
  participantName: string;
  participantEmail: string;
  status: RegistrationStatus;
  createdAt: string;
}

export interface ScheduleItem {
  registrationId: string;
  workshopId: string;
  workshopTitle: string;
  startTime: string;
  endTime: string;
  status: RegistrationStatus;
}

export interface SeatFlowClient {
  getWorkshops(): Promise<Workshop[]>;
  getParticipants(): Promise<Participant[]>;
  getRegistrations(workshopId: string): Promise<Registration[]>;
  getSchedule(participantId: string): Promise<ScheduleItem[]>;
  register(workshopId: string, participantId: string, idempotencyKey: string): Promise<Registration>;
  cancel(registrationId: string): Promise<Registration>;
}
