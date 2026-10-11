import type { Registration } from '../types';
import { StatusBadge } from './StatusBadge';

export interface RegistrationListProps {
  registrations: Registration[];
  loading?: boolean;
  error?: string | null;
  busyRegistrationId?: string | null;
  mutationsDisabled?: boolean;
  onCancel?: (registration: Registration) => void;
}

function RegistrationRow({ registration, busy, mutationsDisabled, onCancel }: { registration: Registration; busy: boolean; mutationsDisabled: boolean; onCancel?: (registration: Registration) => void }) {
  const cancellable = registration.status !== 'Cancelled';
  return (
    <li className="registration-row">
      <div className="registration-person">
        <span className="person-name">{registration.participantName}</span>
        <span className="person-email">{registration.participantEmail}</span>
      </div>
      <StatusBadge status={registration.status} />
      {cancellable ? (
        <button className="button button-quiet button-small" type="button" disabled={busy || mutationsDisabled} onClick={() => onCancel?.(registration)} aria-label={`Cancel ${registration.participantName}'s ${registration.status.toLowerCase()} registration`}>
          {busy ? 'Cancelling…' : 'Cancel'}
        </button>
      ) : <span className="registration-ended">Ended</span>}
    </li>
  );
}

export function RegistrationList({ registrations, loading = false, error = null, busyRegistrationId = null, mutationsDisabled = false, onCancel }: RegistrationListProps) {
  if (loading) return <p className="inline-state" role="status">Loading registrations…</p>;
  if (error) return <p className="inline-state inline-state-error" role="alert">{error}</p>;

  const groups = [
    { title: 'Confirmed', items: registrations.filter((item) => item.status === 'Confirmed') },
    { title: 'Waitlist', items: registrations.filter((item) => item.status === 'Waitlisted') },
    { title: 'Cancelled', items: registrations.filter((item) => item.status === 'Cancelled') },
  ];
  if (registrations.length === 0) return <p className="empty-state">No registrations yet. The first seat is waiting.</p>;

  return (
    <div className="registration-groups">
      {groups.filter((group) => group.items.length > 0).map((group) => (
        <section className="registration-group" key={group.title} aria-label={`${group.title} registrations`}>
          <h3>{group.title}<span className="group-count">{group.items.length}</span></h3>
          <ul className="registration-list">
            {group.items.map((registration) => <RegistrationRow key={registration.id} registration={registration} busy={busyRegistrationId === registration.id} mutationsDisabled={mutationsDisabled} onCancel={onCancel} />)}
          </ul>
        </section>
      ))}
    </div>
  );
}
