import type { ScheduleItem, Participant } from '../types';

interface Props {
  participantId: number;
  schedule: ScheduleItem[];
  participants: Participant[];
  onSelectParticipant: (id: number) => void;
  onViewWorkshop: (workshopId: number) => void;
}

export default function ParticipantSchedule({
  participantId,
  schedule,
  participants,
  onSelectParticipant,
  onViewWorkshop,
}: Props) {
  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleString();
  };

  const currentParticipant = participants.find(p => p.id === participantId);

  return (
    <div className="schedule-view">
      <h2>Participant Schedule</h2>

      <div className="participant-selector">
        <label>Select Participant: </label>
        <select
          value={participantId}
          onChange={(e) => onSelectParticipant(parseInt(e.target.value))}
        >
          {participants.map(p => (
            <option key={p.id} value={p.id}>{p.name} ({p.email})</option>
          ))}
        </select>
      </div>

      {currentParticipant && (
        <div className="participant-info">
          <h3>{currentParticipant.name}</h3>
          <p>{currentParticipant.email}</p>
        </div>
      )}

      <div className="schedule-list">
        {schedule.length === 0 ? (
          <p>No confirmed registrations</p>
        ) : (
          <>
            <h3>Confirmed Workshops ({schedule.length})</h3>
            <div className="schedule-grid">
              {schedule.map(item => (
                <div key={item.registrationId} className="schedule-item">
                  <h4>{item.workshopTitle}</h4>
                  <p><strong>Time:</strong> {formatTime(item.startTime)}</p>
                  <p><strong>End:</strong> {formatTime(item.endTime)}</p>
                  <button onClick={() => onViewWorkshop(item.workshopId)}>
                    View Workshop
                  </button>
                </div>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
