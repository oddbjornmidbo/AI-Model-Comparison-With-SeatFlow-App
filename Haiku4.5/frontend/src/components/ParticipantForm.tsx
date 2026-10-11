import { useState } from 'react';

interface Props {
  onParticipantAdded: () => void;
}

export default function ParticipantForm({ onParticipantAdded }: Props) {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim() || !email.trim()) {
      setError('Name and email are required');
      return;
    }

    try {
      setLoading(true);
      const res = await fetch('http://localhost:5000/api/participants', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email }),
      });

      if (!res.ok) throw new Error('Failed to add participant');

      setSuccess(true);
      setError(null);
      setName('');
      setEmail('');
      onParticipantAdded();

      setTimeout(() => setSuccess(false), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error adding participant');
      setSuccess(false);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="participant-form">
      <h2>Add New Participant</h2>

      {success && <div className="success-message">Participant added successfully!</div>}
      {error && <div className="error-message">{error}</div>}

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="name">Name:</label>
          <input
            id="name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Enter participant name"
            disabled={loading}
          />
        </div>

        <div className="form-group">
          <label htmlFor="email">Email:</label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="Enter email address"
            disabled={loading}
          />
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Adding...' : 'Add Participant'}
        </button>
      </form>
    </div>
  );
}
