import { ArrowUpRight, CalendarDays, CircleHelp, Layers3, Users } from 'lucide-react';
import type { ReactNode } from 'react';

export type Page = 'workshops' | 'participants';

export interface ShellProps {
  children: ReactNode;
  activePage: Page;
  onPageChange: (page: Page) => void;
}

export function Shell({ children, activePage, onPageChange }: ShellProps) {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <aside className="sidebar">
        <a className="brand" href="#main-content" aria-label="SeatFlow home">
          <span className="brand-mark"><Layers3 size={23} strokeWidth={2.3} aria-hidden="true" /></span>
          <span>seatflow<span className="brand-dot">.</span></span>
        </a>
        <div className="workspace-label">
          <span className="workspace-monogram" aria-hidden="true">S</span>
          <div>
            <strong>Summit workspace</strong>
            <span>Conference management</span>
          </div>
        </div>
        <div className="nav-label">WORKSPACE</div>
        <nav className="main-nav" aria-label="Main navigation">
          <button
            type="button"
            className={activePage === 'workshops' ? 'nav-item active' : 'nav-item'}
            aria-current={activePage === 'workshops' ? 'page' : undefined}
            onClick={() => onPageChange('workshops')}
          >
            <CalendarDays size={20} aria-hidden="true" />
            <span>Workshops</span>
            {activePage === 'workshops' && <span className="nav-active-dot" />}
          </button>
          <button
            type="button"
            className={activePage === 'participants' ? 'nav-item active' : 'nav-item'}
            aria-current={activePage === 'participants' ? 'page' : undefined}
            onClick={() => onPageChange('participants')}
          >
            <Users size={20} aria-hidden="true" />
            <span>Participants</span>
            {activePage === 'participants' && <span className="nav-active-dot" />}
          </button>
        </nav>
        <div className="sidebar-bottom">
          <div className="sidebar-tip">
            <CircleHelp size={19} aria-hidden="true" />
            <strong>A little less admin.</strong>
            <p>Seats, schedules, and waitlists. All in one place.</p>
            <span>Make room for great ideas <ArrowUpRight size={14} aria-hidden="true" /></span>
          </div>
          <div className="workspace-profile">
            <span className="profile-avatar" aria-hidden="true">SW</span>
            <div>
              <strong>Summit workspace</strong>
              <span>Local conference</span>
            </div>
            <span className="connection-dot" title="Local workspace" />
          </div>
        </div>
      </aside>
      <div className="main-shell">
        <header className="topbar">
          <div className="breadcrumb">
            Workspace <span>/</span> <strong>{activePage === 'workshops' ? 'Workshops' : 'Participants'}</strong>
          </div>
          <div className="topbar-right">
            <span className="local-label"><span className="connection-dot" />Local workspace</span>
            <span className="topbar-avatar" aria-hidden="true">S</span>
          </div>
        </header>
        <main id="main-content" className="main-content" tabIndex={-1}>{children}</main>
        <footer className="app-footer">
          <span>Thoughtful events start with a little flow.</span>
          <span>SeatFlow <span aria-hidden="true">·</span> Conference workspace</span>
        </footer>
      </div>
    </div>
  );
}
