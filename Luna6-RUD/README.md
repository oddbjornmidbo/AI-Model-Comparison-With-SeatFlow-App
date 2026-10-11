# SeatFlow

SeatFlow is a small workshop registration app for a conference. It includes an ASP.NET Core API backed by SQLite and a React + TypeScript interface.

## Run the backend

From `backend/SeatFlow.Api`:

```sh
dotnet run --urls http://localhost:5000
```

The API creates `seatflow.db` in the current working directory on first run and seeds four workshops and six participants. The browser app expects the API at `http://localhost:5000`; set `VITE_API_BASE` to change it.

## Run the frontend

From `frontend`:

```sh
npm install
npm run dev
```

Use Node.js 22.12 or newer (required by the current Vite toolchain).
Open `http://localhost:5173`.

## Run tests

From `backend`:

```sh
dotnet test SeatFlow.Tests/SeatFlow.Tests.csproj
```

## Architecture

- `SeatFlow.Api` contains the EF Core entities and SQLite context, the registration service that owns registration, cancellation, conflict, waitlist, and promotion rules, and the minimal HTTP API.
- `INotificationService` is the notification boundary. The development implementation logs and keeps an in-memory message list. Notification exceptions are contained after state changes.
- `SeatFlow.Tests` tests business outcomes using an in-memory SQLite database.
- `frontend` is a Vite React app. It loads workshop/participant lists and uses the HTTP API for registration, cancellation, and schedules.

## Assumptions

- Workshop times are stored and compared as `DateTimeOffset`; overlap uses half-open intervals, so a workshop ending at 10:00 does not conflict with one starting at 10:00.
- Email addresses are unique for participants. The UI selects seeded/existing participants; participant management is outside this small app's scope.
- A waitlisted participant may remain queued when they have another overlapping confirmed workshop. Promotion skips them and leaves them in FIFO position until a later opening.
- A repeated idempotency key is scoped globally and only replays when its workshop and participant match. Reusing it for a different request returns `409 Conflict`.
- Registration changes are serialized within this single API process to protect capacity decisions. Run one API instance when using the local SQLite file.
- Cancelled registrations remain visible in history. Re-registering the same participant for the same workshop reuses that registration record and updates its timestamp/status.

## Known limitations

- Notifications are development-only, in-memory/log based, and disappear when the API process exits.
- There is no participant creation/edit UI, authentication, authorization, or multi-instance coordination.
- The local seed dates are generated relative to the first API startup and are not refreshed after the database is created.
