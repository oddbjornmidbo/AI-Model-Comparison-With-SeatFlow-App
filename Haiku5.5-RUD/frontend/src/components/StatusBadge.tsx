import type { RegistrationStatus } from "../types";

// Each state has its own icon and text, so meaning does not depend on colour alone.
const labels: Record<RegistrationStatus, { icon: string; text: string }> = {
  Confirmed: { icon: "✔", text: "Confirmed" },
  Waitlisted: { icon: "⏳", text: "Waitlisted" },
  Cancelled: { icon: "✖", text: "Cancelled" },
};

export function StatusBadge({ status }: { status: RegistrationStatus }) {
  const { icon, text } = labels[status];
  return (
    <span className={`badge badge-${status.toLowerCase()}`}>
      <span aria-hidden="true">{icon}</span> {text}
    </span>
  );
}
