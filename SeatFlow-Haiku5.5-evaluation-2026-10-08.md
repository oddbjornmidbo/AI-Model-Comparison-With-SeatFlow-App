# SeatFlow Haiku 5.5 evaluation

## Result

**20.9/30.** Haiku 5.5 produced a working SeatFlow application with particularly good coverage of the required backend behavior for a reported **9m 53s** and **$0.41** implementation run. The live app handled confirmation, waitlisting, duplicate and overlap rejection, conflict-aware FIFO promotion, cancellation, and schedule display. Both builds and all backend tests passed. Its main weakness is the phone interface: a **390 px** viewport produced a roughly **640 px** page, placing columns and actions offscreen.

| Category | Score | Reason |
| --- | ---: | --- |
| Code | **7.6/10** | Clear service/API split and 26 substantive backend tests. A process-only concurrency lock, non-atomic cancellation/promotion saves, and unguarded asynchronous frontend reads limit robustness. There are no frontend automated tests. |
| Requirements | **9.0/10** | All required business rules, routes, seed data, notifications, UI workflows, builds, tests, and README sections are present. Exercised workflows worked. The single-process capacity guarantee and cancellation failure window qualify the result. |
| Visual | **5.6/10** | Clean, legible desktop table and clear status colors, but a sparse presentation with default-looking controls and limited hierarchy. |
| Responsive | **2.5/10** | Tablet has dense wrapping; phone has substantial horizontal overflow and hides columns and actions to the right. |
| Universal Design | **4.8/10** | Native controls, associated participant labels, table headers, status text, and an error alert help. Repeated generic “Select”/“Cancel” names, small touch targets, no success announcement, and the phone overflow impede use. |
| **Design average** | **4.3/10** | Mean of Visual, Responsive, and Universal Design. |
| **Total** | **20.9/30** | Code + Requirements + Design. |

## Scope and method

The assessment uses the [original SeatFlow assignment](/Users/oddbjornmidbo/Source/App-test/prompt.md) and the existing **Code + Requirements + Design** rubric. The app was evaluated at [http://localhost:5273/](http://localhost:5273/) using the supplied `Haiku5.5` checkout and its existing SQLite data. Its API and frontend were stopped at the start, so I started both and assigned **no penalty** for server availability. I checked the interface at the default desktop size, **768 × 1024**, and **390 × 844**, exercised the live booking workflow, reviewed the source and tests, and ran fresh builds and the backend suite. Browser actions changed local demo registrations; the resulting state persisted after an API restart and page reload.

The **9m 53s** generation time and **$0.41** cost are user-reported. This evaluation was run separately; the earlier applications were not rerun. Unlike some earlier assessments, this source review did not use a separate Astra agent, which should be considered when comparing the code scores.

## Live behavior

- **Full workshop and FIFO promotion:** Testing Asynchronous Code was full at **1/1**. Erik joined its waitlist first while confirmed in an overlapping Event Sourcing workshop; Alice joined second. Cancelling Bjørn's confirmed seat kept Erik waitlisted and promoted Alice. The workshop remained **1/1**, the waitlist fell from two to one, and Alice's participant schedule showed the confirmed workshop.
- **Validation:** Trying to register already-confirmed Alice again produced a visible duplicate-registration error. Trying to register her in the available Event Sourcing seat produced an overlap error. Registering Bjørn for that seat then succeeded and updated Event Sourcing to **2/2**.
- **State and feedback:** The roster distinguishes Confirmed, Waitlisted, and Cancelled, and waitlist positions were shown in order. Success messages identify the participant and workshop, while errors appear in an alert. Cancellation feedback names the cancelled registration but does not identify who was promoted. The changed totals survived reload. No browser console errors were captured in the exercised session.

These browser checks support the required booking behavior. Idempotent HTTP replay, repeated cancellation, notification failure isolation, and final-seat contention were verified through the automated backend tests rather than a second live fault-injection session.

## Layout and accessibility

At desktop width, the single-column workspace is understandable and its status badges use text as well as color. At **768 px**, the workshop table used the available **736 px** content width and wrapped heavily, but the page itself did not overflow. At **390 px**, the document measured about **640 px** wide, with a **625 px** workshop table; only its first columns appeared in the initial phone viewport, while the remaining counts and **Select** buttons required horizontal scrolling. The selected workshop's registration table measured about **542 px** wide. The stylesheet has no width-specific responsive rules or table overflow container. [Table and layout CSS](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/styles.css:1)

The UI uses semantic tables, native buttons and selects, associated labels, status text, and `role="alert"` for errors. However, every workshop action is simply announced as **“Select”**, every roster action as **“Cancel”**, success feedback has no live-region role, and the controls are small for touch: a phone-width **Select** button measured about **53 × 22 px**. No screen reader or zoom audit was run. [Workshop actions](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:185), [roster actions](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:22), [feedback](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:172)

## Builds and tests

| Fresh check | Result |
| --- | --- |
| .NET 9 solution build | **Passed**, 0 warnings, 0 errors |
| xUnit backend suite | **26/26 passed**, 0 skipped |
| React/TypeScript/Vite production build | **Passed** with Node 22 |
| Frontend automated tests | **None supplied** |

The backend tests use file-backed SQLite and cover all nine minimum scenarios in the assignment, plus adjacent cases such as back-to-back workshops, re-registration after cancellation, notification failure, API error statuses, and key replay. The ten-contender final-seat test executes against one `SeatFlowService` and therefore verifies the process lock, not independent API processes. [Service tests](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/tests/SeatFlow.Tests/SeatFlowServiceTests.cs:10), [HTTP tests](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/tests/SeatFlow.Tests/ApiTests.cs:10), [test database](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/tests/SeatFlow.Tests/TestDatabase.cs:10)

## Source review

**What works well:** The business rules reside in `SeatFlowService`, not in the endpoints. Its waitlist is ordered by creation timestamp and ID, skips conflicting participants without removing them, and uses a half-open overlap check so adjoining workshops are allowed. Registration keys are stored with a unique database index; same-key replay returns the existing registration and sends no second notification. Notification exceptions are caught after a successful write. The README accurately describes the architecture and its principal limits. [Registration and promotion](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Core/SeatFlowService.cs:98), [notification boundary](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Core/SeatFlowService.cs:294), [README](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/README.md)

**Important limits and risks:**

1. Capacity and conflict checks are protected by a static `SemaphoreSlim` in one API process. Two API processes using the same SQLite file would not share it, and the database has no confirmed-seat capacity or active-duplicate backstop. The README discloses this single-process limit. [Service lock](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Core/SeatFlowService.cs:20), [database model](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Core/SeatFlowDbContext.cs:20)
2. Cancellation and subsequent promotion are saved in **separate database commits**. An error or interruption after the cancellation save can leave a free seat while an eligible person remains waitlisted. This failure path was not reproduced live. [Cancellation saves](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Core/SeatFlowService.cs:200)
3. Workshop and schedule fetches write to React state without cancellation or request-generation checks. Switching selections during a slow response can place the prior roster under the new workshop heading, or display the prior participant's schedule. An in-flight mutation can similarly refresh the previous selection. [Frontend fetch effects](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:76), [refresh after mutation](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:125)
4. The browser retains only one uncertain registration key in memory. Reloading, or starting a different attempt before resolving a lost response, discards the original key and weakens its safe-retry behavior. [Retry state](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:73)
5. Workshop creation responds with a `Location` for `/api/workshops/{id}`, but no GET route exists at that URL. Overnight workshops are accepted, while their UI range shows the start date and only the end clock time. [API routes](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/backend/src/SeatFlow.Api/Program.cs:42), [date formatting](/Users/oddbjornmidbo/Source/App-test/Haiku5.5/frontend/src/App.tsx:5)

The first two are backend robustness gaps outside the exercised single-process happy path. The frontend state risks and date display are conditional source findings. The phone overflow is directly observed.

## Placement

Using the same rubric and the currently recorded historical scores, Haiku's **20.9/30** falls between Sonnet 5.5 (**21.6**) and the original Sol (**20.6**). It is substantially cheaper than Sonnet's reported roughly **$0.80**, while taking longer (**9m 53s** versus **5m 07s**). Those earlier applications were not retested on 8 October. [Comparison record](/Users/oddbjornmidbo/Source/App-test/readme.md:32)
