# SeatFlow

SeatFlow is a small conference registration app for managing workshop seats, waitlists, cancellations, and participant schedules. It includes a React interface and an ASP.NET Core API backed by SQLite.

## Run it

The backend targets .NET 9 and uses EF Core 9 with SQLite. Node 22 is used for the frontend.

Start the API in one terminal:

```sh
cd backend/SeatFlow.Api
dotnet run --urls http://localhost:5080
```

Start the frontend in another terminal:

```sh
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The API creates `backend/SeatFlow.Api/seatflow.db` on first run and seeds five workshops and six participants. The seed includes a full workshop with an ordered waitlist, partially overlapping workshops, and an overlapping booking that demonstrates conflict skipping during promotion. The available overlapping session makes schedule-conflict rejection easy to try.

## Storybook and checks

```sh
cd frontend
npm run storybook
npm run build-storybook
```

Stories are next to each user-facing React component and cover empty, loading, error, status, full, narrow, and dark variants where relevant. App stories exercise successful registration, waitlisting, duplicate and overlap blocking, action failures, and a successful write whose attendee-list refresh fails.

Run the backend tests with:

```sh
dotnet test SeatFlow.sln
```

The service tests use separate EF Core contexts and a shared temporary file-backed SQLite database. They coordinate independent calls at a start gate and reread persisted rows after completion. HTTP integration tests verify required idempotency headers, conflict responses, resolving `Location` URLs, and UTC timestamp round-trips.

## Architecture

- `backend/SeatFlow.Api` contains the ASP.NET Core API, EF Core model, SQLite persistence, registration rules, seed data, and an in-memory development notification implementation.
- `backend/SeatFlow.Tests` tests registration behavior using temporary SQLite databases.
- `frontend` contains a typed React client, responsive interface, and Storybook stories.
- Domain decisions live in `RegistrationService`; the API maps those results and validation errors to HTTP responses.

SQLite write transactions begin with an immediate lock. That serializes capacity decisions across independent connections before they read the seat count. Registration rows and idempotency records commit together. Each workshop assigns a monotonic queue position to registrations, so FIFO promotion remains stable even when creation timestamps tie. A unique filtered index prevents duplicate active registrations, and a unique idempotency-key record lets a retry return its original registration. Notification calls happen after commit and failures are logged without changing the committed result.

## API overview

- `GET /api/workshops` — workshop details and remaining capacity
- `GET /api/workshops/{id}` — one workshop
- `POST /api/workshops` — create a workshop
- `GET /api/workshops/{id}/registrations` — confirmed, waitlisted, and cancelled registrations
- `POST /api/workshops/{id}/registrations` — register with `Idempotency-Key`
- `GET /api/registrations/{id}` — resolve a created registration URL
- `POST /api/registrations/{id}/cancel` — cancel and promote the first eligible waitlisted person
- `GET /api/participants` — participants available to the UI
- `GET /api/participants/{id}/schedule` — confirmed sessions for a participant

Workshop timestamps are accepted as ISO 8601 values and normalized to UTC. Overlap uses half-open ranges, so one workshop may start exactly when another ends. A participant whose requested workshop has space but overlaps a confirmed session receives a conflict response; a full workshop can still accept that participant on its waitlist. Waitlisted conflicts are checked again at promotion time.

## Known limitations

- SQLite allows one writer at a time; this is suitable for a small conference and intentionally favors correctness over write throughput.
- The development notification sink is in-memory and is not durable across restarts.
- There is no authentication, authorization, deployment setup, or production notification delivery.
