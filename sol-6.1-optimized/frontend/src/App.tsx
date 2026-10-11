import { useEffect, useMemo, useState } from 'react';
import { CalendarDays, Plus, RefreshCw, Search, Users } from 'lucide-react';
import { EmptyState, Feedback, Loading, ParticipantSchedule, Shell, StatCard, WorkshopCard, WorkshopDetail, WorkshopForm } from './components';
import type { Participant } from './types';
import { registrationAvailability, useSeatFlow } from './useSeatFlow';

export default function App() {
  const state = useSeatFlow();
  const [page, setPage] = useState<'workshops' | 'participants'>('workshops');
  const [workshopId, setWorkshopId] = useState<number | null>(null);
  const [participantId, setParticipantId] = useState<number | null>(null);
  const [scheduleParticipant, setScheduleParticipant] = useState<Participant | null>(null);
  const [query, setQuery] = useState('');
  const [availability, setAvailability] = useState('all');
  const [showForm, setShowForm] = useState(false);
  const [narrow, setNarrow] = useState(() => window.matchMedia('(max-width: 800px)').matches);
  useEffect(() => {
    const media = window.matchMedia('(max-width: 800px)');
    const update = () => setNarrow(media.matches);
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);
  const snapshot = state.snapshot;
  const allRegistrations = useMemo(() => snapshot ? Object.values(snapshot.registrations).flat() : [], [snapshot]);
  const workshops = snapshot?.workshops || [];
  const participants = snapshot?.participants || [];
  const selected = workshops.find(workshop => workshop.id === workshopId) || workshops[0];
  const roster = selected ? snapshot?.registrations[selected.id] || [] : [];
  const participant = participants.find(item => item.id === participantId);
  const participantSchedule = allRegistrations.filter(item => item.participantId === participantId && item.status !== 'Cancelled');
  const pending = Boolean(selected && participantId && state.attempts[`${selected.id}:${participantId}`]);
  const pendingParticipantIds = Object.values(state.attempts).filter(attempt => attempt.workshopId === selected?.id).map(attempt => attempt.participantId);
  // A delayed action response must never become feedback for a different session.
  // Stale-data warnings apply to the whole workspace and remain visible.
  const notice = state.notice && (!state.notice.workshopId || state.stale
    || (page === 'workshops' && state.notice.workshopId === selected?.id))
    && !(showForm && state.notice.operation === 'creation') ? state.notice : null;
  const inlineFeedback = narrow && page === 'workshops' && Boolean(snapshot && selected)
    && notice?.workshopId === selected?.id && !state.stale;
  const registrationState = selected ? registrationAvailability(selected, participant, roster, participantSchedule, pending && !state.busy) : { disabled: true, hint: '' };
  const visibleWorkshops = workshops.filter(workshop => workshop.title.toLowerCase().includes(query.toLowerCase())
    && (availability === 'all' || (availability === 'available' ? workshop.remainingSeats > 0 : workshop.remainingSeats === 0)));
  const confirmed = allRegistrations.filter(item => item.status === 'Confirmed').length;
  const waitlisted = allRegistrations.filter(item => item.status === 'Waitlisted').length;
  const enrolled = new Set(allRegistrations.filter(item => item.status !== 'Cancelled').map(item => item.participantId)).size;

  return (
    <Shell
      activePage={page}
      onPageChange={next => { setPage(next); if (!state.stale) state.dismissNotice(); }}
    >
      <div className="page-heading">
        <div>
          <p className="eyebrow">YOUR CONFERENCE, CONNECTED</p>
          <h1>{page === 'workshops' ? 'Workshops' : 'Participants'}</h1>
          <p className="subtitle">
            {page === 'workshops'
              ? 'Great sessions. The right people. Every seat accounted for.'
              : 'A familiar face, a clear schedule. Keep everyone in the loop.'}
          </p>
        </div>
        <div className="heading-actions">
          <button
            className="button button-secondary"
            type="button"
            onClick={() => void state.reload()}
            disabled={state.loading || state.busy}
          >
            <RefreshCw size={16} aria-hidden="true" />Refresh
          </button>
          {page === 'workshops' && (
            <button
              className="button button-primary"
              type="button"
              onClick={() => { state.dismissNotice(); setShowForm(true); }}
              disabled={state.busy || state.stale || !snapshot}
            >
              <Plus size={18} aria-hidden="true" />
              {state.pendingCreation ? 'Resolve workshop' : 'New workshop'}
            </button>
          )}
        </div>
      </div>

      {notice && !inlineFeedback && (
        <Feedback kind={notice.kind} onDismiss={state.stale ? undefined : state.dismissNotice}>
          <strong>{notice.title}</strong>
          <span>{notice.message}</span>
        </Feedback>
      )}

      {!snapshot ? (
        state.loading ? <Loading label="Getting your conference ready…" /> : (
          <EmptyState
            title="Your workspace is taking a moment"
            description="Start the local API, then try loading your workshops again."
            action={<button className="button button-primary" onClick={() => void state.reload()}>Try again</button>}
          />
        )
      ) : <>
        <section className="stats-grid" aria-label="Conference overview">
          <StatCard label="Workshops" value={workshops.length} description="Ideas worth coming together for" icon="workshops" />
          <StatCard label="Confirmed seats" value={confirmed} description={`${workshops.reduce((sum, workshop) => sum + workshop.remainingSeats, 0)} seats still available`} icon="confirmed" />
          <StatCard label="On the waitlist" value={waitlisted} description="Next in line for a great session" icon="seats" />
          <StatCard label="Participants" value={participants.length} description={`${enrolled} with an active registration`} icon="participants" />
        </section>

        {page === 'workshops' ? <div className="workspace-content" aria-busy={state.loading || state.busy}>
          <section className="catalog" aria-labelledby="catalog-title">
            <div className="catalog-header">
              <div>
                <h2 id="catalog-title">Find your next session</h2>
                <p>{workshops.length} workshops, plenty of possibilities.</p>
              </div>
              <span className="section-label"><CalendarDays size={15} aria-hidden="true" />Conference program</span>
            </div>
            <div className="catalog-tools">
              <div className="search-field">
                <Search size={18} aria-hidden="true" />
                <label className="sr-only" htmlFor="workshop-search">Search workshops</label>
                <input
                  id="workshop-search"
                  type="search"
                  placeholder="Search workshops…"
                  value={query}
                  onChange={event => setQuery(event.target.value)}
                />
              </div>
              <label className="sr-only" htmlFor="availability-filter">Filter by availability</label>
              <select id="availability-filter" value={availability} onChange={event => setAvailability(event.target.value)}>
                <option value="all">All workshops</option>
                <option value="available">Seats available</option>
                <option value="full">Waitlist only</option>
              </select>
            </div>
            <div className="workshop-grid">
              {visibleWorkshops.map(workshop => (
                <WorkshopCard
                  key={workshop.id}
                  workshop={workshop}
                  selected={workshop.id === selected?.id}
                  onSelect={item => {
                    setWorkshopId(item.id);
                    if (!state.stale) state.dismissNotice();
                    if (window.matchMedia('(max-width: 800px)').matches) requestAnimationFrame(() => {
                      const detail = document.getElementById('selected-workshop');
                      detail?.scrollIntoView({
                        block: 'start',
                        behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth',
                      });
                      detail?.focus({ preventScroll: true });
                    });
                  }}
                />
              ))}
            </div>
            {visibleWorkshops.length === 0 && (
              <EmptyState
                title={workshops.length ? 'No matching workshops' : 'Make room for your first workshop'}
                description={workshops.length
                  ? 'Try another title or change the availability filter.'
                  : 'Create a session to get your conference started.'}
                compact
              />
            )}
            <div className="catalog-note">
              <span className="note-icon"><Users size={18} aria-hidden="true" /></span>
              <div>
                <strong>Full doesn’t mean finished.</strong>
                <p>When a confirmed registration is cancelled, the first waiting participant without a schedule conflict receives the seat.</p>
              </div>
            </div>
          </section>
          {selected && (
            <WorkshopDetail
              workshop={selected}
              registrations={roster}
              participants={participants}
              selectedParticipantId={participantId}
              onParticipantChange={id => { setParticipantId(id); if (!state.stale) state.dismissNotice(); }}
              onRegister={() => { if (participantId) void state.register(selected.id, participantId); }}
              onCancel={registration => void state.cancel(registration)}
              onViewSchedule={setScheduleParticipant}
              busy={state.busy}
              readOnly={state.stale || state.loading}
              registrationHint={state.stale
                ? 'The displayed information may be out of date. Refresh before changing registrations.'
                : registrationState.hint}
              registrationDisabled={registrationState.disabled || state.stale}
              pendingAttempt={pending}
              pendingParticipantIds={pendingParticipantIds}
              feedback={inlineFeedback ? notice : null}
              onDismissFeedback={state.stale ? undefined : state.dismissNotice}
            />
          )}
        </div> : <section aria-labelledby="participants-title">
          <div className="catalog-header">
            <div>
              <h2 id="participants-title">People make the program</h2>
              <p>Select a participant to explore their schedule.</p>
            </div>
          </div>
          <div className="participant-grid">
            {participants.map(item => {
              const registrations = allRegistrations.filter(row => row.participantId === item.id);
              const seats = registrations.filter(row => row.status === 'Confirmed').length;
              const waiting = registrations.filter(row => row.status === 'Waitlisted').length;
              return (
                <button className="participant-card" key={item.id} onClick={() => setScheduleParticipant(item)}>
                  <span className="avatar">{item.name.split(' ').map(part => part[0]).slice(0, 2).join('')}</span>
                  <span className="participant-card-info">
                    <strong>{item.name}</strong>
                    <span>{item.email}</span>
                    <span className="participant-card-meta">
                      {seats} confirmed {seats === 1 ? 'workshop' : 'workshops'}
                      {waiting > 0 ? ` · ${waiting} waitlisted` : ''}
                    </span>
                  </span>
                  <CalendarDays size={20} aria-hidden="true" />
                </button>
              );
            })}
          </div>
          {!participants.length && <EmptyState title="No participants yet" description="Your conference participants will appear here." />}
        </section>}
      </>}

      {scheduleParticipant && (
        <ParticipantSchedule
          participant={scheduleParticipant}
          registrations={allRegistrations
            .filter(item => item.participantId === scheduleParticipant.id && item.status !== 'Cancelled')
            .sort((a, b) => Date.parse(a.workshop.start) - Date.parse(b.workshop.start))}
          loading={false}
          error={state.stale ? 'The latest schedule could not be loaded. Close this window and refresh the workspace.' : null}
          onClose={() => setScheduleParticipant(null)}
        />
      )}
      <WorkshopForm
        open={showForm}
        busy={state.busy}
        notice={showForm && state.notice?.operation === 'creation' ? state.notice : null}
        pendingInput={state.pendingCreation?.input}
        onClose={() => { setShowForm(false); if (!state.stale) state.dismissNotice(); }}
        onSubmit={async values => { if (await state.createWorkshop(values)) setShowForm(false); }}
      />
    </Shell>
  );
}
