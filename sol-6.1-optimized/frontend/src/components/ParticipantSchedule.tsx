import { CalendarDays, X } from 'lucide-react';
import { useEffect, useId, useRef } from 'react';
import type { Participant, Registration } from '../types';
import { EmptyState, Feedback, Loading } from './Feedback';
import { StatusBadge } from './StatusBadge';
import { WorkshopTiming } from './WorkshopTiming';

export interface ParticipantScheduleProps {
  participant: Participant;
  registrations: Registration[] | null;
  loading?: boolean;
  error?: string | null;
  onClose: () => void;
}

export function ParticipantSchedule({
  participant,
  registrations,
  loading = false,
  error,
  onClose,
}: ParticipantScheduleProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => {
    const modal = dialog.current;
    const previousFocus = document.activeElement;
    modal?.showModal();
    return () => {
      modal?.close();
      // This dialog unmounts on close. Native restoration alone happens too late
      // once its opener has become inert; explicitly restore the user's place.
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected)
        previousFocus.focus({ preventScroll: true });
    };
  }, []);
  const ordered = [...(registrations || [])].sort((a, b) =>
    new Date(a.workshop.start).getTime() - new Date(b.workshop.start).getTime());
  const confirmedCount = ordered.filter(registration => registration.status === 'Confirmed').length;
  const waitlistedCount = ordered.filter(registration => registration.status === 'Waitlisted').length;

  return (
    <dialog
      ref={dialog}
      className="modal schedule-modal"
      aria-labelledby={titleId}
      onCancel={event => { event.preventDefault(); onClose(); }}
    >
      <div className="modal-heading">
        <div>
          <span className="eyebrow">PARTICIPANT SCHEDULE</span>
          <h2 id={titleId}>{participant.name}</h2>
          <p>{participant.email}</p>
        </div>
        <button type="button" className="icon-button" onClick={onClose} aria-label="Close schedule">
          <X size={20} aria-hidden="true" />
        </button>
      </div>

      <div className="modal-body">
        {error && <Feedback kind="error">{error}</Feedback>}
        {loading ? <Loading label="Loading participant schedule…" /> : ordered.length ? (
          <>
            <div className="schedule-summary">
              <CalendarDays size={18} aria-hidden="true" />
              <span>
                <strong>{confirmedCount}</strong> confirmed workshops
                <span className="schedule-summary-divider">·</span>
                {waitlistedCount} waitlisted
              </span>
            </div>
            <ul className="schedule-list">
              {ordered.map(registration => (
                <li className={`schedule-item schedule-${registration.status.toLowerCase()}`} key={registration.id}>
                  <span className="schedule-item-marker" aria-hidden="true" />
                  <div className="schedule-item-content">
                    <StatusBadge status={registration.status} />
                    <h3>{registration.workshop.title}</h3>
                    <WorkshopTiming workshop={registration.workshop} className="schedule-meta" iconSize={14} />
                    {registration.status === 'Waitlisted' && (
                      <p>
                        When a confirmed participant cancels this workshop, the first person in line without a schedule
                        conflict gets the seat. Anyone skipped keeps their place in the queue.
                      </p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </>
        ) : !error && (
          <EmptyState title="A fresh schedule" description="This participant hasn’t registered for a workshop yet." />
        )}
      </div>

      <div className="modal-footer">
        <button type="button" className="button button-secondary" onClick={onClose}>Done</button>
      </div>
    </dialog>
  );
}
