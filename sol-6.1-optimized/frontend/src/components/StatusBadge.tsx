import { Check, Clock3, Minus } from 'lucide-react';
import type { Registration } from '../types';

export interface StatusBadgeProps { status: Registration['status']; }

export function StatusBadge({ status }: StatusBadgeProps) {
  const Icon = status === 'Confirmed' ? Check : status === 'Waitlisted' ? Clock3 : Minus;
  return <span className={`status-badge status-${status.toLowerCase()}`}><Icon size={12} strokeWidth={2.3} aria-hidden="true" />{status}</span>;
}
