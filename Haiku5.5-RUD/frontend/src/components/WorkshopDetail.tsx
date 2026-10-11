import { useState, type FormEvent } from "react";
import type { ParticipantDto, RegistrationDto, WorkshopDto } from "../types";
import { formatTimeRange } from "../format";
import { StatusBadge } from "./StatusBadge";

interface Props {
  workshop: WorkshopDto;
  registrations: RegistrationDto[];
  participants: ParticipantDto[];
  pending: boolean;
  onRegister: (participantId: number) => void;
  onCancel: (registration: RegistrationDto) => void;
}

export function WorkshopDetail({ workshop, registrations, participants, pending, onRegister, onCancel }: Props) {
  const [participantId, setParticipantId] = useState<number | "">("");

  const active = registrations.filter((r) => r.status !== "Cancelled");
  const confirmed = active.filter((r) => r.status === "Confirmed");
  const waitlist = active.filter((r) => r.status === "Waitlisted");

  // Participants who already hold an active seat or place here cannot register again.
  const activeIds = new Set(active.map((r) => r.participantId));
  const available = participants.filter((p) => !activeIds.has(p.id));

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (participantId === "") return;
    onRegister(participantId);
    setParticipantId("");
  };

  return (
    <article aria-labelledby="detail-title">
      <h3 id="detail-title">{workshop.title}</h3>
      <p>
        {formatTimeRange(workshop.startsAt, workshop.endsAt)} · Capacity {workshop.capacity} ·{" "}
        {workshop.remainingCapacity === 0 ? "Full" : `${workshop.remainingCapacity} seat(s) left`}
      </p>

      <form className="register-form" onSubmit={submit}>
        <div className="field">
          <label htmlFor="register-participant">Register a participant</label>
          <select
            id="register-participant"
            value={participantId}
            onChange={(e) => setParticipantId(e.target.value === "" ? "" : Number(e.target.value))}
            disabled={pending || available.length === 0}
          >
            <option value="">
              {available.length === 0 ? "Everyone is already registered" : "Choose a participant"}
            </option>
            {available.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </div>
        <button type="submit" disabled={pending || participantId === ""}>
          {workshop.remainingCapacity === 0 ? "Join waitlist" : "Register"}
        </button>
      </form>

      <h4>Confirmed ({confirmed.length})</h4>
      {confirmed.length === 0 ? (
        <p>No confirmed registrations yet.</p>
      ) : (
        <ul className="list">
          {confirmed.map((r) => (
            <li key={r.id}>
              <span>{r.participantName}</span>
              <span className="list-actions">
                <StatusBadge status={r.status} />
                <button
                  type="button"
                  className="danger"
                  disabled={pending}
                  onClick={() => onCancel(r)}
                  aria-label={`Cancel confirmed registration for ${r.participantName}`}
                >
                  Cancel
                </button>
              </span>
            </li>
          ))}
        </ul>
      )}

      <h4>Waitlist ({waitlist.length})</h4>
      {waitlist.length === 0 ? (
        <p>The waitlist is empty.</p>
      ) : (
        <ol className="list">
          {waitlist.map((r) => (
            <li key={r.id}>
              <span>{r.participantName}</span>
              <span className="list-actions">
                <StatusBadge status={r.status} />
                <button
                  type="button"
                  className="danger"
                  disabled={pending}
                  onClick={() => onCancel(r)}
                  aria-label={`Remove ${r.participantName} from the waitlist`}
                >
                  Cancel
                </button>
              </span>
            </li>
          ))}
        </ol>
      )}
    </article>
  );
}
