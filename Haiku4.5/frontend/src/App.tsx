import { useState, useEffect } from 'react';
import './App.css';
import type { Workshop, Participant, Registration, ScheduleItem } from './types';
import WorkshopsList from './components/WorkshopsList';
import WorkshopDetail from './components/WorkshopDetail';
import ParticipantSchedule from './components/ParticipantSchedule';
import ParticipantForm from './components/ParticipantForm';

const API_URL = 'http://localhost:5000/api';

function App() {
  const [activeTab, setActiveTab] = useState<'workshops' | 'schedule' | 'register'>('workshops');
  const [workshops, setWorkshops] = useState<Workshop[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [selectedWorkshop, setSelectedWorkshop] = useState<Workshop | null>(null);
  const [registrations, setRegistrations] = useState<Registration[]>([]);
  const [selectedParticipantId, setSelectedParticipantId] = useState<number | null>(null);
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadWorkshops();
    loadParticipants();
  }, []);

  const loadWorkshops = async () => {
    try {
      setLoading(true);
      const res = await fetch(`${API_URL}/workshops`);
      if (!res.ok) throw new Error('Failed to load workshops');
      const data = await res.json();
      setWorkshops(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error loading workshops');
    } finally {
      setLoading(false);
    }
  };

  const loadParticipants = async () => {
    try {
      const res = await fetch(`${API_URL}/participants`);
      if (!res.ok) throw new Error('Failed to load participants');
      const data = await res.json();
      setParticipants(data);
    } catch (err) {
      console.error('Error loading participants:', err);
    }
  };

  const loadWorkshopRegistrations = async (workshopId: number) => {
    try {
      const res = await fetch(`${API_URL}/workshops/${workshopId}/registrations`);
      if (!res.ok) throw new Error('Failed to load registrations');
      const data = await res.json();
      setRegistrations(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error loading registrations');
    }
  };

  const loadSchedule = async (participantId: number) => {
    try {
      const res = await fetch(`${API_URL}/participants/${participantId}/schedule`);
      if (!res.ok) throw new Error('Failed to load schedule');
      const data = await res.json();
      setSchedule(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error loading schedule');
    }
  };

  const handleViewWorkshop = async (workshop: Workshop) => {
    setSelectedWorkshop(workshop);
    await loadWorkshopRegistrations(workshop.id);
  };

  const handleViewSchedule = async (participantId: number) => {
    setSelectedParticipantId(participantId);
    await loadSchedule(participantId);
    setActiveTab('schedule');
  };

  const handleRegister = async (workshopId: number, participantId: number) => {
    try {
      setLoading(true);
      const res = await fetch(`${API_URL}/workshops/${workshopId}/registrations`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ participantId }),
      });
      if (!res.ok) throw new Error('Registration failed');
      const registration = await res.json();
      setError(null);

      // Refresh
      await loadWorkshops();
      if (selectedWorkshop) {
        await loadWorkshopRegistrations(selectedWorkshop.id);
      }
      alert(`Registered! Status: ${registration.status}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration error');
    } finally {
      setLoading(false);
    }
  };

  const handleCancelRegistration = async (registrationId: number) => {
    if (!confirm('Cancel this registration?')) return;

    try {
      setLoading(true);
      const res = await fetch(`${API_URL}/registrations/${registrationId}/cancel`, {
        method: 'POST',
      });
      if (!res.ok) throw new Error('Cancellation failed');
      setError(null);

      // Refresh
      await loadWorkshops();
      if (selectedWorkshop) {
        await loadWorkshopRegistrations(selectedWorkshop.id);
      }
      if (selectedParticipantId) {
        await loadSchedule(selectedParticipantId);
      }
      alert('Registration cancelled');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Cancellation error');
    } finally {
      setLoading(false);
    }
  };

  const handleAddParticipant = async () => {
    await loadParticipants();
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>SeatFlow - Workshop Registration</h1>
        <div className="tabs">
          <button
            className={`tab ${activeTab === 'workshops' ? 'active' : ''}`}
            onClick={() => setActiveTab('workshops')}
          >
            Workshops
          </button>
          <button
            className={`tab ${activeTab === 'schedule' ? 'active' : ''}`}
            onClick={() => setActiveTab('schedule')}
          >
            Schedule
          </button>
          <button
            className={`tab ${activeTab === 'register' ? 'active' : ''}`}
            onClick={() => setActiveTab('register')}
          >
            Register
          </button>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}
      {loading && <div className="loading">Loading...</div>}

      <main className="app-main">
        {activeTab === 'workshops' && (
          <div className="workshops-view">
            {selectedWorkshop ? (
              <WorkshopDetail
                workshop={selectedWorkshop}
                registrations={registrations}
                participants={participants}
                onBack={() => setSelectedWorkshop(null)}
                onRegister={handleRegister}
                onCancel={handleCancelRegistration}
                onViewSchedule={handleViewSchedule}
              />
            ) : (
              <WorkshopsList
                workshops={workshops}
                onSelectWorkshop={handleViewWorkshop}
              />
            )}
          </div>
        )}

        {activeTab === 'schedule' && selectedParticipantId && (
          <ParticipantSchedule
            participantId={selectedParticipantId}
            schedule={schedule}
            participants={participants}
            onSelectParticipant={(id) => {
              setSelectedParticipantId(id);
              loadSchedule(id);
            }}
            onViewWorkshop={async (workshopId) => {
              const w = workshops.find(ws => ws.id === workshopId);
              if (w) {
                setActiveTab('workshops');
                await handleViewWorkshop(w);
              }
            }}
          />
        )}

        {activeTab === 'register' && (
          <ParticipantForm
            onParticipantAdded={handleAddParticipant}
          />
        )}
      </main>
    </div>
  );
}

export default App;
