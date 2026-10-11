import { CalendarPlus, X } from 'lucide-react';
import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import type { Notice } from '../useSeatFlow';
import { Feedback } from './Feedback';

export interface WorkshopFormValues {
  title: string;
  start: string;
  end: string;
  capacity: number;
}

export interface WorkshopFormProps {
  open: boolean;
  busy?: boolean;
  error?: string | null;
  notice?: Notice | null;
  pendingInput?: WorkshopFormValues;
  onClose: () => void;
  onSubmit: (values: WorkshopFormValues) => void;
}

function localInputValue(value?: string) {
  if (!value) return '';
  const date = new Date(value);
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function WorkshopForm({ open, busy = false, error, notice, pendingInput, onClose, onSubmit }: WorkshopFormProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const id = useId();
  const [title, setTitle] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [capacity, setCapacity] = useState('12');
  const [validation, setValidation] = useState<string | null>(null);
  const initialInput = useRef(pendingInput);
  initialInput.current = pendingInput;
  const resolving = Boolean(pendingInput);
  const fieldsDisabled = busy || resolving;

  useEffect(() => {
    if (!open) {
      dialog.current?.close();
      return;
    }
    // Restore a lost-response attempt on each opening; keep in-progress edits
    // intact when the parent updates busy/notice/pending state.
    const previous = initialInput.current;
    setTitle(previous?.title || '');
    setStart(localInputValue(previous?.start));
    setEnd(localInputValue(previous?.end));
    setCapacity(String(previous?.capacity || 12));
    setValidation(null);
    dialog.current?.showModal();
    const focusTarget = previous
      ? dialog.current?.querySelector<HTMLButtonElement>('button[type="submit"]')
      : dialog.current?.querySelector<HTMLInputElement>('input');
    focusTarget?.focus();
  }, [open]);

  function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    if (pendingInput) {
      onSubmit(pendingInput);
      return;
    }
    if (!title.trim()) { setValidation('Enter a workshop title.'); return; }
    if (!Number.isFinite(Date.parse(start)) || !Number.isFinite(Date.parse(end))) {
      setValidation('Enter valid start and end times.');
      return;
    }
    if (new Date(end).getTime() <= new Date(start).getTime()) {
      setValidation('The end time must be after the start time.');
      return;
    }
    setValidation(null);
    onSubmit({ title: title.trim(), start: new Date(start).toISOString(), end: new Date(end).toISOString(), capacity: Number(capacity) });
  }

  return (
    <dialog
      ref={dialog}
      className="modal workshop-form-modal"
      aria-labelledby={`${id}-title`}
      onCancel={event => { event.preventDefault(); if (!busy) onClose(); }}
    >
      <form onSubmit={submit}>
        <div className="modal-heading">
          <div>
            <span className="eyebrow">MAKE ROOM FOR IDEAS</span>
            <h2 id={`${id}-title`}>{resolving ? 'Check your workshop' : 'Create a workshop'}</h2>
            <p>{resolving ? 'Check the original request safely, without creating a second workshop.' : 'Set the essentials. We’ll take care of the seats.'}</p>
          </div>
          <button type="button" className="icon-button" onClick={onClose} disabled={busy} aria-label="Close workshop form">
            <X size={20} aria-hidden="true" />
          </button>
        </div>
        <div className="modal-body form-fields">
          {(validation || error) && <Feedback kind="error">{validation || error}</Feedback>}
          {!validation && !error && notice && (
            <Feedback kind={notice.kind}><strong>{notice.title}</strong><span>{notice.message}</span></Feedback>
          )}
          {resolving && !busy && !notice && (
            <Feedback kind="warning">
              <strong>A previous creation still needs checking</strong>
              <span>Its response was not received. Retry the original request to find out whether this workshop was saved. These details are kept until the attempt is resolved.</span>
            </Feedback>
          )}
          <div className="form-field">
            <label htmlFor={`${id}-name`}>Workshop title</label>
            <input id={`${id}-name`} value={title} onChange={event => { setTitle(event.target.value); setValidation(null); }} placeholder="e.g. Design systems that scale" maxLength={200} required disabled={fieldsDisabled} />
          </div>
          <div className="form-columns">
            <div className="form-field">
              <label htmlFor={`${id}-start`}>Starts</label>
              <input id={`${id}-start`} type="datetime-local" value={start} onChange={event => { setStart(event.target.value); setValidation(null); }} required disabled={fieldsDisabled} />
            </div>
            <div className="form-field">
              <label htmlFor={`${id}-end`}>Ends</label>
              <input id={`${id}-end`} type="datetime-local" value={end} onChange={event => { setEnd(event.target.value); setValidation(null); }} required disabled={fieldsDisabled} />
            </div>
          </div>
          <p className="field-hint">Times are in your local timezone ({Intl.DateTimeFormat().resolvedOptions().timeZone}). An end on a later date creates an overnight or multi-day workshop.</p>
          <div className="form-field">
            <label htmlFor={`${id}-capacity`}>Seat capacity</label>
            <input id={`${id}-capacity`} type="number" value={capacity} onChange={event => setCapacity(event.target.value)} min={1} max={10000} step={1} required disabled={fieldsDisabled} aria-describedby={`${id}-capacity-hint`} />
            <p className="field-hint" id={`${id}-capacity-hint`}>When all seats are filled, new registrations join the waitlist.</p>
          </div>
        </div>
        <div className="modal-footer">
          <button type="button" className="button button-secondary" onClick={onClose} disabled={busy}>{resolving ? 'Close for now' : 'Cancel'}</button>
          <button type="submit" className="button button-primary" disabled={busy}>
            <CalendarPlus size={17} aria-hidden="true" />
            {busy ? resolving ? 'Checking workshop…' : 'Creating workshop…' : resolving ? 'Retry workshop creation' : 'Create workshop'}
          </button>
        </div>
      </form>
    </dialog>
  );
}
