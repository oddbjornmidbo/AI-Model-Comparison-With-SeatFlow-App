import type { RegistrationStatus } from '../types';

export interface StatusBadgeProps {
  status: RegistrationStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  const marker = status === 'Confirmed' ? '✓' : status === 'Waitlisted' ? '◷' : '×';
  return (
    <span className={`status-badge status-${status.toLowerCase()}`} aria-label={`Status: ${status}`}>
      <span aria-hidden="true" className="status-marker">{marker}</span>
      {status}
    </span>
  );
}
