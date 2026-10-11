import type { ParticipantDto, RegistrationDto } from "../types";
import { formatTimeRange } from "../format";
import { StatusBadge } from "./StatusBadge";

interface Props {
  participants: ParticipantDto[];
  selectedId: number | null;
  schedule: RegistrationDto[];
  onSelect: (participantId: number) => void;
}

export function ParticipantSchedule({ participants, selectedId, schedule, onSelect }: Props) {
  return (
    <>
      <div className="field">
        <label htmlFor="schedule-participant">Participant</label>
        <select
          id="schedule-participant"
          value={selectedId ?? ""}
          onChange={(e) => onSelect(Number(e.target.value))}
        >
          {participants.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      </div>

      {schedule.length === 0 ? (
        <p>No registrations for this participant.</p>
      ) : (
        <div className="table-wrap">
          <table>
            <caption className="visually-hidden">Workshops for the selected participant</caption>
            <thead>
              <tr>
                <th scope="col">Workshop</th>
                <th scope="col">When</th>
                <th scope="col">Status</th>
              </tr>
            </thead>
            <tbody>
              {schedule.map((r) => (
                <tr key={r.id} className={r.status === "Cancelled" ? "row-muted" : undefined}>
                  <th scope="row" data-label="Workshop">{r.workshopTitle}</th>
                  <td data-label="When">{formatTimeRange(r.workshopStartsAt, r.workshopEndsAt)}</td>
                  <td data-label="Status"><StatusBadge status={r.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
