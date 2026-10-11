# SeatFlow: Haiku 5.5 RUD and Luna 6 RUD evaluation

## Result

Both implementations cover the main SeatFlow workflow, build successfully, and reflow to a 390 px phone without horizontal page overflow. **Haiku 5.5 RUD scores 23.3/30; Luna 6 RUD scores 22.8/30.** Haiku's stronger test coverage and safer registration history give it a narrow overall lead. Luna has an atomic cancellation/promotion transaction, but a live retry sequence showed that it reuses a cancelled registration's ID, allowing an old cancellation request to cancel a new booking.

| Implementation | Code | Requirements | Visual | Responsive | Universal Design | Design¹ | Total | Build time² | Cost² |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| **Haiku 5.5 RUD** | 7.4 | 8.3 | 6.3 | 8.5 | 8.1 | **7.6** | **23.3/30** | 15m 43s | $0.38 |
| **Luna 6 RUD** | 7.2 | 8.0 | 6.6 | 8.8 | 7.5 | **7.6** | **22.8/30** | 9m 34s | <$0.20 |

¹ Design is the arithmetic mean of Visual, Responsive, and Universal Design, rounded to one decimal. The total is Code + Requirements + rounded Design. ² The generation time and cost are user-reported, not measured in this evaluation. The user also reports Luna's run consumed significantly less than 1% of the weekly GPT Pro quota.

## Scope and method

These are **two separate new generation runs**, both made with [prompt-rud.md](/Users/oddbjornmidbo/Source/App-test/prompt-rud.md). A direct diff against the [original prompt](/Users/oddbjornmidbo/Source/App-test/prompt.md) shows exactly one changed line: “The UI does not need to be beautiful, but it must work” was replaced with a requirement for responsiveness, keyboard usability, labelled controls, readable contrast, and mobile reflow. The underlying business, API, technology, and test requirements are the same. Scores use the previous **Code + Requirements + Design** rubric. Since the UI instruction changed, these scores should not be treated as a controlled model-only comparison with original-prompt runs.

I started each stopped app without any availability penalty, used isolated disposable SQLite databases, reviewed source and tests, ran fresh backend/frontend builds and backend test suites, and exercised the APIs and browsers. An independent **GPT-6 Astra high-mode** review inspected both codebases. Browser checks used **1440 × 900 desktop**, **768 × 1024 tablet**, and **390 × 844 phone** viewports. The live API checks included coordinated final-seat requests, key replay, repeat cancellation, and conflict-aware waitlist promotion. They do not prove correctness across multiple API processes or every possible failure path.

## Haiku 5.5 RUD

### What worked

- A live capacity-one race produced one **Confirmed** and one **Waitlisted** registration; the roster contained exactly one of each. Same-key replay returned the same registration ID and creation timestamp without a duplicate; changing participant or workshop with that key returned `409`.
- Cancelling the confirmed registration promoted the oldest eligible person. Repeating the cancellation returned success without another promotion. In a separate FIFO scenario, the first waitlisted person had gained a conflicting confirmed workshop; the app kept them waitlisted and promoted the next eligible person. Validation produced field-level `400`s; duplicates and confirmed overlaps gave `409`; missing IDs gave `404`.
- The browser showed confirmation, waitlisting, cancellation, promotion, and schedules. The selected roster and schedule used written status labels alongside color. Descriptive action names, form labels, a skip link, visible keyboard focus, and a live status announcement support basic keyboard use. Pressing Enter on a focused workshop action worked.
- At 390 px the workshop table becomes labelled blocks and the roster actions remain visible. The document did not overflow horizontally at phone or tablet widths.

### Findings behind the score

1. **A selected workshop can appear empty while its registrations still exist.** Clicking its already selected **“Viewing”** button clears the detail roster, but the selected workshop ID does not change, so the data-fetch effect does not run again. I reproduced this in the browser: the overview still showed two confirmed and one waitlisted registration while the detail showed zero of each. Selecting another workshop and then returning restored the roster. This affects the immediate booking view. [Selection handler](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/frontend/src/App.tsx:164), [fetch effect](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/frontend/src/App.tsx:57)
2. **Cancellation and promotion are not one database transaction.** The cancellation is saved before the waitlist promotion is separately saved. A database error or interruption between those writes could leave an eligible waitlisted person stranded; retrying that already cancelled registration exits without promotion. This is a source-review risk, not a failure reproduced in the live session. [Cancellation and promotion](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/backend/SeatFlow.Api/Services/RegistrationService.cs:151)
3. **A full workshop rejects some otherwise possible waitlist entries.** A participant with an overlapping confirmed workshop received `409` rather than joining the full workshop's waitlist. The prompt prohibits *overlapping Confirmed* seats; it does not explicitly require accepting such a waitlist request. Haiku documents this stricter assumption. [Conflict check before capacity decision](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/backend/SeatFlow.Api/Services/RegistrationService.cs:127), [README](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/README.md)

The in-process write lock protected the observed one-process final-seat race. Its guarantee does not extend to independent API processes sharing the same SQLite file. Database indexes independently enforce active duplicates and unique idempotency keys. Selection fetches can race, and errors during post-mutation refresh are discarded. The UI is legible and functional but visually plain, with a dense tablet table and a verbose phone table layout. [Service lock](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/backend/SeatFlow.Api/Services/RegistrationService.cs:26), [database indexes](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/backend/SeatFlow.Api/Data/SeatFlowDbContext.cs:34), [refresh handling](/Users/oddbjornmidbo/Source/App-test/Haiku5.5-RUD/frontend/src/App.tsx:71)

## Luna 6 RUD

### What worked

- A live final-seat race returned one **Confirmed** and one **Waitlisted** registration, with zero seats remaining. A same-key request replayed the same registration and timestamp; key reuse for a different participant returned `409`. Repeated cancellation of an unchanged cancelled registration returned `200` both times.
- In the browser, a full workshop waitlisted Morgan. Morgan could also take a Confirmed seat in an overlapping workshop; when the original seat opened, promotion skipped Morgan and confirmed the next eligible person. A participant who already held a conflicting Confirmed seat was rejected from another available workshop. Roster counts and statuses updated after each action.
- Desktop cards are orderly; at tablet width they wrap, and at phone width they form a single column. Forms and schedule controls also stack at phone width. No horizontal document overflow was observed. Workshop cards are keyboard-operable buttons with pressed state, and the forms have visible labels and focus styles.

### Findings behind the score

1. **Old requests can change a later booking.** Luna reuses a cancelled registration row when that participant joins the same workshop again, resetting its creation timestamp and status while retaining its numeric ID. In a live sequence, a delayed retry of the *old* cancellation then cancelled the *new* Confirmed booking. Replaying the original `Idempotency-Key` also returned the recycled registration's new state and timestamp, rather than the original request's result. This breaks reliable retry and historical registration identity. It was reproduced through the HTTP API and follows directly from the row-reuse code. [Re-registration and old-key replay](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/backend/SeatFlow.Api/RegistrationService.cs:43), [cancellation lookup](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/backend/SeatFlow.Api/RegistrationService.cs:87)
2. **An opened schedule becomes stale after a change.** The browser showed Sam as **Cancelled** in the roster immediately after cancellation, while the already opened participant schedule still said **Confirmed**. Clicking **View schedule** refreshed it. Registration and cancellation reload workshops and roster but do not reload the displayed schedule. [Mutation refreshes](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/frontend/src/main.tsx:39), [captured contradiction](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/luna6-rud-desktop-stale-schedule.png)
3. **Accessibility is basic but uneven.** Statuses use text and high-contrast colors, forms have labels, errors use alerts, and card selection is keyboard accessible. Phone **Cancel** controls are about 75 × 32 px, and status badge text is small. There is no skip link or focus move after a mutation. A browser keyboard pass succeeded; a screen-reader or zoom audit was not performed. [UI components](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/frontend/src/main.tsx:62), [styling](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/frontend/src/style.css)

The backend does have a transaction around registration and idempotency writes, and another around cancellation plus promotion; that is a genuine strength over Haiku's split cancellation save. The one-process mutation gate still limits capacity safety across independent instances. The 11 tests cover the nine minimum scenarios and notification-failure containment, but have less HTTP and lifecycle coverage than Haiku's suite. The contention test shares one SQLite connection and has no request-start barrier. [Registration service](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/backend/SeatFlow.Api/RegistrationService.cs:27), [tests](/Users/oddbjornmidbo/Source/App-test/Luna6-RUD/backend/SeatFlow.Tests/RegistrationTests.cs:32)

## Build and test evidence

| Check | Haiku 5.5 RUD | Luna 6 RUD |
| --- | --- | --- |
| Backend target | .NET 9 | .NET 9 |
| Backend build | Passed, 0 warnings/errors | Passed, 0 warnings/errors |
| xUnit suite | **30/30 passed** | **11/11 passed** |
| React/TypeScript/Vite production build | Passed | Passed |
| Frontend automated tests | None supplied | None supplied |

The first build/test attempts encountered the host's older default SDK/Node and a sandbox restriction on the test runner's local socket. Re-running with the installed .NET 9/Node 22 tools and a permitted runner completed successfully; **no score penalty** was applied for those environment issues. Test counts show the supplied suites passed; they do not erase the bugs reproduced during this evaluation.

## Screenshots

The existing [screenshot folder](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08) now contains **19 new PNGs** for these two runs: overview, waitlist, and schedule for each app at desktop, tablet, and phone sizes, plus Luna's stale-schedule evidence. Filenames identify the model, the **RUD prompt**, device, and function. The folder name predates these images; they were captured during this **9 October 2026** evaluation before shutting down the test servers.

- [Haiku phone overview](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/haiku5.5-rud-phone-overview.png), [waitlist](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/haiku5.5-rud-phone-waitlist.png), [schedule](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/haiku5.5-rud-phone-schedule.png)
- [Luna phone overview](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/luna6-rud-phone-overview.png), [waitlist](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/luna6-rud-phone-waitlist.png), [schedule](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/luna6-rud-phone-schedule.png)

The two new apps' test servers were stopped after the capture and verification. No other project's local server was intentionally stopped.
