import type { WorkshopDto } from "../types";
import { formatTimeRange } from "../format";

interface Props {
  workshops: WorkshopDto[];
  selectedId: number | null;
  onSelect: (workshopId: number) => void;
}

export function WorkshopTable({ workshops, selectedId, onSelect }: Props) {
  return (
    <div className="table-wrap">
      <table>
        <caption className="visually-hidden">Workshops with capacity and remaining seats</caption>
        <thead>
          <tr>
            <th scope="col">Workshop</th>
            <th scope="col">When</th>
            <th scope="col">Capacity</th>
            <th scope="col">Confirmed</th>
            <th scope="col">Remaining</th>
            <th scope="col">Waitlist</th>
            <th scope="col"><span className="visually-hidden">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          {workshops.map((w) => (
            <tr key={w.id} className={w.id === selectedId ? "row-selected" : undefined}>
              <th scope="row" data-label="Workshop">{w.title}</th>
              <td data-label="When">{formatTimeRange(w.startsAt, w.endsAt)}</td>
              <td data-label="Capacity">{w.capacity}</td>
              <td data-label="Confirmed">{w.confirmedCount}</td>
              <td data-label="Remaining">
                {w.remainingCapacity === 0 ? "0 (full)" : w.remainingCapacity}
              </td>
              <td data-label="Waitlist">{w.waitlistCount}</td>
              <td data-label="Actions">
                <button
                  type="button"
                  className="secondary"
                  onClick={() => onSelect(w.id)}
                  aria-label={`View registrations for ${w.title}`}
                >
                  {w.id === selectedId ? "Viewing" : "View"}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
