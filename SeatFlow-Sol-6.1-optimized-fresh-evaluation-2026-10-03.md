# SeatFlow Sol-6.1-optimized: fresh reevaluation

## Result

**27.8/30.** The current Sol-6.1-optimized implementation satisfies the original SeatFlow assignment and remains the highest-scoring implementation in this comparison. The difficult booking rules worked in a new live-browser scenario, and the backend and frontend test suites passed. This assessment was scored from the current app and source before the earlier report was consulted. The earlier applications were not rerun.

| Category | Score | Evidence and judgment |
| --- | ---: | --- |
| Code | **9.2/10** | Atomic SQLite registration/cancellation flows, database backstops, persisted idempotency, recoverable frontend mutations, and substantive concurrency tests. The remaining concerns are narrow reliability and time-display edges. |
| Requirements | **9.6/10** | Every required route, business rule, UI workflow, seed requirement, build, test, and README element is present; the exercised booking flows worked. |
| Visual | **9.0/10** | Clear information hierarchy and status styling, with compact secondary text in places. |
| Responsive | **9.1/10** | All three checked widths worked without horizontal overflow; tablet and phone selection brings booking controls into view. |
| Universal Design | **8.9/10** | Semantic controls, textual statuses, labeled fields, live feedback, visible focus, keyboard-operable dialogs, focus return, reduced motion, and large primary targets. Some peripheral text remains small; no screen reader or zoom audit was run. |
| **Design average** | **9.0/10** | Mean of Visual, Responsive, and Universal Design. |
| **Total** | **27.8/30** | Code + Requirements + Design. |

## Method

I used the [original SeatFlow prompt](/Users/oddbjornmidbo/Source/App-test/prompt.md) and the same three-part rubric as the earlier evaluations. I checked the running app at [http://127.0.0.1:5177/](http://127.0.0.1:5177/) in a new browser session, measured **1440 × 900**, **768 × 1024**, and **390 × 844** viewports, exercised registration/cancellation/schedule flows, reviewed the current source, and ran fresh builds and tests. The process on port 5177 was confirmed to serve this checkout. Server availability carries no score penalty. Browser actions changed local demo registrations.

An independent **GPT-6 Astra, high-mode** agent reviewed the current source and tests without reading earlier reports or scores. It found no confirmed defect in the required booking rules and suggested Code **9.2/10**. I set all category scores before opening the earlier evaluation for comparison.

## Fresh browser findings

- **Available seat and touching schedules:** Lucas registered for Design systems that scale while already confirmed in Shipping resilient APIs. One ends at 13:00 and the other begins at 13:00, so the confirmation is valid. The new roster count and duplicate-disabled state appeared immediately.
- **Cancellation and feedback scope:** Cancelling that Design systems registration named the workshop and participant in the notice. Moving to another workshop cleared the notice. The same workshop-specific behavior held at desktop, tablet, and phone widths.
- **Conflict-aware FIFO promotion:** Practical AI prototyping had two confirmed participants and a four-person waitlist. Noah, Oliver, and Maya had conflicting confirmed workshops. Cancelling Amara's confirmed Practical AI seat promoted Lucas, the first eligible person, while the first three retained their queue order. The roster stayed **2/2**, and Lucas's schedule showed Practical AI followed by nonoverlapping Shipping resilient APIs.
- **Full-workshop retry path:** Re-registering Amara for the now-full Practical AI workshop placed her fourth on its waitlist, disabled another active registration, and named Practical AI in the feedback. A later available-seat registration for Amara in Shipping resilient APIs succeeded at phone width. The updated **10 confirmed / 4 waitlisted** totals persisted after reload.
- **Keyboard and console:** The workshop creation dialog opened with a labeled title field focused; Escape closed it and returned focus to **New workshop**. No browser console errors were captured in the exercised session.

The page had no horizontal overflow at any measured width (document width equaled viewport width at **1440**, **768**, and **390 px**). The desktop retains a catalog and detail panel; the tablet moves the detail below the catalog; the phone stacks cards and provides a **Back to workshops** link. The phone page is long (about **3,400 px** in the tested state), but the primary actions remained reachable. The app explicitly says promotion happens when a confirmed registration is cancelled, matching the assignment's trigger.

## Builds and tests

| Fresh check | Result |
| --- | --- |
| .NET 9 API build | **Passed**, 0 warnings, 0 errors |
| xUnit backend suite | **53/53 passed**, 0 skipped |
| React/TypeScript/Vite production build | **Passed** with Node 22 |
| Frontend unit suite | **31/31 passed** |
| Storybook static build | **Passed**; third-party preview tooling emitted `eval` and large-chunk warnings |

The backend tests cover independent-connection final-seat races, duplicate/overlap contention, FIFO promotion, idempotent registration, postcommit notification failures, API responses, and new workshop-creation replay/concurrency/rollback and schema-upgrade cases. The frontend tests cover uncertain creation recovery across reloads, blocked duplicate submissions, registration retries, stale refreshes, and date ranges including overnight and year boundaries. [Backend race fixture](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Tests/DatabaseFixture.cs:94), [creation tests](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Tests/WorkshopServiceTests.cs:14), [frontend recovery tests](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.test.tsx:182), [date tests](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/workshopDates.test.ts:11)

The authored Playwright browser suite and Storybook interaction runner were not rerun. Their existence was reviewed, but no fresh pass count is claimed for them. A screen reader, browser zoom, dark theme, and an induced lost-response creation failure were not exercised in the live browser.

## Source assessment and corrections

Registration and cancellation reserve SQLite's writer before reading capacity, conflicts, duplicates, or idempotency keys. A filtered unique index and database triggers backstop those checks. Cancellation, vacancy, and promotion commit together; candidates are ordered by creation time and ID, and conflicting candidates remain waitlisted. Touching time intervals are correctly nonoverlapping. Notifications are delivered after commit, with individual failures caught. [Registration service](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/RegistrationService.cs:22), [database safeguards](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Data/DatabaseInitializer.cs:69)

The frontend guards rapid double submissions, retains uncertain registration and creation attempts, refreshes a complete client snapshot after mutation, and blocks further mutations when a refresh fails. The following corrections directly address the earlier report's findings:

| Earlier finding | Current evidence | Status |
| --- | --- | --- |
| A success notice could appear under another workshop | Notices now carry a workshop ID, selection clears them, and live navigation at desktop/tablet/phone showed no misplaced notice. Success and cancellation text names the affected workshop. [App notice scope](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/App.tsx:33) | **Resolved in live checks** |
| Retrying workshop creation after a lost response could create a duplicate | Creation now persists a key and request fingerprint in the same transaction as the workshop, replays the original response, and rejects different input under the same key. The browser retains the original input/key across reloads and offers a safe retry. Backend and frontend recovery tests passed. [Creation transaction](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/backend/SeatFlow.Api/Services/WorkshopService.cs:14), [frontend retry](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.ts:200) | **Resolved by source and tests; not fault-injected live** |
| Overnight workshops omitted their end date | A shared formatter now shows both dates for multi-day ranges in cards, details, and schedules; seven date-formatting tests passed. Accessible workshop labels include both endpoints. [Date formatter](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/workshopDates.ts:38), [shared display](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/components/WorkshopTiming.tsx:10) | **Resolved by source and tests; not created live** |

**Remaining low-severity edges:** A refresh composes several HTTP reads, so an external writer acting between those reads can briefly produce a mixed-generation summary and roster until the next refresh. Requests have no timeout or abort path, so a permanently pending request can hold the global busy state. Date formatting omits per-endpoint UTC offsets during a repeated daylight-saving hour, leaving that rare local-time interval ambiguous. These are source-based possibilities, not reproduced assignment failures. [Snapshot refresh](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/useSeatFlow.ts:85), [request helper](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/api.ts:10), [time formatter](/Users/oddbjornmidbo/Source/App-test/sol-6.1-optimized/frontend/src/workshopDates.ts:14)

## Comparison to the earlier run

The [earlier Sol-6.1-optimized evaluation](/Users/oddbjornmidbo/Source/App-test/SeatFlow-Sol-6.1-optimized-evaluation-2026-10-03.md) scored **27.4/30** (Code 9.2, Requirements 9.3, Design 8.9). This fresh assessment scores **27.8/30** (Code 9.2, Requirements 9.6, Design 9.0). The higher Requirements score reflects the corrected workshop notice, creation retry, and multi-day date behavior; the small Design increase reflects clearer feedback context and accessible time ranges. The comparison is for this updated app only; other applications were not rerun.
