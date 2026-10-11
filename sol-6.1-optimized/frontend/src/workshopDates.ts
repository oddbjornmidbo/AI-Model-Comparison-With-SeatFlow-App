import type { Workshop } from './types';

type WorkshopInterval = Pick<Workshop, 'start' | 'end'>;

export function formatDate(value: string, timeZone?: string) {
  return new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone,
  }).format(new Date(value));
}

export function formatTime(value: string, timeZone?: string) {
  return new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
    timeZone,
  }).format(new Date(value));
}

function calendarParts(value: string, timeZone?: string) {
  const parts = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone,
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(p => p.type === type)?.value;
  return { day: part('day'), month: part('month'), year: part('year') };
}

function sameCalendarDate(start: ReturnType<typeof calendarParts>, end: ReturnType<typeof calendarParts>) {
  return start.day === end.day && start.month === end.month && start.year === end.year;
}

// Compare calendar dates in the same timezone used for display. UTC dates may
// differ while local dates match, or vice versa, particularly around midnight.
export function dateRange(workshop: WorkshopInterval, timeZone?: string) {
  const start = calendarParts(workshop.start, timeZone);
  const end = calendarParts(workshop.end, timeZone);
  if (sameCalendarDate(start, end))
    return formatDate(workshop.start, timeZone);

  if (start.month === end.month && start.year === end.year)
    return `${start.day} – ${formatDate(workshop.end, timeZone)}`;

  if (start.year === end.year)
    return `${start.day} ${start.month} – ${formatDate(workshop.end, timeZone)}`;

  return `${formatDate(workshop.start, timeZone)} – ${formatDate(workshop.end, timeZone)}`;
}

export function timeRange(workshop: WorkshopInterval, timeZone?: string) {
  const start = calendarParts(workshop.start, timeZone);
  const end = calendarParts(workshop.end, timeZone);
  if (sameCalendarDate(start, end))
    return `${formatTime(workshop.start, timeZone)} – ${formatTime(workshop.end, timeZone)}`;

  const endpoint = (value: string, parts: ReturnType<typeof calendarParts>) => {
    const date = start.year === end.year
      ? `${parts.day} ${parts.month}`
      : formatDate(value, timeZone);
    return `${date}, ${formatTime(value, timeZone)}`;
  };
  return `${endpoint(workshop.start, start)} – ${endpoint(workshop.end, end)}`;
}

export function accessibleWorkshopRange(workshop: WorkshopInterval, timeZone?: string) {
  return `${formatDate(workshop.start, timeZone)} at ${formatTime(workshop.start, timeZone)} to ${formatDate(workshop.end, timeZone)} at ${formatTime(workshop.end, timeZone)}`;
}
