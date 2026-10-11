# SeatFlow

A small local workshop registration application using .NET 9, ASP.NET Core, EF Core, SQLite, xUnit, React, TypeScript, and Vite.

## Run

Requirements: .NET 9 SDK, Node.js 22, and npm. There is no Docker, authentication, external database, or cloud dependency. Internet access is needed for the initial package restore/install.

From the `SeatFlow` directory, run the backend:

```sh
dotnet restore backend/SeatFlow.sln
dotnet run --project backend/SeatFlow.Api
```

The API listens at **http://localhost:5080**. In a second terminal, run the frontend:

```sh
cd frontend
npm ci
npm run dev
```

Open **http://localhost:5173**. Vite proxies `/api` to the local backend, so no CORS configuration is needed. Stop either server with Ctrl+C. If port 5173 is occupied, stop that process or choose another port with `npm run dev -- --port 5174`.

For this workspace, a .NET 9 SDK was also installed locally because the system SDK is .NET 8. From the `SeatFlow` directory, you can use it with:

```sh
export DOTNET_ROOT="$(cd ../../work/dotnet && pwd)"
export DOTNET_CLI_HOME="$(cd ../../work/dotnet-home && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"
```

The portable SDK is workspace tooling outside the application source; on another machine, install .NET 9 normally.

## Build and test

```sh
dotnet build backend/SeatFlow.sln
dotnet test backend/SeatFlow.sln --no-build
cd frontend
npm ci
npm run build
```

Verified with .NET SDK 9.0.318 and Node.js 22.22.3: the backend solution build has zero warnings/errors, all **32 xUnit tests pass**, and the frontend production build passes.

The backend tests use independent, temporary SQLite files and real EF queries/transactions. They cover confirmations, full workshops, FIFO promotion, conflict skipping, overlap and boundary times, active duplicates, re-registration, idempotency and payload mismatch, repeated cancellation, notification failures, concurrent requests, and HTTP validation/responses. The frontend build checks TypeScript and produces `frontend/dist`.

## Try it

The first backend startup creates `backend/SeatFlow.Api/seatflow.db` and seeds four workshops, six participants, and a few registrations. Workshop times are on the following UTC day at initial seeding. The first two workshops overlap; the last two meet at a boundary and can both be attended.

Select **Building useful APIs**. It starts full, with Sam followed by Riley on the waitlist. Sam is already confirmed in the overlapping **Designing better interfaces** workshop. Cancel Alex or Jamie: Sam stays waitlisted and Riley is promoted. Select a participant to see their confirmed schedule. Register an existing participant, or create a workshop using the form.

Data persists across restarts. To reset the demo, stop the backend and delete `backend/SeatFlow.Api/seatflow.db` (and any matching `-wal` / `-shm` files), then restart it.

## Architecture

- `backend/SeatFlow.Api/Models` defines workshops, participants, registrations, and persistent idempotency keys.
- `Data/SeatFlowDbContext.cs` configures SQLite constraints, relationships, and UTC timestamps. A filtered unique index provides an additional safeguard against duplicate active registrations.
- `Services/RegistrationService.cs` owns registration, overlap checks, FIFO promotion, cancellation, and idempotency. Controllers handle HTTP input/output and read-only queries.
- Each registration/cancellation starts a SQLite **immediate transaction before reading state**. SQLite serializes writers to the same database file, including independent processes. Capacity, schedule conflicts, and idempotency checks therefore use the state left by the preceding committed writer. A 30-second lock timeout fails safely with HTTP 503; it never overbooks.
- `INotificationService` is called after commit, outside the database lock. Its development implementation records events in memory and logs them. Exceptions are caught and logged, preserving the successful registration or cancellation.
- `frontend/src` contains the typed API client and React UI. Mutation refreshes update workshop counts, registrations, and the selected participant's schedule. An uncertain registration attempt retains its key for retry, including across page reloads.

## API

| Method | Path | Behavior |
| --- | --- | --- |
| GET | `/api/workshops` | Workshops with confirmed count, remaining capacity, and waitlist count |
| POST | `/api/workshops` | Create from `{ "title": "Workshop", "startTime": "2030-01-01T09:00:00Z", "endTime": "2030-01-01T10:00:00Z", "capacity": 2 }` |
| GET | `/api/workshops/{id}` | One workshop and its counts |
| GET | `/api/participants` | Existing participants for the registration selector |
| GET | `/api/workshops/{id}/registrations` | All registrations, including cancelled history, in FIFO order |
| POST | `/api/workshops/{id}/registrations` | Register using `{ "participantId": 1 }` and optional `Idempotency-Key` header |
| GET | `/api/registrations/{id}` | Current registration state |
| POST | `/api/registrations/{id}/cancel` | Cancel and promote the first eligible waitlisted participant |
| GET | `/api/participants/{id}/schedule` | Confirmed workshops ordered by start time |

Creation returns HTTP 201 and a usable `Location`; cancellation returns 200, including repeated cancellation. Invalid input returns 400, missing resources 404, and duplicate registrations/schedule conflicts/idempotency payload mismatches 409. Errors use JSON Problem Details. Enum statuses are strings: `Confirmed`, `Waitlisted`, `Cancelled`.

## Assumptions and limitations

- Time intervals are half-open: `[start, end)`. Workshops ending when another starts do not conflict. Clients should send ISO timestamps with `Z` or a numeric offset. Input timestamps are normalized to UTC; the UI displays times in the browser's local timezone.
- An available seat with a schedule conflict returns 409. A full workshop may accept a conflicting participant onto its waitlist; eligibility is checked again at promotion.
- FIFO uses creation timestamp and then registration ID to break ties. Promotion runs when a confirmed registration in that workshop is cancelled. Skipped entries stay waitlisted. If everybody conflicts, the seat remains open. There is no background sweep after another workshop is cancelled.
- A participant can register again after cancellation using a fresh idempotency key (or no key). Old registrations remain visible as history.
- Idempotency keys are optional, case-sensitive, global to registration creation, and contain 1–200 printable characters. A key is stored atomically with a successful registration and may only be reused for the same workshop/participant pair. Validation failures do not consume a key. Replays return the same registration ID with its **current** status and do not resend notifications; both original and replay responses use 201. Keys are retained indefinitely in this small application.
- Notifications are best effort, in memory/logs, without persistent delivery retries. A process stopping after database commit can lose a notification. Initial demo seed records do not send notifications.
- Only existing seeded participants can register. There is no participant creation UI/API or workshop editing/deletion, as these are outside this feature.
- SQLite has a single writer. This is appropriate for the small local app; heavy traffic may reach the lock timeout. Clients should retry uncertain registration failures with the same key.
- Schema creation uses `EnsureCreated`, without migrations. Reset the local database if you change the schema. The development server is HTTP and intended for local use.

You can override the database with `ConnectionStrings__SeatFlow="Data Source=/path/to/seatflow.db;Default Timeout=30;Foreign Keys=True"`. Set `SeedData=false` to initialize an empty database, as the HTTP tests do.
