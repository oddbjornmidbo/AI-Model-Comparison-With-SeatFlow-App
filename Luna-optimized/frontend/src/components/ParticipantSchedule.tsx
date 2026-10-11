import type { Participant, ScheduleItem } from '../types';
import { formatWorkshopTime } from './WorkshopCard';

export interface ParticipantScheduleProps {
  participants: Participant[];
  selectedParticipantId: string;
  schedule: ScheduleItem[];
  loading?: boolean;
  error?: string | null;
  onParticipantChange: (id: string) => void;
}

export function ParticipantSchedule({ participants, selectedParticipantId, schedule, loading = false, error = null, onParticipantChange }: ParticipantScheduleProps) {
  const participant = participants.find((item) => item.id === selectedParticipantId);
  return (
    <section className="panel schedule-panel" aria-labelledby="schedule-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Your day</p>
          <h2 id="schedule-heading">Participant schedule</h2>
        </div>
      </div>
      <label className="field-label" htmlFor="schedule-participant">Participant</label>
      <select id="schedule-participant" value={selectedParticipantId} onChange={(event) => onParticipantChange(event.target.value)} disabled={participants.length === 0}>
        {participants.length === 0 && <option value="">No participants available</option>}
        {participants.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
      </select>
      {participant && <p className="schedule-for">Confirmed sessions for <strong>{participant.name}</strong></p>}
      {loading ? <p className="inline-state" role="status">Loading schedule…</p>
        : error ? <p className="inline-state inline-state-error" role="alert">{error}</p>
          : schedule.length === 0 ? <p className="empty-state">No confirmed workshops on this schedule.</p>
            : <ol className="schedule-list">{schedule.map((item) => (
              <li className="schedule-item" key={item.registrationId}>
                <span className="schedule-time">{formatWorkshopTime(item.startTime, item.endTime)}</span>
                <span className="schedule-title">{item.workshopTitle}</span>
                <span className="schedule-status">Confirmed</span>
              </li>
            ))}</ol>}
    </section>
  );
}
