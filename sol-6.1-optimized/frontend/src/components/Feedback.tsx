import { AlertCircle, CheckCircle2, Info, Inbox, LoaderCircle } from 'lucide-react';
import type { ReactNode } from 'react';

export interface FeedbackProps { kind: 'success' | 'error' | 'warning' | 'info'; children: ReactNode; onDismiss?: () => void; }
export function Feedback({ kind, children, onDismiss }: FeedbackProps) {
  const Icon = kind === 'success' ? CheckCircle2 : kind === 'info' ? Info : AlertCircle;
  return <div className={`feedback feedback-${kind}`} role={kind === 'error' || kind === 'warning' ? 'alert' : 'status'}><Icon size={19} aria-hidden="true" /><div>{children}</div>{onDismiss && <button className="feedback-dismiss" type="button" onClick={onDismiss} aria-label="Dismiss message">×</button>}</div>;
}

export interface LoadingProps { label?: string; }
export function Loading({ label = 'Loading workshops…' }: LoadingProps) {
  return <div className="loading-state" role="status"><LoaderCircle className="spinner" size={24} aria-hidden="true" /><span>{label}</span></div>;
}

export interface EmptyStateProps { title: string; description?: string; action?: ReactNode; compact?: boolean; }
export function EmptyState({ title, description, action, compact }: EmptyStateProps) {
  return <div className={`empty-state${compact ? ' compact' : ''}`}><span className="empty-icon"><Inbox size={compact ? 22 : 30} aria-hidden="true" /></span><h3>{title}</h3>{description && <p>{description}</p>}{action}</div>;
}
