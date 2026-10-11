import { CalendarDays, Clock3 } from 'lucide-react';
import type { Workshop } from '../types';
import { accessibleWorkshopRange, dateRange, timeRange } from '../workshopDates';

interface WorkshopTimingProps {
  workshop: Pick<Workshop, 'start' | 'end'>;
  className: string;
  iconSize?: number;
}

export function WorkshopTiming({ workshop, className, iconSize = 15 }: WorkshopTimingProps) {
  return (
    <div className={className}>
      <span aria-hidden="true">
        <CalendarDays size={iconSize} />
        {dateRange(workshop)}
      </span>
      <span aria-hidden="true">
        <Clock3 size={iconSize} />
        {timeRange(workshop)}
      </span>
      <span className="sr-only">{accessibleWorkshopRange(workshop)}</span>
    </div>
  );
}
