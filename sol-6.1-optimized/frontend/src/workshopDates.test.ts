import { describe, expect, it } from 'vitest';
import { accessibleWorkshopRange, dateRange, timeRange } from './workshopDates';

describe('workshop dates', () => {
  it('keeps a same-day interval compact', () => {
    const interval = { start: '2026-10-15T09:00:00Z', end: '2026-10-15T10:30:00Z' };
    expect(dateRange(interval, 'UTC')).toBe('15 Oct 2026');
    expect(timeRange(interval, 'UTC')).toBe('09:00 – 10:30');
  });

  it('shows both dates for an overnight workshop', () => {
    const interval = { start: '2026-10-15T23:00:00Z', end: '2026-10-16T01:00:00Z' };
    expect(dateRange(interval, 'UTC')).toBe('15 – 16 Oct 2026');
    expect(timeRange(interval, 'UTC')).toBe('15 Oct, 23:00 – 16 Oct, 01:00');
    expect(accessibleWorkshopRange(interval, 'UTC'))
      .toBe('15 Oct 2026 at 23:00 to 16 Oct 2026 at 01:00');
  });

  it('includes both month names at a month boundary', () => {
    expect(dateRange({ start: '2026-10-31T23:00:00Z', end: '2026-11-01T01:00:00Z' }, 'UTC'))
      .toBe('31 Oct – 1 Nov 2026');
  });

  it('includes both years at a year boundary', () => {
    const interval = { start: '2026-12-31T23:00:00Z', end: '2027-01-01T01:00:00Z' };
    expect(dateRange(interval, 'UTC')).toBe('31 Dec 2026 – 1 Jan 2027');
    expect(timeRange(interval, 'UTC')).toBe('31 Dec 2026, 23:00 – 1 Jan 2027, 01:00');
  });

  it('compares local dates rather than UTC dates', () => {
    const sameUtcDay = { start: '2026-10-15T21:30:00Z', end: '2026-10-15T23:30:00Z' };
    expect(dateRange(sameUtcDay, 'Europe/Oslo')).toBe('15 – 16 Oct 2026');
    expect(timeRange(sameUtcDay, 'Europe/Oslo')).toBe('15 Oct, 23:30 – 16 Oct, 01:30');

    const differentUtcDays = { start: '2026-10-15T23:30:00Z', end: '2026-10-16T01:30:00Z' };
    expect(dateRange(differentUtcDays, 'Europe/Oslo')).toBe('16 Oct 2026');
  });

  it('preserves the date for workshops spanning more than one night', () => {
    expect(dateRange({ start: '2026-10-15T09:00:00Z', end: '2026-10-18T17:00:00Z' }, 'UTC'))
      .toBe('15 – 18 Oct 2026');
  });

  it('formats the local clock when an overnight workshop crosses the daylight-saving change', () => {
    const interval = { start: '2026-10-24T21:00:00Z', end: '2026-10-25T02:00:00Z' };
    expect(dateRange(interval, 'Europe/Oslo')).toBe('24 – 25 Oct 2026');
    expect(timeRange(interval, 'Europe/Oslo')).toBe('24 Oct, 23:00 – 25 Oct, 03:00');
  });
});
