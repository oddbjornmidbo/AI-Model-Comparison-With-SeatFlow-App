import type { Workshop } from '../types';

export interface WorkshopCardProps {
  workshop: Workshop;
  selected?: boolean;
  disabled?: boolean;
  onSelect?: (id: string) => void;
}

export function formatWorkshopTime(start: string, end: string) {
  const date = new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(new Date(start));
  const time = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' });
  return `${date} · ${time.format(new Date(start))}–${time.format(new Date(end))}`;
}

export function WorkshopCard({ workshop, selected = false, disabled = false, onSelect }: WorkshopCardProps) {
  const full = workshop.remainingCapacity === 0;
  return (
    <button
      type="button"
      className={`workshop-card${selected ? ' is-selected' : ''}`}
      aria-pressed={selected}
      disabled={disabled}
      onClick={() => onSelect?.(workshop.id)}
    >
      <span className="workshop-card-top">
        <span className="workshop-date">{formatWorkshopTime(workshop.startTime, workshop.endTime)}</span>
        <span className={`seat-count${full ? ' seat-count-full' : ''}`}>
          {full ? 'Full' : `${workshop.remainingCapacity} ${workshop.remainingCapacity === 1 ? 'seat' : 'seats'} left`}
        </span>
      </span>
      <span className="workshop-title">{workshop.title}</span>
      <span className="workshop-card-bottom">
        <span>{workshop.confirmedCount} of {workshop.capacity} confirmed</span>
        <span aria-hidden="true" className="card-arrow">↗</span>
      </span>
    </button>
  );
}
