# SeatFlow six-implementation evaluation

## Executive summary

All six implementations loaded at their listed URLs and completed the requested frontend production build and backend test run. The leading pair is close: Luna-optimized scores 26.6/30 and RUD-Opus 26.5/30. Luna-optimized has the strongest overall engineering evidence; RUD-Opus has the best balance of responsive and inclusive usability.

The most important directly observed defect was Luna's participant schedule remaining stale after a registration until the participant was reselected. Source review confirmed that its schedule fetch is not triggered by register or cancel. Opus and Sonnet also have frontend selection races; Sol and RUD-Opus can hide refresh failures after a successful mutation. No confirmed backend overbooking or overlap-prevention defect was found.

## Overall scores

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| --- | ---: | ---: | ---: | ---: |
| Luna-optimized | 8.7 | 9.4 | 8.5 | 26.6 |
| Luna | 6.7 | 8.2 | 8.0 | 22.9 |
| Opus | 8.4 | 9.4 | 5.7 | 23.5 |
| Sonnet | 7.5 | 9.0 | 5.6 | 22.1 |
| RUD-Opus | 8.5 | 9.4 | 8.6 | 26.5 |
| Sol | 6.3 | 8.3 | 6.4 | 21.0 |

## Design scores

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| --- | ---: | ---: | ---: | ---: |
| Luna-optimized | 9.0 | 8.6 | 8.0 | 8.5 |
| Luna | 8.4 | 8.2 | 7.5 | 8.0 |
| Opus | 6.5 | 5.5 | 5.2 | 5.7 |
| Sonnet | 6.4 | 5.3 | 5.0 | 5.6 |
| RUD-Opus | 8.7 | 8.7 | 8.4 | 8.6 |
| Sol | 6.8 | 5.8 | 6.5 | 6.4 |

Design is the direct arithmetic mean of Visual, Responsive, and Universal Design, rounded to one decimal.

## Method and evidence notes

- I evaluated the live applications first at their exact listed URLs: [Luna-optimized](http://localhost:5173/), [Luna](http://localhost:8201/), [Opus](http://localhost:8202/), [Sonnet](http://localhost:8203/), [RUD-Opus](http://localhost:8204/), and [Sol](http://localhost:8205/). Each URL loaded and its title/workshop content matched the expected implementation. Luna-optimized was available at port 5173 during this evaluation; an earlier availability issue is superseded.
- Browser interaction and design were checked at 1440×900, 768×1024, and 390×844. Available-seat registration, full-seat waitlisting, duplicate handling, cancellation, conflict-aware promotion, participant schedule, and updates after actions were exercised where the current app data allowed. Current registrations differed across apps; I adapted participants and workshops rather than treating different seed states as defects.
- Keyboard Tab navigation and visible focus were checked. Screen-reader output and browser zoom were not directly tested. Accessibility findings about announcements and semantics are based on the DOM/accessibility tree, not a screen-reader session. Contrast was assessed visually, not with a contrast meter.
- Builds and tests ran after browser/design evaluation. All six frontend production builds succeeded. Backend API and test projects built under .NET 9; all six test suites passed, totaling 115 tests. Test runs used no-restore mode; no dependencies were updated. The initial sandbox denied the .NET test host's local communication socket, so the requested suites were rerun with local test-host socket access. The suites then passed.
- A solution-level build for Luna-optimized stalled during restore without diagnostic output. Its API and test projects built individually and its complete test suite passed. Luna and Sol builds reported NU1900 warnings because the NuGet vulnerability-data endpoint could not be reached; those warnings did not prevent project builds or tests.
- Luna-optimized and RUD-Opus Storybook production builds succeeded. Luna-optimized's Storybook build reported a dependency-runtime eval warning and a large-chunk warning; RUD-Opus reported a large-chunk warning. Storybook interaction stories were inspected as source but were not run as tests.
- GPT-6 Astra reviewed implementation source and tests only after browser/build/test evaluation. It received the common requirements, runtime observations, and build/test results, but no prior evaluations or scores. I used its findings as evidence and checked key findings against source; the final scores are mine.
- No implementation source files were edited. Browser workflows changed local registration data as required for evaluation.

# Luna-optimized

## Browser findings

At [port 5173](http://localhost:5173/), the workshop registration and detail panels worked through an available registration, duplicate attempt, conflict rejection, full-workshop waitlisting, cancellation, and promotion. The registration count, status, and schedule reflected successful actions. When Taylor's confirmed seat was cancelled, three waitlisted candidates with overlapping confirmed bookings were skipped and eligible Jamie was promoted. Duplicate and conflict feedback were clear.

## Responsive Design

At 1440×900 the two-panel dark interface was polished and information-dense. At 768×1024 it remained two columns without horizontal overflow. At 390×844 it stacked cleanly, with a page width of 390 px and no horizontal scroll; the mobile flow was long vertically.

## Universal Design

Workshop cards and form controls had useful labels and semantic actions. Tab focus was visible with a clear 3 px outline. Statuses used text as well as color. Small, muted secondary labels reduce readability, keeping the score below the top range. Screen-reader behavior was inferred, not directly tested.

## Build and automated tests

- Backend API and test projects built; the full solution build stalled silently during restore.
- Backend tests: 16 passed, 0 failed.
- Frontend production build: passed without warnings.
- Storybook build: passed; dependency-runtime eval and large-chunk warnings.

## Astra code-review findings

Astra recommended Code 8.7. The strongest backend evidence is an immediate SQLite transaction before capacity, duplicate, overlap, and idempotency checks; durable key mapping committed with registration; and a persisted queue position that makes FIFO deterministic. Database constraints backstop active-registration uniqueness, queue order, positive capacity, time ranges, and idempotency keys. Notification errors are isolated after commit. See [RegistrationService.cs:59](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/backend/SeatFlow.Api/RegistrationService.cs:59) and [SeatFlowDbContext.cs:14](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/backend/SeatFlow.Api/SeatFlowDbContext.cs:14).

Its concurrency tests use a temporary file database, fresh contexts, coordinated callers, and persisted-state assertions. The remaining gaps are narrower cancellation/notification failure cases and incomplete HTTP coverage. The UI has request sequence guards and distinguishes a successful write from a failed refresh. A rare retry-key lifecycle risk remains if a response is lost, the registration is cancelled, and the same participant registers again before the pending key is cleared.

## Important defects and risks

- Likely edge-case risk: the frontend can retain an idempotency key after an uncertain success, then replay a cancelled registration if the user cancels and retries the same participant/workshop. This was not reproduced in the browser.
- Test gap: double-cancel is not tested with waitlisted participants, so the test does not directly establish that a repeated cancellation cannot promote twice.
- The concurrency notification recorder uses a plain list; that is a test robustness concern, not evidence of a seat-allocation defect.

## Strengths

Reliable conflict-aware promotion was directly observed. The API/source and concurrency tests provide the strongest combined evidence for correct SQLite seat allocation, idempotency, FIFO tie-breaking, and overlap handling. The interface also gave the clearest runtime feedback of the six.

## Scores

Code 8.7; Requirements 9.4; Visual 9.0; Responsive 8.6; Universal Design 8.0; Design 8.5; Total 26.6.

# Luna

## Browser findings

At [port 8201](http://localhost:8201/), available registration updated the roster and capacity; a duplicate attempt did not add a row but left Register enabled and showed an error. A full-workshop registration was waitlisted. On cancellation, a later waiter was promoted while the earlier waiter remained. Since I had not established that the earlier waiter was conflict-free at that moment, this remains an unverified ordering anomaly rather than a confirmed FIFO defect. One schedule snapshot initially showed no registrations; changing away and back refreshed it with the correct confirmed workshops.

## Responsive Design

The desktop layout used a clean workshop list, registration area, and schedule. At tablet size its panels remained side by side without horizontal overflow. At 390 px it stacked the list and detail area without horizontal scroll, but required substantial vertical scrolling.

## Universal Design

Workshop cards and controls were usable with the keyboard and showed visible default focus. Labels and status text were present. Small, low-contrast secondary text reduced readability; duplicate registration was not disabled before submission.

## Build and automated tests

- Backend API build succeeded with two NU1900 warnings from the unreachable NuGet vulnerability-data endpoint.
- Backend tests: 12 passed, 0 failed.
- Frontend production build: passed without warnings.

## Astra code-review findings

Astra recommended Code 6.7. The service has capacity, duplicate, idempotency, overlap, FIFO, cancellation, and notification-failure handling. FIFO source sorts by creation timestamp then integer ID; UTC ticks are stored for SQLite ordering. The observed waiter ordering is not confirmed as a defect because eligibility was not established.

The confirmed UI defect is that registration and cancellation refresh the roster and workshop count, but not the currently displayed participant schedule. The schedule fetch runs only when the participant selection changes. This matches the stale schedule seen during browser use. [App.tsx:64](/Users/oddbjornmidbo/Source/App-test/Luna/frontend/src/App.tsx:64).

The source also has unguarded asynchronous responses across workshop/participant changes. A slow response for a previous selection can replace the latest selection's results. Its concurrency tests use one service and shared in-memory SQLite connection, without a coordinated start or persisted final-capacity assertion; that limits confidence but does not prove production overbooking. The API returns a registration Location for a GET route that is not registered.

## Important defects and risks

- Confirmed runtime and source defect: schedule does not refresh after register/cancel; it can remain stale until reselection or reload.
- Likely frontend risk: old requests can overwrite data for a newer selection.
- Test evidence for cross-connection/process concurrency is weak. Source uses a process-wide gate and serializable transaction; no production seat-allocation defect was established.
- Retry attempts generate a new idempotency key, so a lost response cannot be recovered with the original key.
- Missing start time can be accepted as a default value when the end time is later.

## Strengths

The backend business rules are implemented in a reasonably simple service. It has durable idempotency records, mismatch rejection, repeat-cancel handling, strict overlap logic, and caught notification failures. The README documents setup and known limitations.

## Scores

Code 6.7; Requirements 8.2; Visual 8.4; Responsive 8.2; Universal Design 7.5; Design 8.0; Total 22.9.

# Opus

## Browser findings

At [port 8202](http://localhost:8202/), the available-seat registration succeeded and the duplicate was blocked. A full workshop waitlisted users; cancellation left the seat free when all current waiters had overlapping bookings. After cancelling one of a participant's conflicting bookings, the app promoted that participant into the available seat and updated their schedule. A schedule view briefly showed a stale participant snapshot after switching selections, then recovered.

## Responsive Design

The desktop table and detail panels were plain but readable. At 768 px the layout stacked without horizontal overflow. At 390 px the table extended to 462 px, clipping columns and requiring horizontal scrolling; selecting a workshop was difficult on a narrow screen.

## Universal Design

Native selects and buttons had labels and visible focus. Workshop choice was represented by clickable table rows with tabIndex -1 and no nested keyboard control, so the primary selection workflow was not reachable with Tab. The table's semantics help reading, but do not replace keyboard operation. Small mobile text and the overflow further reduce inclusive usability.

## Build and automated tests

- Backend API build: passed with 0 warnings.
- Backend tests: 34 passed, 0 failed.
- Frontend production build: passed without warnings.

## Astra code-review findings

Astra recommended Code 8.4. The backend uses immediate SQLite transactions before rule checks, a filtered unique active-registration constraint, global idempotency keys, and caught post-commit notification errors. Cancelling a conflict also rechecks the participant's other waitlists. Service logic is separated from HTTP endpoints. See [RegistrationService.cs:85](/Users/oddbjornmidbo/Source/App-test/Opus/backend/src/SeatFlow.Api/Services/RegistrationService.cs:85).

Its concurrency tests were the strongest broad suite: file-backed SQLite, independent contexts/connections, coordinated starts, and persisted assertions for final-seat, same-key, duplicate, and concurrent cancellation cases. The main engineering weakness is the frontend's unguarded refresh flow: results captured for an earlier workshop/participant can overwrite the newest selection. [App.tsx:35](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:35).

## Important defects and risks

- Confirmed source defect: workshop selection is not keyboard reachable.
- Confirmed source risk: cross-selection responses can make the roster or schedule stale.
- Workshop creation returns a Location without a corresponding GET-by-ID endpoint. The form also clears its title after failed creation.
- Each registration attempt creates a new key; the UI does not preserve the key for recovery after an uncertain response.
- Refresh reads depend on a development notification endpoint; failure there can prevent workshop, roster, and schedule reads.

## Strengths

The broadest and most credible automated test evidence was in this implementation. Domain behavior and HTTP validation coverage are strong, and its backend separation is clear without excessive abstraction. Runtime conflict skipping and subsequent promotion behaved correctly.

## Scores

Code 8.4; Requirements 9.4; Visual 6.5; Responsive 5.5; Universal Design 5.2; Design 5.7; Total 23.5.

# Sonnet

## Browser findings

At [port 8203](http://localhost:8203/), registration into an available workshop succeeded. A duplicate did not create another record, but gave little visible feedback. A cancelled participant was added to a full workshop behind the existing waiter; when their conflicting booking was cancelled, they were promoted while the conflicting waiter remained. The schedule briefly showed an empty snapshot before updating after another selection.

## Responsive Design

The stacked desktop/tablet cards fit at 1440 and 768 px. At 390 px, the workshop table reached 476 px wide, clipping time and other columns. Mobile selects and buttons were small, and the table needed horizontal scrolling.

## Universal Design

Text and status colors were generally readable, but workshop selection depended on clicking table rows that were not keyboard-focusable. Native action controls did show visible focus. Positive mutation feedback was absent and duplicate outcomes were unclear.

## Build and automated tests

- Backend API build: passed with 0 warnings.
- Backend tests: 26 passed, 0 failed.
- Frontend production build: passed without warnings.

## Astra code-review findings

Astra recommended Code 7.5. The transactional service implements persisted idempotency, duplicate prevention, conflict-aware FIFO promotion, repeated-cancellation handling, and half-open overlap boundaries. Notifications are sent outside the transaction and process gate, with failures caught. Service and endpoint responsibilities are separated.

Frontend refresh requests have no identity guard, so switching workshops or participants during a request can display stale data. The API returns creation Location URLs without corresponding GET routes. [App.tsx:41](/Users/oddbjornmidbo/Source/App-test/sonnet/frontend/src/App.tsx:41) and [ApiEndpoints.cs:32](/Users/oddbjornmidbo/Source/App-test/sonnet/src/SeatFlow.Api/Endpoints/ApiEndpoints.cs:32).

Tests use a temporary file database and fresh contexts, with broad HTTP pipeline coverage. Their concurrent callers lack a synchronized start barrier and the process semaphore serializes them, so the suite is less persuasive about cross-process locking than Opus or Luna-optimized. This is a confidence limitation, not proof of a concurrency bug.

## Important defects and risks

- Confirmed source concern: workshop selection is a mouse-only table-row interaction.
- Likely state risk: unguarded requests may overwrite a newer selection's results.
- Retry attempts use new idempotency keys, no positive success feedback is provided, and an earlier refresh error can remain after a later success.
- No material backend business-rule omission was confirmed.

## Strengths

The backend service and HTTP tests cover required operations, input validation, idempotency replay, waitlist promotion, and overlap behavior. The 26 passing tests provide solid broad behavior evidence even though their concurrency scheduling is less strong.

## Scores

Code 7.5; Requirements 9.0; Visual 6.4; Responsive 5.3; Universal Design 5.0; Design 5.6; Total 22.1.

# RUD-Opus

## Browser findings

At [port 8204](http://localhost:8204/), registration updated the roster and schedule. Cancelling a confirmed attendee prompted a second confirmation action; the oldest eligible waiter was promoted, and the next waiter remained in order. Counts and schedule updated correctly.

## Responsive Design

The dark navy interface reflowed cleanly at tablet and mobile sizes. At 390 px it remained 390 px wide with no horizontal overflow; workshop cards stacked and metrics reflowed. Touch controls were larger than in the table-based apps. The long mobile page adds vertical navigation cost.

## Universal Design

This was the strongest accessibility/usability combination: controls had clear names, visible custom focus, status used text and color, and the mobile controls were comfortably sized. The app used actionable workshop buttons, cancellation confirmation, and panel-level loading/error feedback. Screen-reader behavior was inferred rather than directly tested.

## Build and automated tests

- Backend API build: passed with 0 warnings.
- Backend tests: 18 passed, 0 failed.
- Frontend production build: passed without warnings.
- Storybook build: passed with a large-chunk warning.

## Astra code-review findings

Astra recommended Code 8.5. The service uses immediate SQLite transactions, a globally persisted idempotency mapping, a unique active-registration constraint, FIFO ordering with a deterministic tie-breaker, and conflict-aware promotion. It persists each promotion before evaluating later waitlists. The frontend uses request sequence guards and per-panel loading/error state. See [RegistrationService.cs:112](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:112).

The most important source-confirmed risk is after a successful mutation: hooks retain old data when a refresh fails, while panels show refresh errors only when they have no data. This can leave stale counts, roster, and schedule visible without a refresh warning or retry control. [useAsync.ts:21](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/hooks/useAsync.ts:21), [RegistrationList.tsx:47](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/RegistrationList.tsx:47).

All tests use the HTTP pipeline and temporary file SQLite, including final-seat and same-key concurrency plus persisted-state reads. They do not include an explicit arrival barrier or timestamp-tie case, and there are no automated frontend behavior tests.

## Important defects and risks

- Likely hidden UI risk: a successful write followed by failed refresh can appear successful while stale state remains. This was source-confirmed, not reproduced in the browser.
- New idempotency key per submit means the UI cannot retry an uncertain response using the same key.
- Participant creation is an extra endpoint; a concurrent email uniqueness conflict may return a server error instead of a conflict response.

## Strengths

Strong domain separation, real HTTP-level tests, conflict-aware waitlisting, and high-quality responsive/accessibility behavior make this the most balanced UI. The README documents important design decisions and limitations in detail.

## Scores

Code 8.5; Requirements 9.4; Visual 8.7; Responsive 8.7; Universal Design 8.4; Design 8.6; Total 26.5.

# Sol

## Browser findings

At [port 8205](http://localhost:8205/), an available registration updated the count and schedule. With two waitlisted candidates, cancellation skipped Alex because of an overlapping confirmed workshop and promoted Taylor; Alex remained waitlisted. The resulting schedule reflected Taylor's confirmation.

## Responsive Design

At desktop and tablet the card list reflowed and fit. At 390 px the two-column card layout remained, and the registration form's horizontal controls extended to 435 px, leaving part of Register clipped beyond the viewport. Controls were smaller than the most usable touch size.

## Universal Design

Workshop cards were keyboard-operable buttons and displayed a visible browser focus outline. Status labels used text as well as color, and selects had labels. Smaller controls and the clipped mobile action reduce touch/motor accessibility.

## Build and automated tests

- Backend API build succeeded; two NU1900 warnings reflected the unreachable NuGet vulnerability-data endpoint.
- Backend tests: 9 passed, 0 failed; the same vulnerability-feed warning appeared during the test build.
- Frontend production build: passed without warnings.

## Astra code-review findings

Astra recommended Code 6.3. The backend service implements allocation, conflict-aware FIFO, repeat cancellation, half-open overlap, and caught post-commit notification failures. However, its process gate remains held while notifications run, so a slow notification service delays other registrations. Database backstops are weaker: no unique active-registration index or capacity/time check constraints were found. [RegistrationService.cs:15](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/RegistrationService.cs:15) and [SeatFlowDb.cs:6](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/SeatFlowDb.cs:6).

The frontend catches refresh errors internally, after which the mutation handler still displays success. Thus a committed change followed by failed refresh can leave stale data presented as successful. Unprotected selection requests can also overwrite a newer selection. [App.tsx:26](/Users/oddbjornmidbo/Source/App-test/Sol/frontend/src/App.tsx:26).

The suite covers basic required cases. Its final-seat test uses separate contexts and a temporary file database, but starts two calls with Task.WhenAll and no barrier; they may execute effectively sequentially. Some promotion checks use the same tracked context. There are no HTTP tests, same-key concurrency tests, or true touching-boundary tests. These are test-confidence gaps, not proof that the service violates the rule.

## Important defects and risks

- Confirmed source defect: refresh failure is swallowed and then overwritten with a success message.
- Likely risk: no request identity guard protects the UI from cross-selection responses.
- Likely concurrency robustness risk: process gate covers notification latency, and database constraints are weaker than the other implementations.
- The same idempotency key is scoped to a workshop; the assignment did not explicitly require global scope, so this is a contract ambiguity rather than a missing baseline behavior.

## Strengths

The service is compact and contains the required core business rules. Available-seat registration and conflict-aware promotion worked in the browser. The UI exposes all required status categories and lets users inspect a participant schedule.

## Scores

Code 6.3; Requirements 8.3; Visual 6.8; Responsive 5.8; Universal Design 6.5; Design 6.4; Total 21.0.

# Cross-implementation comparison

- **Functional correctness:** All six completed available-seat registration and the exercised cancellation/promotion workflows. Luna's stale schedule after a mutation is a confirmed required-UI defect. The other five reflected tested changes on the exercised successful paths. No backend seat-allocation or overlap-prevention defect was confirmed.
- **Concurrency:** Luna-optimized and Opus provide the strongest test evidence with file-backed SQLite, independent contexts, coordinated starts, and persisted assertions. RUD-Opus has useful HTTP-level concurrency tests and persisted reads, but no explicit start barrier. Sonnet uses file-backed tests without a barrier and serializes callers with a process semaphore. Luna's shared in-memory connection tests and Sol's unbarriered two-call test establish less. This is confidence ranking, not a finding that the weaker implementations overbook.
- **Idempotency:** All implementations have backend key handling. Luna-optimized preserves the pending key for retry and validates replay identity. Luna, Opus, Sonnet, RUD-Opus, and Sol create a new key on each UI submit, limiting lost-response recovery. Sol's key scope is workshop-specific; the requirement does not define global scope.
- **Waitlists and schedules:** All exercised conflict-aware promotion successfully. Luna-optimized's stored queue position gives deterministic ordering; the others use creation timestamps with an ID tie-break. Luna's displayed earlier/later waiter order remains inconclusive because the earlier waiter's conflict eligibility was not verified. Sol, RUD-Opus, Opus, and Sonnet refreshed relevant schedule/roster data on observed success. Luna did not reliably refresh schedule.
- **Notifications:** All six provide notification abstractions and isolate notification exceptions from the committed registration/cancellation path. Sol holds its process gate while awaiting notification work, which can extend mutation latency.
- **API quality:** The required routes and validation paths exist across the implementations. Opus has the broadest HTTP behavior tests; RUD-Opus also tests through the real HTTP pipeline. Luna, Opus, and Sonnet return some Location URLs whose target GET-by-ID resource is not registered. This is API polish, not a missing required route.
- **Frontend state:** Luna-optimized and RUD-Opus guard async response ordering. Luna, Opus, Sonnet, and Sol can allow older selection responses to replace newer data. Luna's schedule is not invalidated after mutation; RUD-Opus can suppress refresh errors while keeping stale data; Sol can overwrite refresh errors with success.
- **Tests:** Opus has the broadest suite (34) and the most convincing coordinated multi-context concurrency coverage. Luna-optimized's 16 tests also provide strong concurrency evidence and deterministic FIFO tie coverage. Sonnet (26) has broad HTTP tests but weaker concurrency scheduling; RUD-Opus (18) has HTTP concurrency and persisted-state evidence; Luna (12) and Sol (9) pass but leave larger confidence gaps. Test counts are not treated as a quality score by themselves.
- **Architecture and maintainability:** Opus has the clearest domain/service/endpoint separation. Luna-optimized and RUD-Opus are also well structured and proportionate. Sonnet has solid service separation. Luna remains reasonably simple. Sol is the most compressed, making endpoint logic and error cases harder to review.
- **Visual and responsive design:** Luna-optimized and RUD-Opus look most polished. Luna-optimized leads visual hierarchy; RUD-Opus has the strongest overall mobile/readability balance. Opus and Sonnet are readable on desktop but their tables overflow on mobile. Sol's Register action is partially clipped at mobile width.
- **Universal Design:** RUD-Opus leads with button-based workshop navigation, visible focus, clearer text status, and larger touch controls. Opus and Sonnet lose access to the primary workshop-selection workflow because selectable rows are not keyboard-focusable.

# Variant comparison

## Luna → Luna-optimized

Score changes in this evaluation: Code +2.0, Requirements +1.2, Visual +0.6, Responsive +0.4, Universal Design +0.5, Design +0.5, Total +3.7.

The optimized variant improved the schedule's mutation refresh, state guards, conflict/duplicate affordances, idempotency retry handling, SQLite transaction/index backstops, deterministic queue position, and test credibility. Its test suite grew from 12 to 16 and shifted from a shared in-memory connection without a barrier to file-backed tests with independent contexts and coordinated callers. Both remain polished responsive interfaces, with the optimized version's stronger hierarchy offset slightly by small muted labels. Luna's schedule-refresh defect and the unverified waiter anomaly were not present in the optimized app's observed workflows.

## Opus → RUD-Opus

Score changes in this evaluation: Code +0.1, Requirements 0.0, Visual +2.2, Responsive +3.2, Universal Design +3.2, Design +2.9, Total +3.0.

RUD-Opus materially improves table overflow, keyboard navigation, touch sizing, and the mobile experience while retaining a strong transactional backend and meaningful concurrency coverage. Opus still has stronger breadth and barrier-based concurrency tests (34 versus 18 total tests). RUD-Opus lacks an explicit concurrency arrival barrier and can hide refresh failures after a successful write. These differences are bounded and do not indicate a backend correctness regression.

# Final ranking

| Rank | Implementation | Total /30 | Notes |
| ---: | --- | ---: | --- |
| 1 | Luna-optimized | 26.6 | Best combined engineering evidence and polished UI; only 0.1 ahead of RUD-Opus. |
| 2 | RUD-Opus | 26.5 | Best balanced responsive and accessible experience; close to first. |
| 3 | Opus | 23.5 | Best automated tests and strong backend; major mobile and keyboard-selection weaknesses. |
| 4 | Luna | 22.9 | Polished and responsive, but schedule invalidation and test-confidence gaps lower its score. |
| 5 | Sonnet | 22.1 | Complete backend behavior and broad tests, with mobile overflow and keyboard-selection gaps. |
| 6 | Sol | 21.0 | Core rules work, but weak state/error handling, thinner tests, and clipped mobile action. |

## Category winners

- **Best backend/domain implementation:** Luna-optimized, narrowly, for durable idempotency, transaction ordering, constraints, and queue-position FIFO. Opus and RUD-Opus are close.
- **Best automated tests:** Opus, for the broadest suite plus independent file-backed contexts, coordinated callers, HTTP tests, and persisted assertions.
- **Best frontend engineering:** Luna-optimized, for request guards, mutation invalidation, retry-key handling, and explicit write-versus-refresh outcomes.
- **Best architecture:** Opus, for clear domain/service/endpoint separation with proportionate abstractions.
- **Best API quality:** No meaningful sole winner; Opus has the broadest HTTP evidence, while RUD-Opus has strong typed errors and full-pipeline tests.
- **Best visual design:** Luna-optimized, for hierarchy and restrained polish.
- **Best responsive design:** RUD-Opus, for clean reflow and no horizontal overflow across the tested sizes.
- **Best Universal Design/accessibility:** RUD-Opus, for keyboard-reachable navigation, visible focus, larger controls, and text status.
- **Best mobile experience:** RUD-Opus, closely followed by Luna-optimized.
- **Best runtime robustness:** Luna-optimized, for the clearest end-to-end state updates and conflict-aware promotion in the tested workflows.
- **Best documentation/developer experience:** No meaningful sole winner; Opus and RUD-Opus have the most detailed operational and architecture guidance.
- **Most serious directly observed bug:** Luna's participant schedule remained stale after a mutation until the participant was reselected.
- **Most serious source-confirmed hidden bug:** Sol can report a successful mutation after its refresh failed, leaving stale data visible; RUD-Opus has a related silent-refresh risk.
- **Most polished overall application:** RUD-Opus, based on the combination of strong visuals, mobile reflow, and accessible controls.
- **Simplest implementation that still satisfies the assignment well:** Sonnet; its backend covers the required behaviors and has broad tests, while frontend interaction quality is more basic.

