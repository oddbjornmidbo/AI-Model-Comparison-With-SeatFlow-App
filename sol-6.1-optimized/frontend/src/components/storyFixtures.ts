import type { Participant, Registration, Workshop } from '../types';
export const workshops: Workshop[] = [
  { id: 1, title: 'Building thoughtful design systems', start: '2026-10-12T09:00:00Z', end: '2026-10-12T10:30:00Z', capacity: 12, confirmedCount: 7, waitlistedCount: 0, remainingSeats: 5 },
  { id: 2, title: 'The art of accessible interfaces', start: '2026-10-12T10:00:00Z', end: '2026-10-12T11:30:00Z', capacity: 2, confirmedCount: 2, waitlistedCount: 2, remainingSeats: 0 },
  { id: 3, title: 'Practical AI for everyday teams', start: '2026-10-12T10:30:00Z', end: '2026-10-12T12:00:00Z', capacity: 10, confirmedCount: 8, waitlistedCount: 0, remainingSeats: 2 },
  { id: 4, title: 'From a good idea to a great product', start: '2026-10-12T13:00:00Z', end: '2026-10-12T14:30:00Z', capacity: 16, confirmedCount: 3, waitlistedCount: 0, remainingSeats: 13 },
];

// Build these examples in the browser's local timezone so their calendar-date
// boundaries remain visible wherever the component library is opened.
export const overnightWorkshop: Workshop = {
  ...workshops[0],
  title: 'Designing through the night',
  start: new Date(2026, 9, 15, 23, 0).toISOString(),
  end: new Date(2026, 9, 16, 1, 0).toISOString(),
};

export const yearBoundaryWorkshop: Workshop = {
  ...workshops[0],
  title: 'A fresh year of ideas',
  start: new Date(2026, 11, 31, 23, 0).toISOString(),
  end: new Date(2027, 0, 1, 1, 0).toISOString(),
};
export const participants: Participant[] = [
  { id: 1, name: 'Sofia Berg', email: 'sofia.berg@example.com' },
  { id: 2, name: 'Liam Andersen', email: 'liam.andersen@example.com' },
  { id: 3, name: 'Amara Okafor', email: 'amara.okafor@example.com' },
  { id: 4, name: 'Noah Chen', email: 'noah.chen@example.com' },
  { id: 5, name: 'Ella Johansen', email: 'ella.johansen@example.com' },
];
export function registration(id: number, participant: Participant, workshop: Workshop, status: Registration['status']): Registration {
  return { id, participantId: participant.id, workshopId: workshop.id, participant, workshop, status, created: `2026-10-03T09:00:0${id}Z` };
}
export const registrations: Registration[] = [registration(1, participants[0], workshops[1], 'Confirmed'), registration(2, participants[1], workshops[1], 'Confirmed'), registration(3, participants[2], workshops[1], 'Waitlisted'), registration(4, participants[3], workshops[1], 'Waitlisted'), registration(5, participants[4], workshops[1], 'Cancelled')];
