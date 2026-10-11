import { ArrowUp, Clock3, Info, Plus, Ticket, UserRound, X } from 'lucide-react';
import { useId } from 'react';
import type { Participant, Registration, Workshop } from '../types';
import { EmptyState, Feedback, Loading } from './Feedback';
import { StatusBadge } from './StatusBadge';
import { WorkshopTiming } from './WorkshopTiming';

export interface WorkshopDetailProps {
  workshop: Workshop;
  registrations: Registration[];
  participants: Participant[];
  selectedParticipantId: number | null;
  onParticipantChange: (id: number | null) => void;
  onRegister: () => void;
  onCancel: (registration: Registration) => void;
  onViewSchedule: (participant: Participant) => void;
  busy?: boolean;
  readOnly?: boolean;
  loading?: boolean;
  error?: string | null;
  registrationHint?: string;
  registrationDisabled?: boolean;
  pendingAttempt?: boolean;
  pendingParticipantIds?: number[];
  feedback?: { kind: 'success' | 'error' | 'warning'; title: string; message: string } | null;
  onDismissFeedback?: () => void;
}

function initials(name: string) {
  return name.split(' ').slice(0, 2).map(word => word[0]).join('');
}

export function WorkshopDetail({
  workshop,
  registrations,
  participants,
  selectedParticipantId,
  onParticipantChange,
  onRegister,
  onCancel,
  onViewSchedule,
  busy = false,
  readOnly = false,
  loading = false,
  error,
  registrationHint,
  registrationDisabled = false,
  pendingAttempt = false,
  pendingParticipantIds = [],
  feedback,
  onDismissFeedback,
}: WorkshopDetailProps) {
  const inputId = useId();
  const hintId = useId();
  const confirmed = registrations.filter(r => r.status === 'Confirmed');
  const waitlisted = registrations.filter(r => r.status === 'Waitlisted');
  const cancelled = registrations.filter(r => r.status === 'Cancelled');
  const full = workshop.remainingSeats === 0;
  const activeParticipants = new Map(registrations.filter(r => r.status !== 'Cancelled').map(r => [r.participantId, r.status]));
  const controlsDisabled = busy || loading || readOnly;
  const canRegister = !controlsDisabled && !registrationDisabled && selectedParticipantId !== null;

  function renderRegistration(registration: Registration, index: number) {
    const participant = registration.participant || participants.find(p => p.id === registration.participantId);
    if (!participant) return null;
    return (
      <li className="roster-row" key={registration.id}>
        <span className={`person-avatar avatar-${participant.id % 4}`} aria-hidden="true">
          {registration.status === 'Waitlisted'
            ? <span className="waitlist-position">{index + 1}</span>
            : initials(participant.name)}
        </span>
        <div className="person-info">
          <button
            type="button"
            className="person-name"
            onClick={() => onViewSchedule(participant)}
            aria-label={`View ${participant.name}'s schedule`}
          >
            {participant.name}
          </button>
          <span className="person-email">{participant.email}</span>
          <StatusBadge status={registration.status} />
        </div>
        {registration.status !== 'Cancelled' && (
          <button
            type="button"
            className="cancel-button"
            onClick={() => onCancel(registration)}
            disabled={controlsDisabled}
            aria-label={`Cancel registration for ${participant.name}`}
            title={`Cancel registration for ${participant.name}`}
          >
            <X size={16} aria-hidden="true" />
            <span>Cancel</span>
          </button>
        )}
      </li>
    );
  }

  return (
    <aside id="selected-workshop" className="workshop-detail" aria-label="Selected workshop" tabIndex={-1}>
      <a href="#catalog-title" className="back-to-catalog">
        <ArrowUp size={15} aria-hidden="true" />
        Back to workshops
      </a>

      <div className="detail-heading">
        <div className="detail-eyebrow">
          <span className="detail-marker" />
          WORKSHOP DETAILS
        </div>
        <h2>{workshop.title}</h2>
        <WorkshopTiming workshop={workshop} className="detail-meta" />
        <p className="timezone-caption">
          Times shown in {Intl.DateTimeFormat().resolvedOptions().timeZone.replaceAll('_', ' ')}.
        </p>
        <div className="detail-capacity">
          <span>
            <Ticket size={16} aria-hidden="true" />
            {workshop.confirmedCount} of {workshop.capacity} seats filled
          </span>
          <strong>{full ? 'Join the waitlist' : `${workshop.remainingSeats} available`}</strong>
        </div>
      </div>

      <section className="register-section" aria-labelledby={`${inputId}-heading`}>
        <h3 id={`${inputId}-heading`}>
          <UserRound size={17} aria-hidden="true" />
          Add a participant
        </h3>
        {feedback && (
          <Feedback kind={feedback.kind} onDismiss={onDismissFeedback}>
            <strong>{feedback.title}</strong>
            <span>{feedback.message}</span>
          </Feedback>
        )}
        <form onSubmit={event => { event.preventDefault(); if (canRegister) onRegister(); }}>
          <label htmlFor={inputId}>Participant</label>
          <select
            id={inputId}
            value={selectedParticipantId ?? ''}
            onChange={event => onParticipantChange(event.target.value ? Number(event.target.value) : null)}
            disabled={controlsDisabled || pendingAttempt}
            aria-describedby={hintId}
          >
            <option value="">Select a participant</option>
            {participants.map(participant => (
              <option
                key={participant.id}
                value={participant.id}
                disabled={activeParticipants.has(participant.id) && !pendingParticipantIds.includes(participant.id)}
              >
                {participant.name}
                {pendingParticipantIds.includes(participant.id)
                  ? ' — retry previous attempt'
                  : activeParticipants.has(participant.id)
                    ? ` — ${activeParticipants.get(participant.id)?.toLowerCase()}`
                    : ''}
              </option>
            ))}
          </select>
          <div
            id={hintId}
            className={`registration-hint${registrationDisabled && selectedParticipantId !== null ? ' hint-unavailable' : ''}`}
          >
            <Info size={15} aria-hidden="true" />
            <span>
              {registrationHint || (full
                ? 'This workshop is full. New registrations join the waitlist.'
                : 'Choose an existing participant to reserve their seat.')}
            </span>
          </div>
          <button type="submit" className="button button-primary register-button" disabled={!canRegister}>
            <Plus size={16} aria-hidden="true" />
            {busy
              ? 'Saving registration…'
              : pendingAttempt
                ? 'Retry registration'
                : full
                  ? 'Add to waitlist'
                  : 'Register participant'}
          </button>
        </form>
      </section>

      <div className="roster-container">
        {error && <Feedback kind="error">{error}</Feedback>}
        {loading ? <Loading label="Loading registrations…" /> : (
          <>
            <section className="roster-section" aria-labelledby={`${inputId}-confirmed`}>
              <div className="roster-heading">
                <h3 id={`${inputId}-confirmed`}>
                  Confirmed participants <span className="count-pill">{confirmed.length}</span>
                </h3>
                <span className="roster-heading-icon">
                  <UserRound size={16} aria-hidden="true" />
                </span>
              </div>
              {confirmed.length
                ? <ul className="roster-list">{confirmed.map(renderRegistration)}</ul>
                : <EmptyState compact title="The first seat is waiting" description="Register a participant to get things started." />}
            </section>

            <section className="roster-section waitlist-section" aria-labelledby={`${inputId}-waitlist`}>
              <div className="roster-heading">
                <h3 id={`${inputId}-waitlist`}>
                  Waitlist <span className="count-pill amber">{waitlisted.length}</span>
                </h3>
                <Clock3 size={16} aria-hidden="true" />
              </div>
              {waitlisted.length ? (
                <>
                  <p className="roster-caption">
                    When a confirmed participant cancels, the first person in line without a schedule conflict gets the seat.
                    Anyone skipped keeps their place in the queue.
                  </p>
                  <ol className="roster-list">{waitlisted.map(renderRegistration)}</ol>
                </>
              ) : <p className="quiet-empty">No participants on the waitlist.</p>}
            </section>

            {cancelled.length > 0 && (
              <details className="cancelled-section">
                <summary>
                  Cancelled registrations <span className="count-pill">{cancelled.length}</span>
                </summary>
                <ul className="roster-list">{cancelled.map(renderRegistration)}</ul>
              </details>
            )}
            <p className="schedule-tip">
              <Info size={14} aria-hidden="true" />
              Select a participant’s name to view their schedule.
            </p>
          </>
        )}
      </div>
    </aside>
  );
}
