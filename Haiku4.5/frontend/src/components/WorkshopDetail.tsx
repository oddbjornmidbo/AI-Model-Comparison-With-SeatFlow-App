import { useState } from 'react';
import type { Workshop, Registration, Participant } from '../types';

interface Props {
  workshop: Workshop;
  registrations: Registration[];
  participants: Participant[];
  onBack: () => void;
  onRegister: (workshopId: number, participantId: number) => void;
  onCancel: (registrationId: number) => void;
  onViewSchedule: (participantId: number) => void;
}

export default function WorkshopDetail({
  workshop,
  registrations,
  participants,
  onBack,
  onRegister,
  onCancel,
  onViewSchedule,
}: Props) {
  const [selectedParticipantId, setSelectedParticipantId] = useState<number | null>(null);

  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleString();
  };

  const confirmed = registrations.filter(r => r.status === 'Confirmed');
  const waitlisted = registrations.filter(r => r.status === 'Waitlisted');

  const handleRegister = () => {
    if (selectedParticipantId !== null) {
      onRegister(workshop.id, selectedParticipantId);
      setSelectedParticipantId(null);
    }
  };

  return (
    <div className="workshop-detail">
      <button className="back-button" onClick={onBack}>← Back to Workshops</button>

      <div className="detail-header">
        <h2>{workshop.title}</h2>
        <div className="workshop-info">
          <p><strong>Time:</strong> {formatTime(workshop.startTime)} - {formatTime(workshop.endTime)}</p>
          <p><strong>Total Capacity:</strong> {workshop.capacity}</p>
          <p><strong>Confirmed:</strong> {confirmed.length}/{workshop.capacity}</p>
          <p><strong>Waitlist:</strong> {waitlisted.length}</p>
        </div>
      </div>

      <div className="registration-section">
        <h3>Register for this Workshop</h3>
        <div className="registration-form">
          <select
            value={selectedParticipantId || ''}
            onChange={(e) => setSelectedParticipantId(e.target.value ? parseInt(e.target.value) : null)}
          >
            <option value="">Select participant...</option>
            {participants.map(p => (
              <option key={p.id} value={p.id}>{p.name} ({p.email})</option>
            ))}
          </select>
          <button onClick={handleRegister} disabled={selectedParticipantId === null}>
            Register
          </button>
        </div>
      </div>

      <div className="registrations-section">
        <h3>Confirmed Registrations ({confirmed.length})</h3>
        {confirmed.length === 0 ? (
          <p>No confirmed registrations</p>
        ) : (
          <table className="registrations-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Registered</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {confirmed.map(reg => (
                <tr key={reg.id}>
                  <td>{reg.participantName}</td>
                  <td>{reg.participantEmail}</td>
                  <td>{new Date(reg.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button className="text-button" onClick={() => onViewSchedule(reg.participantId)}>
                      View Schedule
                    </button>
                    <button className="cancel-button" onClick={() => onCancel(reg.id)}>
                      Cancel
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="registrations-section">
        <h3>Waitlist ({waitlisted.length})</h3>
        {waitlisted.length === 0 ? (
          <p>No one on waitlist</p>
        ) : (
          <table className="registrations-table waitlist">
            <thead>
              <tr>
                <th>Position</th>
                <th>Name</th>
                <th>Email</th>
                <th>Registered</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {waitlisted.map((reg, idx) => (
                <tr key={reg.id}>
                  <td>{idx + 1}</td>
                  <td>{reg.participantName}</td>
                  <td>{reg.participantEmail}</td>
                  <td>{new Date(reg.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button className="cancel-button" onClick={() => onCancel(reg.id)}>
                      Cancel
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
