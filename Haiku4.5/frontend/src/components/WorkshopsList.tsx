import type { Workshop } from '../types';

interface Props {
  workshops: Workshop[];
  onSelectWorkshop: (workshop: Workshop) => void;
}

export default function WorkshopsList({ workshops, onSelectWorkshop }: Props) {
  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleString();
  };

  return (
    <div className="workshops-list">
      <h2>Available Workshops</h2>
      {workshops.length === 0 ? (
        <p>No workshops available</p>
      ) : (
        <div className="workshop-grid">
          {workshops.map((workshop) => (
            <div key={workshop.id} className="workshop-card">
              <h3>{workshop.title}</h3>
              <p><strong>Time:</strong> {formatTime(workshop.startTime)} - {formatTime(workshop.endTime)}</p>
              <p><strong>Capacity:</strong> {workshop.confirmedCount}/{workshop.capacity} confirmed</p>
              <p><strong>Waitlist:</strong> {workshop.waitlistCount}</p>
              <p className="availability">
                {workshop.confirmedCount < workshop.capacity
                  ? '✓ Seats available'
                  : '✗ Full (waitlist only)'}
              </p>
              <button onClick={() => onSelectWorkshop(workshop)}>View Details</button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
