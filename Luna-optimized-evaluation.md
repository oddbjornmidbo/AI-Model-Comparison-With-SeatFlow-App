# SeatFlow evaluation: Luna-optimized

## Executive summary

**Luna-optimized is a strong implementation of the SeatFlow assignment.** The live interface completed the principal registration, waitlist, cancellation, promotion, and schedule scenarios without an observed runtime failure. The backend uses an immediate SQLite write transaction before checking capacity or schedule conflicts, and persists registrations with idempotency keys in that transaction. All 16 backend tests passed; both backend and frontend builds succeeded.

The main weakness is inclusive usability: important metadata uses text as small as 9–10 px, and some action targets are 27–34 px high. The responsive layout works, but the compact type makes the otherwise polished mobile interface harder to read and tap. A confirmed frontend feedback defect can misdescribe a cancelled idempotent replay as a waitlist success; it does not corrupt registration state.

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| -------------- | -------: | ---------------: | ---------: | --------: |
| Luna-optimized | 8.8 | 9.1 | 7.6 | 25.5 |

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| Luna-optimized | 8.3 | 7.8 | 6.7 | 7.6 |

Design is the average of its three components: (8.3 + 7.8 + 6.7) / 3 = 7.6. Total is 8.8 + 9.1 + 7.6 = 25.5.

## Scope and method

This is a **single-app evaluation** of `Luna-optimized` at `http://localhost:5173/`, using the original SeatFlow rubric. I completed the browser and design assessment before opening the source code. I then built both projects, ran the backend tests, and critically checked a GPT-6 Astra source review against the code and observed behavior. I did not read prior evaluations of Luna or use another implementation to set this score. I made no source changes. Functional testing changed the running app's registration data through its UI.

### Browser findings

The initial browser view loaded five workshops and six participants. Workshop cards showed remaining seats and confirmed counts; selecting a card loaded its confirmed, waitlisted, and cancelled registrations. The participant selector and schedule panel worked and stayed aligned. The schedule intentionally listed confirmed sessions only; all three registration states remained visible in workshop details.

| UI scenario | Observation |
| --- | --- |
| Full workshop and FIFO | Casey and then Jordan joined the full **Designing for the real world** workshop. Cancelling Alex promoted Casey, left Jordan waitlisted, and kept the confirmed count at 2/2. Casey's schedule updated. |
| Conflict-aware promotion | Jordan and Sam were on Designing's waitlist with overlapping confirmed sessions; Taylor was behind them and eligible. Cancelling Jamie skipped Jordan and Sam without removing them, promoted Taylor, and kept capacity at 2/2. |
| Available seat and conflict | Registering Jordan into an available seat in **Creative systems in practice** reduced the displayed free-seat count and added the session to Jordan's schedule. When Casey had a confirmed overlapping session, the registration action was disabled with an explicit explanation. |
| Boundary and mobile operations | Taylor could register for **Building better teams** starting exactly when Designing ended. On mobile, I filled Building, waitlisted Alex, cancelled a confirmed place, and saw Alex promoted with counts and schedule refreshed. |
| Duplicate and second cancellation | The UI disabled a duplicate active registration and explained why. A cancelled row became **Ended** with no second cancel action; the backend test separately verified idempotent second cancellation. |

Success messages, selected-workshop details, counts, and schedules stayed consistent after the tested mutations. Cancelled registrations remained visible with a text status. No unexpected console errors or warnings were observed. HTTP-level idempotency and final-seat concurrency were assessed through source and automated tests, since the UI does not expose a practical way to drive those conditions directly.

### Responsive Design

I inspected and interacted at approximately **1440 × 900**, **768 × 1024**, and **390 × 844**. No unintended document-width overflow was found at those widths. Desktop uses a clear workshop/detail split. Tablet retains two columns; the workshop column is narrow but usable. Mobile stacks the workshop list, details, form, attendee groups, and schedule. The mobile registration and cancellation actions worked, and status labels stayed readable as words rather than relying on color.

The mobile screen requires substantial scrolling from the workshop list to its details, and the compact type and narrow controls reduce comfort. This is a responsive usability weakness, not a broken layout: actions remained reachable and functional. Zoom/reflow beyond the tested viewport sizes was not directly verified.

### Universal Design and accessibility

**Confirmed strengths:** the page uses a main region and ordered headings at the page/panel level; fields have visible labels and native selects; workshop selection uses buttons with `aria-pressed`; the attendee groups use lists; schedule entries use an ordered list; feedback uses status/alert roles; focused controls have a visible outline. The observed keyboard Tab sequence reached the main controls. Statuses have text labels, and disabled registration actions have nearby explanations.

**Confirmed inclusive-usability weakness:** several important pieces of information, including seat badges, status badges, dates, and schedule metadata, are set to **9–10 px**. Registration selects and primary buttons use roughly **11–12 px** text. Cancel buttons are **34 px** high and feedback dismissal is **27 × 27 px**. These choices make reading and touch interaction harder, especially on mobile or for users with reduced vision or motor precision.

**Likely semantic concern:** the “Attendees” heading and the nested Confirmed/Waitlist/Cancelled group headings are all `h3`, so their heading levels do not express the visual nesting. A screen-reader session was not run; this is a source-based concern rather than a demonstrated assistive-technology failure. I did not find a keyboard blocker, hover-only requirement, or color-only status in the inspected flows.

### Build and automated tests

| Check | Command | Result |
| --- | --- | --- |
| Backend build | `dotnet build SeatFlow.sln --no-restore --disable-build-servers -m:1 -v:minimal` | Passed; **0 warnings, 0 errors**. |
| Backend tests | `dotnet test SeatFlow.sln --no-restore --no-build -m:1 --logger 'console;verbosity=minimal'` | **16 passed, 0 failed, 0 skipped**. |
| Frontend build | `npm run build` (`tsc -b` and Vite) | Passed. |

The backend tests cover available and full workshops, FIFO promotion, timestamp ties, skipping conflicting waitlistees, overlap and touching boundaries, active duplicates and re-registration after cancellation, idempotency, second cancellation, notification failure, schedules, and three concurrent-request cases. The concurrency tests use independent EF contexts on the same file-backed SQLite database, coordinate their start, and assert persisted results. Their start gate makes a race plausible but does not prove both calls reached the database lock at exactly the same instant. API integration tests check creation URLs, UTC handling, a required idempotency header, and duplicate conflict responses.

Missing useful coverage includes concurrent cancellation versus registration/promotion, notification failure specifically during promotion, and the frontend's cancelled-replay feedback case. The project contains Storybook interaction assertions, but the production frontend build did not execute them; I did not count them as passing automated tests.

### Astra code-review findings and lead assessment

The GPT-6 Astra reviewer recommended **8.8/10** for code quality. I independently confirmed the main points in [RegistrationService.cs](Luna-optimized/backend/SeatFlow.Api/RegistrationService.cs), [SeatFlowDbContext.cs](Luna-optimized/backend/SeatFlow.Api/SeatFlowDbContext.cs), [RegistrationServiceTests.cs](Luna-optimized/backend/SeatFlow.Tests/RegistrationServiceTests.cs), and [App.tsx](Luna-optimized/frontend/src/App.tsx):

- Registration opens an **immediate, non-deferred SQLite transaction before** reading the idempotency record, duplicate registration, capacity count, or participant schedule. This serializes final-seat decisions across connections. A filtered unique index also prevents two active registrations for the same participant and workshop.
- The idempotency key is stored persistently with the new registration in one transaction. Replaying the same key/request returns the same registration; reusing it for a different request returns a conflict. The UI retains a key in memory for an uncertain retry, though a reload loses it.
- Cancellation, the FIFO eligibility scan, and promotion commit together. Queue positions, assigned inside the writer transaction and uniquely indexed per workshop, give stable order even when timestamps tie. Skipped conflicting participants stay waitlisted. Overlap uses strict inequalities, permitting back-to-back workshops.
- The `INotificationService` implementation sends confirmation, cancellation, and promotion notices after commit. Each send failure is caught and logged, so it cannot undo the domain operation. Delivery is best effort and not durable across a crash, which the README discloses as a development limitation.
- The required API routes, validation responses, seed data, and README are present. Business rules are in a service rather than the HTTP handlers. The architecture is compact and largely cohesive.

I agree with Astra's central verdict and with its caution about the test gate: passing concurrency tests strengthen the transaction argument but do not alone establish it. The actual immediate-transaction boundary is the decisive evidence. I also verified that the frontend issue below is a real code path, but it requires an unusual lost-response/cancel/retry sequence and was **not** observed during ordinary browser actions. I do not treat the 30-second SQLite lock timeout or best-effort notification sink as failed core requirements; they are operational limits of this small-app design.

### Important defects, risks, and limitations

1. **Confirmed code defect, misleading UI feedback:** `App.tsx` treats every registration response other than `Confirmed` as “added to the waitlist.” An idempotent replay can return a registration now `Cancelled`. If a first response is lost, another client cancels that booking, and this client retries its saved key, the UI can report waitlist success although refreshed data correctly shows cancellation. Booking state remains correct.
2. **Confirmed inclusive-usability weakness:** important text and secondary touch targets are unusually small. This is the principal reason Universal Design scores below the visual and functional scores.
3. **Likely operational risk:** sustained SQLite write contention can exhaust the configured 30-second lock wait and surface as a generic server error. This does not imply oversold capacity.
4. **Known limitation:** notifications are in memory and have no durable retry/outbox. A process crash after commit can lose a notice; notification exceptions themselves do not roll back bookings.
5. **Test gap:** no automated test directly verifies a promotion when notification delivery fails or races cancellation against a new registration. The code's transaction structure supports correctness, but those paths have less direct test evidence.

### Strengths

The implementation addresses the assignment's hardest invariants with clear database-backed transactions and credible integration-style tests. Its browser behavior matched the business rules in both ordinary and conflict-aware promotion flows. The interface is visually coherent, communicates registration states in text, refreshes related panels after changes, and remains usable on desktop, tablet, and mobile. The README explains how to run it, its architecture, assumptions, and limitations.

### Scores

- **Code quality and correctness: 8.8/10** — strong domain correctness and test quality; one narrow frontend feedback bug and some untested edge races.
- **Requirement fulfillment: 9.1/10** — essentially all specified technology, API, domain, UI, seed, test, and documentation requirements are met in the examined evidence.
- **Visual Design: 8.3/10** — cohesive, polished hierarchy and status treatment; overly small secondary type.
- **Responsive Design: 7.8/10** — functional reflow at all three sizes, with a compressed tablet column and lengthy, small-type mobile flow.
- **Universal Design: 6.7/10** — good baseline semantics and keyboard support, limited by small text and targets and a minor heading-nesting concern.
- **Design average: 7.6/10**.
- **Total: 25.5/30**.

## Overall verdict

Luna-optimized satisfies the SeatFlow task well, especially the concurrency, idempotency, waitlist, and scheduling rules. The most valuable improvements would be larger text and touch targets, and accurate feedback for all three possible statuses on a registration replay. Those findings do not change the observed success of the core booking flows.
