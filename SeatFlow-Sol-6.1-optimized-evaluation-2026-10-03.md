# SeatFlow Sol-6.1-optimized evaluation

## Executive summary

Sol-6.1-optimized is the strongest SeatFlow implementation evaluated so far on this rubric. The live app handled available-seat confirmation, full-workshop waitlisting, proactive overlap prevention, cancellation, conflict-aware FIFO promotion, re-registration, schedule updates, and a phone-sized booking. Its backend protects the difficult rules with immediate SQLite write transactions and database constraints, and its tests provide unusually credible contention evidence. The interface is polished, responsive, and substantially more accessible than the earlier Sol-6.1 run.

The remaining issues are limited: a prior action's success notice can follow the user to another workshop, workshop creation can be retried after a lost response and create a duplicate, and an overnight workshop's end date is omitted from displayed time ranges. The first was reproduced in the browser; the other two are source-confirmed conditional cases.

**Score: 27.4/30.** This is ahead of Luna-optimized (26.5) and Sol-6.1 (26.4) in the consistent later comparison. The earlier applications were not rerun for this assessment.

## Scores

| Category | Score | Reason |
| --- | ---: | --- |
| Code | **9.2/10** | Strong transaction and database design, coherent frontend snapshots and retries, broad meaningful tests; a few narrow UI edge cases and dense JSX. |
| Requirements | **9.3/10** | Every core requirement is present and the exercised workflows worked; notice context and overnight date presentation need refinement. |
| Visual | **9.0/10** | Clear hierarchy, consistent cards and status styling, polished light interface; some secondary text remains small. |
| Responsive | **9.0/10** | Actions worked at all three sizes with no horizontal overflow; the phone page is long but has a return link to the catalog. |
| Universal Design | **8.7/10** | Semantic controls, skip link, visible focus, 44 px targets, live feedback, modal focus return, reduced-motion support; some compact metadata and no direct screen-reader/zoom verification. |
| **Design average** | **8.9/10** | Mean of Visual, Responsive, and Universal Design, rounded once. |
| **Total** | **27.4/30** | Code + Requirements + Design. |

## Method and evidence limits

I used the [original SeatFlow prompt](/Users/oddbjornmidbo/Source/App-test/prompt.md) and the same three-part rubric as the earlier evaluations. I evaluated [http://127.0.0.1:5177/](http://127.0.0.1:5177/) in the live browser before reading implementation source. The process serving port 5177 had its working directory in this `sol-6.1-optimized/frontend` checkout. The server was already running. Server availability and sandbox permissions carry no score penalty.

Browser actions changed local demo registrations. I measured layout at **1440 × 900**, **768 × 1024**, and **390 × 844** and interacted with workshop controls at each size. I checked keyboard focus and schedule-dialog Escape/focus return. I did not run a screen reader, browser zoom, the authored Playwright browser suite, or Storybook's interaction-test runner. Those test sources were inspected, but their self-reported historical pass counts are not fresh results here. An independent **Astra high-mode** agent reviewed the source and existing tests without reading previous scores or reports. I set the Sol-6.1-optimized scores before reading those reports for comparison.

## Browser findings

- **Conflict and available seat:** Selecting Lucas for the available Designing accessible interfaces workshop showed his conflict with Practical AI prototyping and disabled registration. Selecting Amara for Practical AI enabled registration; submission confirmed her, updated the capacity and roster, and disabled the duplicate action.
- **FIFO with conflict skipping:** The Practical AI waitlist initially showed Noah, Oliver, Maya, then Sofia. The first three had overlapping confirmed workshops. After Sofia's conflicting booking in Design systems that scale was cancelled, cancelling Lucas's confirmed Practical AI booking skipped the first three and promoted Sofia. Capacity remained 2/2, the waitlist fell from four to three, the success message named Sofia, and her schedule showed Practical AI confirmed.
- **Full workshop and re-registration:** After Lucas's cancellation, registering him again in full Practical AI added a new waitlist entry at position four. The cancelled history remained, and another active booking attempt was disabled. His later available-seat booking in Shipping resilient APIs succeeded at 390 px and remained present after a reload.
- **Navigation and discovery:** Search found the matching AI workshop, the “Waitlist only” filter showed the full workshop, and the Participants view listed six people with their confirmed/waitlisted counts. The workshop form opened with labeled fields. No unexpected browser console errors were captured in the exercised session.
- **Directly observed feedback issue:** After Lucas joined Practical AI's waitlist, I selected Shipping resilient APIs at tablet size. The green “Added to the waitlist” notice remained inside Shipping's registration panel, although it referred to the earlier Practical AI action and did not name that workshop. Selection leaves the global notice intact, while the narrow layout renders it inside the currently selected workshop. This can mislead users about which workshop was changed. [Selection and feedback placement](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/App.tsx:33), [workshop selection](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/App.tsx:68)

The seed fixture also exposed an open Practical AI seat with four waitlisted people, all initially conflicting. Cancelling Sofia's conflicting booking in another workshop made her eligible, but did not immediately rescan Practical AI. A later new registration took that open seat. This is **not a confirmed violation of the prompt**: its promotion trigger is cancellation of a confirmed registration in the affected workshop, and it directs new registrations with available capacity to be confirmed. The UI's broad promise that the next eligible person gets an available seat would benefit from stating that trigger precisely. [Specified promotion trigger](/Users/oddbjornmidbo/Source/App-test/prompt.md), [promotion implementation](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/RegistrationService.cs:73)

## Responsive design and Universal Design

At **1440 × 900**, a fixed sidebar, four overview cards, two-column workshop catalog, and detail panel give the page a clear hierarchy. At **768 × 1024**, the detail moves beneath the catalog; selecting a workshop brought the detail and booking controls into view. At **390 × 844**, cards stack and the registration, roster, and schedule actions remain reachable. Document width matched viewport width at every tested size (**1440, 768, 390 px**). The mobile page was about **3,265 px** tall in the tested state, but the detail provides a “Back to workshops” link.

The page has a skip link, navigation and region labels, pressed-state workshop buttons, labeled form fields, textual status badges, live feedback, and a visible 3 px keyboard focus outline. The tested schedule dialog closed with Escape and returned focus to its triggering participant button. Registration and cancellation controls have practical 44 px minimum targets; phone inputs use 16 px text. Reduced-motion styles and a dark theme are present in source, though the dark theme was not independently exercised in this browser pass. Some peripheral metadata remains around 10–12 px. [Focus and motion styles](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/styles.css:2), [target and type improvements](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/styles.css:25)

## Builds and tests

| Check | Fresh result |
| --- | --- |
| .NET 9 API build | **Passed**, 0 warnings, 0 errors |
| xUnit backend tests | **39/39 passed**, 0 skipped |
| React/TypeScript/Vite production build | **Passed** with Node 22 |
| Frontend unit tests | **16/16 passed** |
| Storybook static build | **Passed**; its third-party preview emitted `eval` and large-chunk warnings |

The shell's default .NET did not include SDK 9; the repo's documented local .NET 9 executable completed the build and tests. The first xUnit launch could not open the runner's local socket under the sandbox. With local test-host permission, the full suite passed. Neither condition reflects an application failure.

The backend suite is substantive: independent contexts and unpooled file-backed SQLite connections, dedicated worker threads, a barrier, and fresh persisted-state assertions test final-seat contention, same-key retries, duplicate races, overlapping workshops, and repeated cancellation. HTTP tests cover routes, statuses, response locations, and validation. Direct database tests exercise capacity, overlap, duplicate, and update constraints. Frontend unit tests cover retry keys, stale responses, mutation/refresh failure, and disabled actions. The repository also contains a real-backend Playwright suite and Storybook interaction stories, whose code was inspected but whose runners were not rerun here. [Race fixture](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Tests/DatabaseFixture.cs:94), [concurrency tests](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Tests/RegistrationServiceTests.cs:227), [frontend state tests](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.test.tsx:62), [browser suite](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/e2e/seatflow.spec.ts:44)

## Astra high-mode source review

**Backend correctness.** Registration and cancellation reserve SQLite's writer before reading capacity, conflicts, duplicates, or idempotency records. A filtered unique active-registration index and database triggers backstop the service's checks. Cancellation, the freed seat, and promotion commit atomically. The waitlist orders by creation time and ID, retains skipped conflicting people, and promotes one eligible person. Half-open intervals permit touching workshops. [Registration transaction](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/RegistrationService.cs:126), [database backstops](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Data/DatabaseInitializer.cs:69)

**Idempotency and state.** Keys, fingerprints, and original responses are persisted with registration. Same-key replay returns the original result without another notification; a different request using that key gets 409. The frontend retains uncertain keys across reloads, blocks rapid double submission, and refreshes all data into one coherent snapshot after a mutation. If refresh fails after a saved mutation, it explicitly marks data stale and blocks new mutations until recovery. Notifications occur after commit, and each delivery failure is contained. [Idempotency](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/RegistrationService.cs:24), [frontend refresh and retries](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.ts:47), [notification boundary](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/RegistrationService.cs:175)

**Source-confirmed conditional issue: lost workshop-creation response.** If the optional creation POST commits but its response is lost, the UI reports a definite failure and leaves the form ready for another submission. Creation has no idempotency key, so retrying can create a duplicate workshop. The README advises refreshing before retrying, but the UI does not. This was not reproduced live. [Creation feedback](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.ts:164), [documented limit](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/README.md:110)

**Source-confirmed display issue: overnight workshops.** Workshop input accepts any increasing start/end interval. Cards, details, and schedules show the start date followed only by start/end clock times; a workshop ending the next day would show an ambiguous “23:00 – 01:00” without the end date. This was not reproduced with a live overnight workshop. [Shared time formatter](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/components/WorkshopCard.tsx:5), [detail display](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/components/WorkshopDetail.tsx:47)

The implementation is proportionate to the assignment and its README documents running, architecture, assumptions, and limits. The main maintainability cost is compressed, near-single-line JSX in larger UI components. Notifications are development logs without durable retry; the prompt asks for postcommit failure isolation, which is implemented. No core domain-rule defect was confirmed. [Architecture and limits](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/README.md:70)

## Placement against prior evaluations

| Rank | Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1 | **Sol-6.1-optimized** | **9.2** | **9.3** | **8.9** | **27.4** |
| 2 | Luna-optimized | 8.8 | 9.2 | 8.5 | **26.5** |
| 3 | Sol-6.1 | 9.0 | 9.2 | 8.2 | **26.4** |
| 4 | RUD-Opus | 8.5 | 9.2 | 8.3 | **26.0** |
| 5 | Luna | 6.8 | 8.5 | 8.0 | **23.3** |
| 6 | Opus | 8.5 | 9.1 | 5.4 | **23.0** |
| 7 | Sonnet | 7.6 | 8.8 | 5.2 | **21.6** |
| 8 | Sol | 6.5 | 8.6 | 6.4 | **21.5** |

The prior scores come from the [six-implementation evaluation](/Users/oddbjornmidbo/Source/App-test/SeatFlow-six-implementation-evaluation-Sol-2026-10-02.md) and [Sol-6.1 evaluation](/Users/oddbjornmidbo/Source/App-test/SeatFlow-Sol-6.1-evaluation-2026-10-03.md). They are not fresh runs today. Scores assess each application against the assignment; they were not adjusted to force a ranking.
