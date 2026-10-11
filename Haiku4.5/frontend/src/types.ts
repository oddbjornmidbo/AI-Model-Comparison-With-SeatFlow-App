export interface Workshop {
  id: number;
  title: string;
  startTime: string;
  endTime: string;
  capacity: number;
  confirmedCount: number;
  waitlistCount: number;
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
  participantName: string;
  participantEmail: string;
  status: 'Confirmed' | 'Waitlisted' | 'Cancelled';
  createdAt: string;
}

export interface ScheduleItem {
  registrationId: number;
  workshopId: number;
  workshopTitle: string;
  startTime: string;
  endTime: string;
}
