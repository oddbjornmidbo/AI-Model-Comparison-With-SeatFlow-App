import { ArrowUpRight, Code2, Layers3, Lightbulb, Palette, Users, Workflow } from 'lucide-react';
import type { Workshop } from '../types';
import { accessibleWorkshopRange } from '../workshopDates';
import { WorkshopTiming } from './WorkshopTiming';

export interface WorkshopCardProps {
  workshop: Workshop;
  selected?: boolean;
  onSelect: (workshop: Workshop) => void;
}

export function WorkshopCard({ workshop, selected = false, onSelect }: WorkshopCardProps) {
  const Icon = [Code2, Palette, Workflow, Lightbulb, Layers3][(workshop.id - 1) % 5] || Layers3;
  const percentage = workshop.capacity ? Math.min(100, workshop.confirmedCount / workshop.capacity * 100) : 0;
  const full = workshop.remainingSeats === 0;
  const nearlyFull = workshop.remainingSeats > 0 && workshop.remainingSeats <= Math.max(1, workshop.capacity * .25);
  return (
    <button
      type="button"
      className={`workshop-card color-${(workshop.id - 1) % 4}${selected ? ' selected' : ''}`}
      onClick={() => onSelect(workshop)}
      aria-pressed={selected}
      aria-label={`${workshop.title}, ${accessibleWorkshopRange(workshop)}, ${full ? 'full, join waitlist' : `${workshop.remainingSeats} seats available`}`}
    >
      <div className="workshop-card-top">
        <span className="workshop-icon">
          <Icon size={25} strokeWidth={1.75} aria-hidden="true" />
        </span>
        <span className={`availability ${full ? 'full' : nearlyFull ? 'limited' : 'available'}`}>
          <span className="availability-dot" />
          {full ? 'Waitlist open' : nearlyFull ? 'Filling up' : 'Seats available'}
        </span>
      </div>
      <h3>{workshop.title}</h3>
      <WorkshopTiming workshop={workshop} className="workshop-meta" />
      <div className="seat-information">
        <span>
          <Users size={15} aria-hidden="true" />
          <strong>{workshop.confirmedCount}</strong> / {workshop.capacity} registered
        </span>
        <strong className={full ? 'full-text' : ''}>
          {full ? 'Full' : `${workshop.remainingSeats} left`}
        </strong>
      </div>
      <div className={`capacity-track${full ? ' is-full' : ''}`} aria-hidden="true">
        <span style={{ width: `${percentage}%` }} />
      </div>
      <div className="workshop-card-bottom">
        <span>
          {workshop.waitlistedCount > 0 ? `${workshop.waitlistedCount} on the waitlist` : 'No one on the waitlist'}
        </span>
        <span className="card-open">
          {selected ? 'Selected' : 'View workshop'}
          <ArrowUpRight size={15} aria-hidden="true" />
        </span>
      </div>
    </button>
  );
}
