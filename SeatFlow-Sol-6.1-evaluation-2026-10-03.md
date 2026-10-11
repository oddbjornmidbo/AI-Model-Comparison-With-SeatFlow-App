# SeatFlow Sol-6.1 evaluation

## Executive summary

Sol-6.1 fulfills the SeatFlow assignment well. The live application handled available bookings, overlap rejection, waitlisting, cancellation, conflict-aware promotion, re-registration, and schedule updates. Its backend has particularly strong transaction boundaries and meaningful concurrency tests. The interface is polished and works at all three requested sizes without horizontal overflow. The clearest defect is a narrow frontend timestamp sort that can display a waitlist in the wrong order when ISO timestamps use different fractional-second precision. Small, muted text and compact cancellation controls limit accessibility.

**Score: 26.4/30.** This places Sol-6.1 just behind Luna-optimized (26.5) and ahead of RUD-Opus (26.0) on the same rubric. A tenth of a point is not a meaningful claim that one application is categorically better.

## Scores

| Category | Score | Reason |
| --- | ---: | --- |
| Code | **9.0/10** | Strong transactional domain service, database safeguards, retry behavior, and credible concurrency tests; narrow frontend defects and no frontend tests. |
| Requirements | **9.2/10** | All core requirements are present and the exercised flows worked; the frontend can misstate queue order in a timestamp edge case. |
| Visual | **8.5/10** | Cohesive layout, hierarchy, and status styling; secondary information is too subdued. |
| Responsive | **8.8/10** | Desktop, tablet, and phone layouts keep actions reachable with no measured horizontal overflow; the phone view is long. |
| Universal Design | **7.3/10** | Semantic buttons, labels, status text, alerts, and visible focus; 9–12 px metadata and small cancellation targets reduce legibility and touch comfort. |
| **Design average** | **8.2/10** | Arithmetic mean of Visual, Responsive, and Universal Design, rounded once. |
| **Total** | **26.4/30** | Code + Requirements + Design. |

## Method and scope

I evaluated the live UI at [http://localhost:5173/](http://localhost:5173/) before reading the implementation. The process serving that URL runs from a separate output directory. I compared its frontend and backend source trees with the `Sol-6.1` workspace copy, excluding generated dependencies, build output, and databases; they were identical. This makes the live observations applicable to the reviewed source. I used the original SeatFlow assignment and the same scoring rubric as the six-app evaluation. The previous report and its scores were read **only after** setting the Sol-6.1 scores above.

The live data was a test fixture, and browser actions changed local registrations. I assessed layout at **1440 × 900**, **768 × 1024**, and **390 × 844**, interacting with workshop details and actions at each size. I measured document width against viewport width. I inspected a keyboard focus transition and accessible names, but did not run a screen reader or zoom test. Runtime behavior, confirmed source defects, and unverified risks are distinguished below. An Astra agent on **high** mode independently reviewed the Sol-6.1 code and existing tests without reading earlier evaluations. A stopped server would not have affected the score; this server was already running.

## Browser findings

- **Available seat and conflict:** An overlapping booking for Alex in Designing better interfaces was rejected with a clear message. A nonconflicting Morgan booking succeeded, changed the roster and seat count, and appeared immediately in Morgan's schedule. The duplicate action then became unavailable.
- **Full workshop and queue:** Nora joined the full Building useful APIs workshop behind Sam and Riley. Cancelling confirmed Alex left conflicting Sam at the front and promoted eligible Riley. The confirmed count stayed at capacity, Nora moved forward, and Riley's schedule showed the confirmed workshop.
- **Cancellation and re-registration:** Alex's schedule cleared after cancellation. Re-registering Alex in the now-full workshop added a new waitlist entry behind the existing waiters; the cancelled historical entry remained visible. Repeat registration was disabled while the new entry was active.
- **Other controls:** The workshop-creation form opened with labeled title, start, end, and capacity fields, and its Create action stayed disabled until the form was valid. I did not create a workshop. Feedback and counts remained coherent through the exercised selection changes and refreshes.

These observations confirm the exercised UI flows, not true simultaneous browser requests. Concurrency and idempotency evidence comes from source and automated tests.

## Responsive design and accessibility

At **1440 px**, a workshop sidebar and detail/schedule column provide a clear scan path. At **768 px**, the narrower two-column layout still kept booking and roster controls reachable. At **390 px**, workshop cards stack above the detail and schedule panels; the page was about 2,022 px tall, so moving between a workshop card and its registration details takes scrolling. In all three cases, document width equaled viewport width, and no horizontal overflow was measured.

Workshop choices are actual buttons. The tested Tab transition reached the next workshop and showed a 3 px focus outline. Form fields had accessible labels; success and error messages use status/alert semantics. Status words supplement color. The principal accessibility weakness is scale: workshop metadata, queue guidance, badges, and schedules use roughly 9–12 px type, often in muted colors. The 12 px text-only Cancel button has only 4 px padding. These observations support the Universal Design deduction; they do not substitute for screen-reader or zoom testing. [Focus and text styles](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/style.css:15), [mobile layout](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/style.css:111)

## Build and automated tests

- .NET API project build: **passed**, 0 warnings and 0 errors.
- Backend test project: **32/32 passed**. The test run compiled both the API and test projects.
- React/TypeScript production build: **passed** using Node 22.
- No frontend test or Storybook script is supplied.

The solution-wide .NET build stalled without compiler diagnostics and was interrupted; I used the successful direct API build and complete test-project run as the build evidence. This tooling interruption carries no score penalty.

The tests cover more than happy paths: final-seat contention, simultaneous overlapping bookings, concurrent same-key retry, FIFO independent of insertion order, conflict skipping, touching time boundaries, repeated cancellation, re-registration, and notification failure after persistence. The three contention tests use independent contexts and connections to file-backed SQLite, a shared start gate, and fresh persisted-state assertions. They provide credible service-level contention evidence, though they do not prove that lock acquisition overlapped in every run or test concurrent HTTP calls. [Final-seat test](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Tests/RegistrationBehaviorTests.cs:343), [test database](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Tests/TestDatabase.cs:11), [HTTP tests](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Tests/ApiBehaviorTests.cs:27)

## Astra high-mode code review

**Backend strengths.** Registration and cancellation begin an immediate SQLite write transaction before reading keys, capacity, duplicates, or schedules. This serializes competing writers around the check-and-write decisions; cancellation and promotion commit together. A filtered unique index backs the active-duplicate rule, and persisted idempotency keys are stored atomically with registrations. Promotion follows creation time and ID, skips conflicting people without removing their entries, and allows workshops that touch at a time boundary. Notifications are sent after commit, with delivery errors contained. The required endpoints, validation, seed data, and README are present. [Registration service](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Api/Services/RegistrationService.cs:12), [transaction and notification boundary](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Api/Services/RegistrationService.cs:108), [database safeguards](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/backend/SeatFlow.Api/Data/SeatFlowDbContext.cs:14)

**Frontend strengths.** Selected roster and schedule requests abort obsolete responses and expose loading and failure states. Mutations refresh counts, roster, and schedule. Pending idempotency keys persist across uncertain network failures and reloads, allowing a true retry with the same key. Known successes and definitive client errors clear those keys. [Selection refresh](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/App.tsx:132), [retry-key lifecycle](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/api.ts:76)

**Confirmed source defect, not reproduced in the seed browser flow.** The frontend re-sorts registrations with `createdAt.localeCompare`, even though the API returns a chronological queue. Variable-length ISO fractional seconds do not sort chronologically as strings: `.11Z` sorts before the earlier `.1Z`. Queue positions can therefore be displayed incorrectly while backend promotion remains correct. The schedule applies the same string sort. [Waitlist sort](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/App.tsx:30), [schedule sort](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/App.tsx:267)

**Minor source defect.** The schedule's “View workshop” action changes selection without clearing an existing notice, so a previous workshop's error can remain visible under the newly selected workshop. This was not observed in the exercised browser path. [Schedule navigation](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/App.tsx:268)

**Unverified risk.** Initial catalog loads can overlap under React Strict Mode, and their state setters lack a cancellation or request-generation guard. An older response could overwrite fresher catalog counts after a later action. The roster and schedule loaders do have guards. This was not reproduced in the live UI. [Catalog loading](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/frontend/src/App.tsx:111)

The notification mechanism is best effort, without durable retry after a process crash; the README documents that limitation. The assignment asks for failure isolation after commit, which the implementation provides. [Documented limitations](/Users/oddbjornmidbo/Source/App-test/Sol-6.1/README.md:91)

## Placement against the earlier six

| Rank | Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1 | Luna-optimized | 8.8 | 9.2 | 8.5 | **26.5** |
| 2 | **Sol-6.1** | **9.0** | **9.2** | **8.2** | **26.4** |
| 3 | RUD-Opus | 8.5 | 9.2 | 8.3 | **26.0** |
| 4 | Luna | 6.8 | 8.5 | 8.0 | **23.3** |
| 5 | Opus | 8.5 | 9.1 | 5.4 | **23.0** |
| 6 | Sonnet | 7.6 | 8.8 | 5.2 | **21.6** |
| 7 | Sol | 6.5 | 8.6 | 6.4 | **21.5** |

The six earlier applications were not rerun for this one-app assessment. Their scores are copied from the [previous six-implementation evaluation](/Users/oddbjornmidbo/Source/App-test/SeatFlow-six-implementation-evaluation-Sol-2026-10-02.md). Sol-6.1's stronger backend and tests give it the highest Code score here; Luna-optimized retains a small overall lead through its design score. All implementations remain scored against the assignment rather than adjusted to force an order.
