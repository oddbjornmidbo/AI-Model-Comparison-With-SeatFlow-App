import { ArrowUpRight, CalendarDays, CheckCheck, Ticket, Users } from 'lucide-react';

export interface StatCardProps { label: string; value: number | string; description?: string; icon?: 'workshops' | 'participants' | 'confirmed' | 'seats'; }
export function StatCard({ label, value, description, icon = 'workshops' }: StatCardProps) {
  const Icon = { workshops: CalendarDays, participants: Users, confirmed: CheckCheck, seats: Ticket }[icon];
  return <div className={`stat-card stat-${icon}`}><div className="stat-label"><span>{label}</span><span className="stat-icon"><Icon size={19} strokeWidth={1.8} aria-hidden="true" /></span></div><div className="stat-value">{value}</div>{description && <div className="stat-description"><ArrowUpRight size={13} aria-hidden="true" />{description}</div>}</div>;
}
