# SeatFlow

A local conference workshop registration app built with **.NET 9, ASP.NET Core, EF Core, SQLite, React, TypeScript, Vite, and Storybook**. It includes capacity allocation, deterministic waitlists, conflict-aware promotion, persisted idempotency, and coordinated frontend state.

## Run locally

Requires the **.NET 9 SDK** and **Node.js 22** (Node 20.19+ also meets Vite's runtime requirement). No Docker, login, cloud account, or external database is needed.

From the project root:

```sh
dotnet build SeatFlow.sln
cd frontend
npm ci
cd ..
bash scripts/dev.sh
```

Open **http://127.0.0.1:5177**. The script starts the API on **5057**, uses its URL for the Vite proxy, and stops both processes when interrupted. Override `SEATFLOW_FRONTEND_PORT` and `SEATFLOW_API_PORT` if those ports are busy. In this workspace .NET 9 was installed at `/private/tmp/seatflow-dotnet/dotnet`; use `DOTNET_BIN=/private/tmp/seatflow-dotnet/dotnet bash scripts/dev.sh` if the system SDK is still .NET 8. Use a Node 22 shell (`nvm use 22` if using nvm).

To run the two processes separately:

```sh
# Terminal 1, project root. Uses localhost:5050 from launchSettings.json.
dotnet run --project backend/SeatFlow.Api

# Terminal 2
cd frontend
npm run dev
```

This uses frontend **5173** and API **5050**. `SEATFLOW_API_URL` configures the Vite proxy for other API ports. The API binds to loopback. Default database: `seatflow.db` in the API's working directory. Override `ConnectionStrings__SeatFlow` to use a different SQLite file. `SeatFlow__SeedData=false` disables demo data. Database creation and seeding are automatic on first startup.

## Storybook

```sh
cd frontend
npm run storybook
npm run build-storybook
```

Open **http://127.0.0.1:6006**. Stories cover every reusable UI component, including empty, loading, error, success, disabled, conflict, cancelled, waitlisted, populated, mobile, and theme states. Interactive stories assert registration, selection, modal, and form behavior. The theme toolbar supports both light and dark previews; the application follows the system theme.

## Tests and builds

```sh
# Project root
dotnet build SeatFlow.sln
dotnet test SeatFlow.sln

cd frontend
npm test
npm run build
npm run build-storybook
npx playwright install chromium
npm run test:e2e

# With Storybook running in another terminal
npm run test:storybook
```

The browser suite automatically starts an isolated API/database on **5058** and Vite on **5178**, then exercises real HTTP/database workflows at **1440×900**, **768×1024**, and **390×844**. It covers registration, full-capacity waitlisting, duplicate prevention, cancellation, promotion, conflicting waitlist skips, touching boundaries, participant schedules, re-registration, lost-response retries for both registrations and workshop creation, overnight dates, successful mutation followed by refresh failure, feedback during selection changes and delayed responses, keyboard form validation, console errors, and page overflow. An additional check exercises **320 CSS pixels** and doubled computed text sizes at desktop and phone widths, including navigation and dialogs. Screenshots are saved under `verification/`; failure traces are retained under `frontend/test-results/`.

For a non-default SDK executable set `DOTNET_BIN` when running the browser suite. `PLAYWRIGHT_BROWSERS_PATH` can point to an existing browser installation. In this workspace it is `/private/tmp/seatflow-browsers`. Tests need permission to start local loopback servers.

The xUnit suite covers all 17 specified scenarios plus HTTP integration and direct database-constraint tests. Race tests use real temporary SQLite files, unpooled independent connections and contexts, dedicated worker threads, and a barrier before the business transaction starts. Persisted rows are checked through a new context after every race. Races include final-seat allocation, same-key replay, conflicting requests using one key, concurrent cancellation, duplicates using different keys, concurrent overlapping workshops for one participant, and workshop creation retries. Creation tests also verify atomic rollback and upgrades that retain existing bookings and registration keys. These checks provide contention evidence; they are not a proof of every possible execution or hardware/environment failure.

The frontend unit suite tests snapshot consistency, retries across remounts/selections, new attempts after cancellation, stale asynchronous responses, mutation/refresh feedback, double submission, and registration availability rules. Storybook supplements these application-level tests.

If NuGet's vulnerability feed cannot be reached, restore/build with `-p:NuGetAudit=false` for that invocation. The application keeps auditing enabled by default; unavailable audit data is a warning rather than a compilation error.

## Architecture

```text
backend/SeatFlow.Api
  Domain/       Workshop, participant, registration, persisted request key
  Data/         EF mappings, SQLite constraints/triggers, initialization/seed
  Services/     Registration transaction, workshop creation, queries, notifications
  Contracts/    Explicit JSON response/request models
  Program.cs    Thin endpoints, DI, ProblemDetails
backend/SeatFlow.Tests
frontend/src
  api.ts        HTTP client and typed API errors
  useSeatFlow   Mutation/key lifecycle and complete data snapshots
  workshopDates Local calendar/date ranges and accessible interval text
  components/   Accessible reusable components and stateful stories
  App.tsx       Workshop and participant views
frontend/e2e    Browser workflows against the actual .NET API
```

There are no repository, mediator, or generic unit-of-work layers. The registration service owns explicit business transactions. Endpoints bind requests and select appropriate HTTP responses.

## API

All timestamps are ISO 8601 UTC. Workshop input accepts offsets and normalizes to UTC; the UI displays local time and sends local form values as UTC. Overnight and multi-day sessions show both dates, associating each clock time with its date. IDs are numeric. Errors use `application/problem+json` with `status`, `title`, `detail`, and a machine-readable `code`.

| Method | Endpoint | Behavior |
|---|---|---|
| GET | `/api/health` | Local API status |
| GET | `/api/workshops` | Workshops with confirmed/waitlisted counts and remaining seats |
| GET | `/api/workshops/{id}` | One workshop; resolves the creation Location |
| POST | `/api/workshops` | `{title,start,end,capacity}` with optional `Idempotency-Key`; 201, or 200 on keyed replay |
| GET | `/api/participants` | Existing participants |
| GET | `/api/participants/{id}` | One participant |
| GET | `/api/workshops/{id}/registrations` | Confirmed, waitlisted, and cancelled rows in deterministic order |
| POST | `/api/workshops/{id}/registrations` | `{participantId}` with required `Idempotency-Key`; 201, or 200 on replay |
| GET | `/api/registrations/{id}` | Current registration; resolves its creation Location |
| POST | `/api/registrations/{id}/cancel` | `{registration,promotedRegistration}`; repeat-safe |
| GET | `/api/participants/{id}/schedule` | Active registrations including workshop details |

Validation errors return 400; missing resources 404; duplicate, schedule, or key conflicts 409. SQLite writer timeouts return 503 with `Retry-After`; clients reuse the same key. Registration creation requires a key. Workshop creation accepts a key for backward compatibility, and the UI always supplies one. Both keyed endpoints return `Idempotency-Replayed: true` on replay. A workshop creation made without a key has no replay protection.

## Registration, concurrency, and waitlists

- Available seats produce `Confirmed`; full workshops produce `Waitlisted`.
- A filtered unique index prevents multiple active registrations for the same participant/workshop. Cancelled history remains and a later registration creates a new ID.
- Confirmed intervals use `[start,end)`: touching end/start boundaries are permitted. Waitlisting is allowed even with a confirmed conflict.
- Write transactions use SQLite **BEGIN IMMEDIATE** before reading capacity, conflicts, existing registrations, or keys. SQLite reserves its single writer across connections, so another writer reads the committed result of its predecessor. WAL permits concurrent readers; connections use a bounded 30-second timeout. No application semaphore or process-local lock participates in correctness.
- Cancellation, the freed seat, and one eligible promotion commit atomically. Already-cancelled rows do not trigger another promotion or notification.
- The queue orders by UTC creation ticks, then the database's increasing registration ID. Equal timestamps therefore have a stable tie-breaker. A conflicting candidate remains in position while the first eligible candidate is promoted.
- Promotion runs when a confirmed registration is cancelled in that workshop. Freeing a participant's schedule in another workshop does not automatically revisit a previously skipped queue entry; the UI describes this trigger explicitly.
- Database checks validate capacity, intervals, titles, and status. Triggers independently prevent over-capacity and overlapping confirmed writes, including direct updates and workshop capacity/time changes. Foreign keys preserve referential integrity.

## Idempotency and frontend state

The registration key, fingerprint (`workshopId` + `participantId`), registration ID, and original response snapshot are committed in the same transaction as the registration. Concurrent identical-key requests replay one persisted response and produce one registration/notification. Reusing a key for different IDs returns 409. Keys currently have no expiry.

Replay intentionally returns the **original response**, even if the registration was subsequently promoted or cancelled. GET resources return current state. The frontend refreshes current views after replay and uses the current registration status for feedback. A new registration after cancellation uses a new key.

Pending attempts are kept per workshop/participant in session storage. Lost responses and uncertain server errors retain the key; retry uses the same key even after remounting or switching selections. Definitive success or rejection clears it. Double submissions are also guarded synchronously.

Workshop creation has its own key namespace and persisted original response. Its fingerprint binds the trimmed title, UTC interval, and capacity. The workshop and retry record commit together under the same SQLite writer reservation; simultaneous identical requests create one workshop. A changed normalized request with the same key returns 409. Pending creation details and their key survive page reload in session storage. The form locks those details and offers **Retry workshop creation**; **Resolve workshop** reopens the saved attempt. It permits a new workshop once the previous result is known. HTTP 408/429 retain uncertain keys rather than discarding replay protection.

After a mutation, all workshop summaries, participant data, and rosters reload into a complete snapshot. The app publishes it only after every query succeeds; schedules derive from that same snapshot. Workshop/participant selection is local and older refresh responses cannot overwrite a newer snapshot. A saved mutation whose refresh fails produces an explicit warning and disables further mutations until a successful refresh, rather than presenting a failed action or quietly hiding stale data.

Registration and cancellation notices name their workshop and are scoped to it. Changing selections clears completed action feedback, and a delayed response for another workshop cannot appear in the current detail panel. Stale-data warnings remain visible across navigation because they apply to the whole workspace.

## Notifications

`INotificationService` receives confirmed, promoted, and cancelled events **after the business transaction commits**. Development delivery logs the event. Failures are caught and logged without changing successful domain state. Idempotent replay and repeated cancellation do not duplicate events. Waitlisting alone does not send a confirmation.

## Demo data and useful flows

Four workshops and six participants are seeded for **15 October 2026**. Times below are UTC (the UI shows your local timezone):

| Workshop | Time | Capacity | Demo state |
|---|---|---:|---|
| Designing accessible interfaces | 08:00–09:30 | 4 | Maya and Oliver confirmed |
| Practical AI prototyping | 08:45–10:00 | 2 | Amara and Lucas confirmed; Maya then Sofia waitlisted |
| Design systems that scale | 09:30–11:00 | 3 | Oliver confirmed; touches the first session |
| Shipping resilient APIs | 11:00–12:30 | 6 | Noah confirmed; Sofia has cancelled history |

Cancel Amara's AI registration: Maya is skipped due to her overlapping confirmed workshop, and Sofia is promoted. Maya retains first waitlist position. Try Amara in accessible interfaces before cancellation to see a confirmed schedule conflict. Oliver can attend both accessible interfaces and design systems because their boundaries touch. Sofia can re-register for resilient APIs with a new registration ID.

## Assumptions and limits

- This is a small local conference app; there is no authentication or participant-management CRUD. Participant accounts are seeded. Anyone with local API access can change registrations, as requested.
- Notifications are development logs, not real emails. Delivery is attempted after commit without a durable outbox, crash recovery, automatic retry, or exactly-once delivery guarantee across a process failure.
- SQLite serializes writers. Under sustained load, a request can time out and must retry. Tests cover simultaneous requests on one local file; they do not establish distributed/network-filesystem guarantees.
- The database uses `EnsureCreated` plus explicit triggers rather than a migration history. This revision includes a tested additive startup upgrade for the workshop creation retry table, preserving existing data. Further schema changes need their own migration plan. Do not delete a real conference database to upgrade it.
- FIFO timestamps come from the application clock, with registration ID as the tie-breaker. Strong clock-order guarantees across clock rollback or machines with skewed clocks are not claimed.
- The frontend reloads small-conference rosters together. This favors coherence and simple code over pagination and large-conference scale. Changes in another browser are visible on Refresh or after your next mutation; there is no push subscription. Multi-query reads do not claim a database-wide snapshot against concurrent external writers.
- Session storage preserves uncertain keys in the current tab/session; clearing browser data, closing that session, or unavailable browser storage can lose a pending key. Active registration duplicates remain database-protected; identical workshop content is allowed when submitted as separate logical creations, so a creation key matters.
- A formal accessibility certification is not claimed. The implementation uses labelled semantic controls, native modal focus behavior, visible focus, textual statuses, live feedback, readable themes, practical touch targets, and reduced-motion support; responsive keyboard/browser verification is documented in `verification/REVIEW.md`.
- Production dependencies had **zero reported advisories** in the final `npm audit --omit=dev` check. The full development toolchain audit reports 39 advisories (8 moderate, 31 high) in Storybook/test-runner/Vitest transitive dependencies. Compatible patches were applied; the remaining suggested fixes require major toolchain changes. This is a known tooling limitation, not a claim that every dependency is vulnerability-free. Storybook and test tools are local development services.
