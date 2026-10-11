# SeatFlow

SeatFlow manages workshop registrations for a small conference: workshops with capacity, participants,
registrations with a waitlist, schedule-conflict checks, and an in-memory notification service.

- Backend: .NET 9, ASP.NET Core minimal APIs, EF Core with SQLite, xUnit
- Frontend: React 19, TypeScript, Vite

## Running the backend

Requirements: .NET 9 SDK.

```bash
cd backend
dotnet build
dotnet test
dotnet run --project SeatFlow.Api --urls http://localhost:5080
```

- The API listens on `http://localhost:5080` (Swagger/OpenAPI is not included).
- The SQLite database `seatflow.db` is created in the working directory on first start, and seeded if it is empty.
  Delete the file to reset the data. The connection string is `ConnectionStrings:SeatFlow` in `appsettings.json`.

## Running the frontend

Requirements: Node 20.19+ (or 22.12+) and the backend running on port 5080.

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173, /api is proxied to the backend
npm run build      # type-check + production build into dist/
```

## Architecture

```
backend/
  SeatFlow.Api/
    Domain/          Workshop, Participant, Registration, RegistrationStatus (overlap logic on Workshop)
    Services/        RegistrationService (all registration/cancel/promotion rules)
                     CatalogService (workshop list/create, participants, registrations and schedule queries)
    Notifications/   INotificationService + in-memory development implementation (logs and records)
    Data/            EF Core DbContext, schema creation and seed data
    *Endpoints.cs    Minimal API routes: validation, HTTP status mapping, Idempotency-Key header
    SeatFlowExceptionHandler.cs   Maps domain errors (NotFound/Conflict/Validation) to ProblemDetails
  SeatFlow.Tests/    Business-rule tests on the real services (SQLite file per test),
                     concurrency and idempotency tests, and HTTP tests via WebApplicationFactory
frontend/
  src/App.tsx        Page state, actions and reload-after-change
  src/components/    Workshop table, workshop detail (register/cancel), participant schedule, status badge
  src/api.ts         Typed fetch client that surfaces server messages
```

Domain rules live in `RegistrationService`, not in the endpoints. The endpoints only validate request shape and translate results to HTTP.

### API

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/workshops` | Capacity, confirmed/waitlist counts, remaining seats |
| POST | `/api/workshops` | `{ title, startsAt, endsAt, capacity }` → 201, or 400 with field errors |
| GET | `/api/workshops/{id}/registrations` | Active registrations: confirmed first, then waitlist in FIFO order |
| POST | `/api/workshops/{id}/registrations` | `{ participantId }`, optional `Idempotency-Key` header. 201 new, 200 replay, 404, 409 |
| POST | `/api/registrations/{id}/cancel` | 200 with the cancelled registration and the promoted one, if any. Repeat cancel is 200 no-op |
| GET | `/api/participants` | Participant list (used by the UI) |
| GET | `/api/participants/{id}/schedule` | All of a participant's registrations with their status |

Errors use ProblemDetails. Validation failures return 400 with per-field `errors`.

## Important assumptions

- **Schedule conflicts are checked at registration time, even for waitlisting.** A participant who already has a
  confirmed workshop that overlaps the new one gets 409 and no registration, whether or not seats are free.
  A waitlisted participant can still become overlapping later (for example, by being confirmed elsewhere).
  Such a waitlisted entry is skipped during promotion and stays on the waitlist.
- **Overlap uses half-open ranges.** `[start, end)`, so 10:00–12:00 and 12:00–14:00 do not overlap.
- **One promotion per freed seat.** Cancelling a confirmed registration promotes at most one participant.
  Cancelling a waitlisted registration frees no seat and promotes nobody.
- **Duplicates.** A participant may have only one non-cancelled registration per workshop. The database enforces
  this with a partial unique index. Cancelled registrations may be followed by a new one.
- **Idempotency-Key** is optional, 1–200 characters, and globally unique. A repeat with the same key returns the
  original registration with 200 and creates nothing. Reusing a key for a different workshop or participant returns 409.
  Keys never expire.
- **Notifications** are sent after commit for: a registration becoming Confirmed, a waitlisted participant being
  promoted, and a cancellation that changed state. Waitlisting alone sends nothing, and repeating a cancel sends nothing.
  The development implementation logs each notification and keeps it in memory. Failures are logged and swallowed,
  so they never roll back a registration or cancellation.
- **Times** are stored as UTC and displayed in the browser's time zone. Seed data uses Europe/Oslo (+01:00 in November).
- **No authentication.** Participants are chosen from the seeded list in the UI.
- **Seed data.** 5 workshops (Minimal APIs / DDD partially overlap; React / xUnit partially overlap) and 6 participants.
  A few registrations are created through the real service so the seed already shows a waitlist.

## Concurrency

Every registration and cancellation runs under one in-process `SemaphoreSlim`. This makes the "count seats, then insert"
step atomic, so when two requests race for the final seat only one becomes Confirmed. Unique indexes back up
the duplicate and idempotency rules at the database level.

## Known limitations

- **The concurrency guarantee is process-local.** It holds for one API process using one SQLite file. Running
  several API instances against the same file would not protect capacity. Duplicate and idempotency rules would still hold
  because of the unique indexes.
- **No migrations.** The schema is created with `EnsureCreated`. Schema changes require deleting `seatflow.db`.
- **Notifications are not persisted** and cannot be read back through the API.
- **No pagination, search or sorting controls** on the UI. Lists are small and fully loaded.
- **Workshop creation exists in the API only.** The UI covers the requested views and actions.
- **Frontend is checked by type-check and build only.** There are no UI tests, and I did not run it in a browser
  against the API. Accessibility is built in (labelled controls, text plus icon for each status, focus management,
  mobile table reflow), but it has not been audited.
- Cancelling asks for no confirmation in the UI.
