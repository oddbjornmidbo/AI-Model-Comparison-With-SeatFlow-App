# SeatFlow

Workshop registration for a small conference. Full-stack: ASP.NET Core (.NET 9) + EF Core/SQLite on the backend, React + TypeScript + Vite on the frontend.

## Running the backend

Requires the .NET 9 SDK.

```bash
cd backend/src/SeatFlow.Api
dotnet run --urls http://localhost:5190
```

On first start the SQLite database `seatflow.db` is created in the API's working directory and seeded (6 participants, 5 workshops, a few demo registrations including a full workshop with a waitlist). Delete the file to reseed.

Run the tests:

```bash
cd backend
dotnet test SeatFlow.sln
```

## Running the frontend

Requires Node 20+ and the backend running on port 5190.

```bash
cd frontend
npm install
npm run dev      # http://localhost:5173, proxies /api to localhost:5190
npm run build    # type-check + production build
```

## Architecture

- `backend/src/SeatFlow.Core` — domain and business rules.
  - `Entities.cs`: `Workshop`, `Participant`, `Registration`, `RegistrationStatus`.
  - `SeatFlowService.cs`: all registration rules (capacity, waitlist, promotion, schedule conflicts, idempotency, cancellation). Endpoints only translate HTTP to these calls.
  - `Notifications.cs`: `INotificationService` and the development implementation `InMemoryNotificationService` (keeps notifications in memory and logs them).
  - `SeatFlowDbContext.cs`: EF Core model with a unique index on `IdempotencyKey`.
  - `SeedData.cs`: development seed data.
  - `Exceptions.cs`: `NotFoundException` (404), `ConflictException` (409), `ValidationFailedException` (400).
- `backend/src/SeatFlow.Api` — minimal API endpoints in `Program.cs`. A middleware maps the domain exceptions to ProblemDetails responses.
- `backend/tests/SeatFlow.Tests` — xUnit. Most tests drive `SeatFlowService` against a real SQLite file. A few use `WebApplicationFactory` for HTTP status codes and the Idempotency-Key header.
- `frontend/src` — `api.ts` (typed fetch client) and `App.tsx` (one screen: workshop list, selected workshop's Confirmed/Waitlist/Cancelled sections, register, cancel, participant schedule).

## API

| Method | Path | Notes |
|---|---|---|
| GET | `/api/workshops` | Includes confirmed/waitlisted counts and remaining capacity |
| POST | `/api/workshops` | `{ title, startTime, endTime, capacity }`, 201 or 400 |
| GET | `/api/participants` | |
| GET | `/api/workshops/{id}/registrations` | All statuses; confirmed first, then waitlist FIFO, then cancelled |
| POST | `/api/workshops/{id}/registrations` | `{ participantId }`, optional `Idempotency-Key` header. 201 new, 200 replay, 409 duplicate or schedule conflict |
| POST | `/api/registrations/{id}/cancel` | 200 whether or not it was already cancelled, 404 if unknown |
| GET | `/api/participants/{id}/schedule` | Confirmed and waitlisted registrations, ordered by workshop start |

Timestamps are UTC. Status values are serialised as strings.

## Business rules and assumptions

- **One active registration per workshop.** A participant can register again only after their previous registration is cancelled.
- **Capacity counts only Confirmed registrations.** A new registration is Confirmed if there is a free seat, otherwise Waitlisted.
- **Schedule conflicts on direct registration.** If a seat is free but the participant already has a Confirmed overlapping workshop, the request is rejected with 409. The registration is not created.
- **Waitlisting is not blocked by conflicts.** A participant may join a full workshop's waitlist even if it overlaps a workshop they are confirmed for. Conflicts are checked again at promotion time (rule 7).
- **Overlap is half-open.** A 09:00–11:00 workshop and an 11:00–12:00 one do not conflict.
- **Promotion** happens once per freed seat. It takes the oldest waitlisted registration (by `CreatedAt`, then `Id`) that has no conflict. Skipped registrations keep their waitlist position.
- **Idempotency-Key.** The key is stored on the registration with a unique index. Replaying the same key returns the original registration with 200 and sends no new notifications. Reusing a key for a different workshop or participant returns 409. Keys are limited to 255 characters.
- **Cancelling** a waitlisted registration does not promote anyone. Cancelling a Confirmed registration promotes at most one participant.
- **Notifications** are dispatched after the database change is committed. Exceptions from `INotificationService` are logged and swallowed, so they never roll back a registration or cancellation.
- **Seeding** uses the service for demo registrations, so the rules apply to the seed data as well.
- **Time zone:** all times are UTC. The UI displays UTC, and the input is ISO-8601 with an offset.

## Concurrency

Registration and cancellation are serialised by a process-wide `SemaphoreSlim` inside `SeatFlowService`. Capacity and schedule checks are read-then-write, and SQLite only supports one writer at a time. The lock makes sure two requests for the last seat cannot both see a free seat. A test fires 10 concurrent registrations at a capacity-1 workshop and checks that exactly one is Confirmed.

## Known limitations

- The lock only protects a **single API process**. Running several instances against the same SQLite file would need a database-level guard, such as `BEGIN IMMEDIATE` transactions or a unique constraint on confirmed seats.
- Schema is created with `EnsureCreated`, not EF migrations. Schema changes require deleting `seatflow.db`.
- Notifications are only logged and kept in memory. There is no delivery, retry, or outbox.
- No authentication or authorisation, as specified. Participants are picked from a dropdown.
- The UI has no create/edit screens for workshops or participants. Workshops can be created through the API.
- The frontend has no automated tests. It was verified by building it and by a manual browser run of register, waitlist, and cancel-and-promote.
