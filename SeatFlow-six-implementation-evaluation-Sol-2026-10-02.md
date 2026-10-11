# SeatFlow six-implementation evaluation

## Executive summary

Luna-optimized and RUD-Opus are the strongest overall applications. Both completed the exercised registration, waitlist, conflict, cancellation, and schedule flows, and both work well at the requested mobile size. Luna-optimized edges ahead through stronger frontend state handling and directly gated, persisted concurrency evidence. RUD-Opus has the most inclusive interface, with larger controls, explicit focus styling, and clearer status announcements.

Opus has the strongest automated tests and a strong backend, but its workshop table is difficult to use on a phone and its clickable rows are not keyboard reachable. Luna has a more usable small-screen layout, but a successful registration left its already-selected participant schedule stale in the browser. Sonnet and Sol meet most functional flows; Sonnet's narrow table and Sol's clipped mobile registration action limit their overall results. **No score includes a penalty for a stopped server or unavailable infrastructure.**

## Overall scores

| Implementation | Code /10 | Requirements /10 | Design /10 | Total /30 |
| --- | ---: | ---: | ---: | ---: |
| Luna-optimized | 8.8 | 9.2 | 8.5 | **26.5** |
| Luna | 6.8 | 8.5 | 8.0 | **23.3** |
| Opus | 8.5 | 9.1 | 5.4 | **23.0** |
| Sonnet | 7.6 | 8.8 | 5.2 | **21.6** |
| RUD-Opus | 8.5 | 9.2 | 8.3 | **26.0** |
| Sol | 6.5 | 8.6 | 6.4 | **21.5** |

Scores assess each implementation against the assignment, without normalizing them to force an order. A passing build or test suite is evidence, not proof of full correctness.

## Design scores

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| --- | ---: | ---: | ---: | ---: |
| Luna-optimized | 8.8 | 9.0 | 7.7 | **8.5** |
| Luna | 8.3 | 8.7 | 7.1 | **8.0** |
| Opus | 6.9 | 4.2 | 5.2 | **5.4** |
| Sonnet | 6.6 | 4.1 | 5.0 | **5.2** |
| RUD-Opus | 8.2 | 8.8 | 8.0 | **8.3** |
| Sol | 6.7 | 6.2 | 6.2 | **6.4** |

Each average is the arithmetic mean of its three dimensions, rounded once to one decimal place.

## Method and evidence notes

I evaluated the live UIs first, at their exact assigned URLs: Luna-optimized 5173, Luna 8201, Opus 8202, Sonnet 8203, RUD-Opus 8204, and Sol 8205. The running frontend processes and working directories matched those folders. I used separate browser tabs for functional workflows, then a dedicated tab at **1440 × 900, 768 × 1024, and 390 × 844** for each design pass. At each size I selected workshops or inspected detail/actions rather than relying only on a screenshot. I checked document width against viewport width at mobile size.

Browser scenarios covered available and full registration, duplicate attempts, overlapping-session rejection, cancellation, ordered conflict-aware promotion, participant schedules, state changes after a failed action, and selection changes. The different pre-existing database contents were treated as test fixtures, not as defects. These browser actions changed local registration data.

After the browser and design passes, I built each .NET 9 API and each React production frontend, ran each complete backend test project, and built both available Storybooks. I then reused **only the independent Astra source review from the Luna run**, as requested, and checked its important claims against source and the fresh evidence here. I did not read or use prior evaluation reports, scores, rankings, or browser/build conclusions.

Luna's frontend at 8201 and Sol's API behind 8205 were initially stopped; I started them on their assigned ports and completed the evaluation. This is an infrastructure observation and affects no model score. The shell's default Node was too old for TypeScript; all six frontend builds passed with the installed Node 22. Four parallel solution-wide .NET builds stalled without compiler errors, so I built each API project directly and ran the test projects; all six API builds passed. Sol's NU1900 warning came from the unavailable NuGet vulnerability feed, not a compilation error. RUD-Opus initially reported 17/18 in a parallel test run because participant creation returned HTTP 500 in its concurrency test setup. That test passed in isolation and the complete suite passed 18/18 when rerun alone; the intermittent setup failure is recorded, without asserting a production booking defect.

I inspected keyboard-reachable controls and focus where practical, plus DOM semantics and CSS. I did **not** run a screen reader or browser zoom test. Screen-reader behavior and zoom resilience are therefore inferred, while the stated viewport and horizontal-overflow results were directly checked.

# Luna-optimized

## Browser findings

Available registration added Alex to AI Toolkit and immediately updated capacity, roster, and his selected schedule. The app disabled the duplicate action and explained why. It also disabled a conflicting available booking for Casey with an explanation. On a full workshop, Casey joined behind Alex and Taylor. Cancelling Sam skipped conflicting Alex and promoted eligible Taylor; the roster and Taylor's schedule agreed. Counts, messages, and selection changes stayed coherent in the exercised paths.

## Responsive Design

At desktop the dark, restrained two-column layout gives workshops and booking detail distinct regions. Tablet retained the columns without horizontal overflow. At 390 px the panels stacked; workshop, registration, roster, and schedule actions remained reachable, with document width equal to 390 px. The mobile page is long, but the sequence is understandable.

## Universal Design

Workshop cards are real buttons with pressed state, and the tested keyboard focus outline was visible. Loading/error regions and labels have useful semantics. Status is conveyed in text as well as color. The main weakness is very small and muted secondary text, especially dates, captions, and compact status details; this lowers practical readability and touch comfort despite the polished styling.

## Build and automated tests

.NET API build passed with 0 warnings and 0 errors. Backend tests passed **16/16**. Frontend production build passed. Storybook build passed; its third-party preview emitted eval and large-chunk warnings, but no build failure. The tests use separate contexts and file-backed SQLite, coordinate two callers at a start gate, and reread persisted state; HTTP tests cover API details. [Concurrency test](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/backend/SeatFlow.Tests/RegistrationServiceTests.cs:274)

## Astra code-review findings

The service takes an immediate SQLite write transaction before capacity, duplicate, and key checks; a persisted idempotency record, monotonic queue position, filtered unique active-registration index, and database checks provide strong backstops. Notifications are sent after commit with failures contained. The frontend uses request sequence guards and explicit refresh errors. [Registration service](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/backend/SeatFlow.Api/RegistrationService.cs:59), [database model](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/backend/SeatFlow.Api/SeatFlowDbContext.cs:14), [frontend state](/Users/oddbjornmidbo/Source/App-test/Luna-optimized/frontend/src/App.tsx:43)

## Important defects and risks

- **Likely narrow risk:** A replay of a pending UI retry key after a cancellation can return the old registration rather than create a new one. This was identified in source review, not reproduced through the UI.
- **Design/accessibility concern:** Tiny secondary labels and subdued contrast reduce legibility.
- **Missing evidence:** No live screen-reader or zoom result.

## Strengths

Coherent live state after mutations, careful backend transaction design, credible final-seat tests, resolvable API response paths, responsive mobile layout, and useful README/Storybook documentation.

## Scores

**Code 8.8 · Requirements 9.2 · Visual 8.8 · Responsive 9.0 · Universal Design 7.7 · Design 8.5 · Total 26.5**

# Luna

## Browser findings

Available bookings, duplicate rejection, full-seat waitlisting, conflict rejection, and conflict-aware promotion worked. Riley, first in the Designing queue, retained a conflict with Practical TypeScript; after Avery cancelled, Morgan was promoted and Riley remained waiting. A later successful booking cleared a prior conflict error. **Directly observed defect:** after registering Avery while Avery was selected in the schedule panel, the roster/count changed but that schedule still said there were no confirmed workshops. Changing the selected participant forced a new schedule load and showed the booking.

## Responsive Design

The light dashboard uses clear workshop cards and well-separated panels. At tablet it reflowed to two columns without horizontal overflow; at 390 px it stacked into readable full-width cards and controls. A long roster requires scrolling, but primary actions remain reachable. Document width matched the 390 px viewport.

## Universal Design

Headings, text status labels, and ordinary form controls make the flow understandable. The 390 px layout preserves readable main titles. Several captions, controls, and cancellation labels are 9–11 px; a select removes its native outline and substitutes a faint shadow. These choices weaken focus visibility, legibility, and touch comfort. [UI styles](/Users/oddbjornmidbo/Source/App-test/Luna/frontend/src/style.css:25)

## Build and automated tests

.NET API build passed with 0 warnings and 0 errors. Backend tests passed **12/12**. Frontend production build passed. No Storybook or frontend test script was present. The concurrency test uses a shared in-memory SQLite connection and Task.WhenAll without a coordinated start or a fresh persisted-state assertion; it is weaker evidence than the file-backed gated tests in Luna-optimized or Opus.

## Astra code-review findings

The service separates booking rules from endpoints, serializes local mutations, and uses transactions. FIFO orders by time and ID; this fresh browser run showed correct conflict skipping. The frontend loads schedule only when the participant selection changes, while registration/cancellation refreshes the roster and workshop list—exactly matching the observed stale schedule. Cross-selection requests lack response guards. The created registration's Location path has no corresponding GET endpoint. [Frontend refresh](/Users/oddbjornmidbo/Source/App-test/Luna/frontend/src/App.tsx:64), [API Location](/Users/oddbjornmidbo/Source/App-test/Luna/backend/SeatFlow.Api/Program.cs:71)

## Important defects and risks

- **Directly observed runtime defect:** Selected participant schedule remained stale after a successful registration.
- **Confirmed source-code defects:** Mutation handlers omit schedule refresh; creation returns a Location URL without a matching resource endpoint.
- **Likely risk:** A slower response for an earlier workshop/participant selection may overwrite a later selection's data.
- **Missing evidence:** The passing final-seat test does not strongly establish actual simultaneous contention across independent database connections.

## Strengths

Good visual organization and mobile reflow, the exercised domain rules, clear business-rule separation, and candid documentation of its single-process assumption.

## Scores

**Code 6.8 · Requirements 8.5 · Visual 8.3 · Responsive 8.7 · Universal Design 7.1 · Design 8.0 · Total 23.3**

# Opus

## Browser findings

Available registration filled Async C# Deep Dive and updated roster/count. On full Intro to EF Core, Linus and Ada were earlier but conflicted; cancelling Grace promoted later eligible Alan, and Alan's schedule moved from waitlisted to confirmed. An overlapping Testing Strategies booking was rejected; a later valid Margaret booking cleared the error and updated the schedule. Duplicate selection was disabled for an active booking. The extra workshop-creation control and notification log are optional features, not requirement credit.

## Responsive Design

Desktop offers a useful two-column overview, though the table and developer log are dense. Tablet stacks the panels and remains usable. At **390 px**, the page had **465 px document width**: table cells squeezed into tiny wrapped text and required horizontal movement; workshop selection and detail scanning became difficult. This is a directly measured layout issue, not a style preference.

## Universal Design

The workshop rows are clickable table rows with no tabindex or interactive role, so the primary workshop-selection workflow is not keyboard reachable. Registration and schedule selects lacked useful accessible names in the browser accessibility tree. Status text is present, but small mobile text and cramped columns impair reading and touch use. [Workshop table](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:164)

## Build and automated tests

.NET API build passed with 0 warnings and 0 errors. Backend tests passed **34/34**. Frontend production build passed. No Storybook or frontend test script was present. The tests provide the strongest overall concurrency evidence: file-backed SQLite, distinct contexts/connections, a coordinated start, persisted assertions, and HTTP coverage. [Concurrency tests](/Users/oddbjornmidbo/Source/App-test/Opus/backend/tests/SeatFlow.Tests/ConcurrencyTests.cs:13)

## Astra code-review findings

Immediate transactions protect check-then-write logic; a filtered active-registration unique index and global key storage protect duplicates and retries. Cancellation can revisit other waitlists newly made eligible by the cancelled participant's opened schedule. Notifications occur after commit. Frontend refresh requests for changing selections are unguarded, and the workshop-creation form clears after failed creation. [Registration service](/Users/oddbjornmidbo/Source/App-test/Opus/backend/src/SeatFlow.Api/Services/RegistrationService.cs:85), [frontend refresh](/Users/oddbjornmidbo/Source/App-test/Opus/frontend/src/App.tsx:35)

## Important defects and risks

- **Design/accessibility concern:** Severe mobile table overflow and keyboard-inaccessible workshop rows.
- **Likely risk:** An older async response can overwrite a newer selected workshop or participant.
- **Confirmed source-code API issue:** The optional workshop-creation endpoint returns a Location for /api/workshops/{id}, but there is no corresponding GET-by-ID endpoint. Registration creation's Location does resolve. [Endpoint mappings](/Users/oddbjornmidbo/Source/App-test/Opus/backend/src/SeatFlow.Api/Endpoints/SeatFlowEndpoints.cs:20)
- **Missing evidence:** No automated frontend state or accessibility tests.

## Strengths

Strong backend/domain behavior, broad and credible tests, useful validation and FIFO behavior, clear documentation, and a maintainable service boundary.

## Scores

**Code 8.5 · Requirements 9.1 · Visual 6.9 · Responsive 4.2 · Universal Design 5.2 · Design 5.4 · Total 23.0**

# Sonnet

## Browser findings

Available registration and exact-touching schedules worked: Bob held a session ending at 17:00 and another beginning at 17:00. A duplicate attempt showed a clear error. On full Domain-Driven Design, conflicting Carol stayed waiting while eligible Erin was promoted after Frank cancelled; Erin's schedule updated. An overlapping available booking was rejected, and a later valid booking cleared the error. Successful operations updated data, but gave little positive confirmation.

## Responsive Design

Desktop and tablet present a simple centered stack and an easy-to-scan selected-workshop detail. At **390 px**, document width was **476 px**. The workshop table's time strings collapsed into very narrow multi-line columns, and the page required horizontal movement. Main booking detail remained more readable below the table.

## Universal Design

Workshop selection is an onClick table row without a keyboard focus target, preventing a keyboard-only user from starting the main workflow. Participant selects also lacked visible programmatic labels in the inspected UI. The status badges use text, and the error has an alert role, but no equivalent positive result message. [Selection and form](/Users/oddbjornmidbo/Source/App-test/sonnet/frontend/src/App.tsx:90)

## Build and automated tests

.NET API build passed with 0 warnings and 0 errors. Backend tests passed **26/26**. Frontend production build passed. No Storybook or frontend test script was present. Service and HTTP tests use real temporary SQLite files and fresh contexts, but concurrency tests do not coordinate the callers at a start barrier.

## Astra code-review findings

The service owns transaction-protected capacity, FIFO, overlap, and idempotency rules; the active-registration and key indexes reinforce them. Notifications are isolated after commit. The process-wide gate is suitable to the documented single-instance assumption. Frontend refreshes can race across selections, use a new key on every retry, and do not give a clear positive success announcement. [Frontend state](/Users/oddbjornmidbo/Source/App-test/sonnet/frontend/src/App.tsx:41), [architecture notes](/Users/oddbjornmidbo/Source/App-test/sonnet/README.md:40)

## Important defects and risks

- **Design/accessibility concern:** Mobile overflow and keyboard-inaccessible primary selection.
- **Likely risk:** An old refresh can replace data for a newer selection.
- **Missing evidence:** The tests do not coordinate concurrency starts; passing them does not prove a hard final-seat race.

## Strengths

The exercised functional rules work, error recovery is clear, the backend remains fairly compact, and tests cover the real HTTP layer as well as service rules.

## Scores

**Code 7.6 · Requirements 8.8 · Visual 6.6 · Responsive 4.1 · Universal Design 5.0 · Design 5.2 · Total 21.6**

# RUD-Opus

## Browser findings

Available registration updated the Accessible Front-ends roster and capacity. A duplicate produced a clear error. On full Event Sourcing, conflicting Grace remained waiting while eligible Alan was promoted after Linus's confirmed booking was cancelled; Alan's schedule reflected promotion. Cancellation required an explicit second confirmation. An overlapping Kubernetes booking was rejected, then a valid booking succeeded and cleared the error.

## Responsive Design

The dark desktop layout separates a selectable workshop list from detailed registration cards. At tablet and 390 px it became a single column without horizontal overflow; the document width was exactly 390 px. Registration, cancellation, waitlist, and schedule controls remained reachable. Long detailed rosters cost scrolling, but do not clip actions.

## Universal Design

Workshop choices are actual buttons with pressed state; labels, status words, visible three-pixel focus outlines, live success/error text, cancellation confirmation, and reduced-motion handling support keyboard and motor use. Text and controls are generally larger than Luna-optimized's compact captions. Some muted dark-theme metadata could have stronger contrast. [Workshop controls](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/WorkshopList.tsx:22), [status messages](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/StatusMessage.tsx:11), [focus and motion](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/index.css:55)

## Build and automated tests

.NET API build passed with 0 warnings and 0 errors. Frontend production and Storybook builds passed; Storybook reported only a large-chunk warning. The first concurrent full-suite run gave **17/18** because a participant-creation setup request returned HTTP 500; the target test then passed alone, and the full suite passed **18/18** in isolation. Tests exercise the HTTP pipeline against file-backed SQLite with 20 registration callers and persisted count assertions, but lack a coordinated start barrier. [Final-seat test](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Tests/RegistrationTests.cs:245)

## Astra code-review findings

Immediate SQLite transactions, global idempotency keys, a filtered active-registration index, deterministic FIFO, typed errors, and post-commit notifications are strong. Cancellation also revisits other waitlists when a participant's schedule opens. The frontend guards older selection requests with sequence IDs and has component-level Storybook states. A source-confirmed error-display gap remains: useAsync retains cached data on reload failure, while roster error UI is shown only when there is no cached data. [Registration service](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/backend/SeatFlow.Api/Domain/RegistrationService.cs:112), [cached data](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/hooks/useAsync.ts:21), [hidden roster error](/Users/oddbjornmidbo/Source/App-test/RUD-Opus/frontend/src/components/RegistrationList.tsx:47)

## Important defects and risks

- **Confirmed source-code defect:** A failed roster refresh after data has loaded can leave the old roster visible without its error. This was not reproduced in the live UI.
- **Test-quality risk:** One parallel full-suite attempt failed in concurrency-test setup; later isolated and full reruns passed. The cause was not established.
- **Design concern:** Some secondary metadata is subdued against the dark background.

## Strengths

Complete exercised booking behavior, strong backend invariants, clear mobile cards, accessible core controls, helpful cancellation protection, and detailed documentation with Storybook coverage.

## Scores

**Code 8.5 · Requirements 9.2 · Visual 8.2 · Responsive 8.8 · Universal Design 8.0 · Design 8.3 · Total 26.0**

# Sol

## Browser findings

After its API was started on the configured backend port, the app completed the booking flows. Alex registered for Product Discovery immediately after Practical TypeScript ended, confirming non-overlap at a touching boundary. A duplicate showed a clear error. On full Designing, Alex remained waiting because of his Practical conflict and Casey was promoted after Sam cancelled; Casey's schedule updated. An overlapping available booking was rejected, then a valid Sam booking cleared the error and updated capacity and schedule.

## Responsive Design

Desktop uses a simple, orderly four-card workshop grid and a readable detail panel; tablet becomes a two-column grid. At **390 px**, the document width was **435 px**. Workshop cards stack, but the participant select and Register button stay on one flex row, pushing part of the button beyond the viewport. The action is available after horizontal scrolling, yet this is a material mobile usability problem. [Form layout](/Users/oddbjornmidbo/Source/App-test/Sol/frontend/src/style.css:1)

## Universal Design

Workshop selection uses native buttons and form controls have visible labels; status words supplement colors, and messages use a status role. The clipped mobile action, dense roster rows, and lack of a dedicated visible-focus treatment weaken practical access. A screen reader and zoom were not tested.

## Build and automated tests

.NET API build passed with no compiler errors; its only warning was NU1900 from the unreachable NuGet vulnerability service. Backend tests passed **9/9**. Frontend production build passed. No Storybook or frontend test script was present. The final-seat test uses separate contexts and a file-backed database with a persisted count assertion, but its two Task.WhenAll calls have no coordinated start and may run effectively sequentially. [Final-seat test](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Tests/RegistrationTests.cs:78)

## Astra code-review findings

The compact service implements half-open overlap, FIFO skip, repeat cancellation, and notification failure containment. Its static gate protects local requests but remains held while awaiting notifications, increasing contention. The database has no unique filtered index for active registrations, and idempotency keys are unique only within a workshop; the assignment's same-request replay works, while cross-workshop key reuse has weaker semantics. A confirmed frontend flaw is that refresh functions catch and swallow errors, after which the mutation handler unconditionally sets a success message. Thus a successful write followed by a failed refresh can present stale data as complete success. [Service](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/RegistrationService.cs:15), [indexes](/Users/oddbjornmidbo/Source/App-test/Sol/backend/SeatFlow.Api/SeatFlowDb.cs:6), [refresh path](/Users/oddbjornmidbo/Source/App-test/Sol/frontend/src/App.tsx:26)

## Important defects and risks

- **Confirmed source-code defect, not reproduced live:** Refresh failure can be hidden behind a success message and stale roster/schedule data.
- **Design/accessibility concern:** Registration button is partly offscreen at 390 px.
- **Likely risk:** Unguarded async selection refreshes can show data from a previous selection.
- **Missing evidence:** No strong coordinated concurrency test or HTTP-level test suite.

## Strengths

Small, understandable implementation; exercised booking and promotion rules work; accessible native workshop buttons; clear API/architecture explanation in the README.

## Scores

**Code 6.5 · Requirements 8.6 · Visual 6.7 · Responsive 6.2 · Universal Design 6.2 · Design 6.4 · Total 21.5**

# Cross-implementation comparison

| Area | Evaluation |
| --- | --- |
| Functional correctness | All six completed the exercised seat, waitlist, conflict, and cancellation paths. Luna alone showed a directly observed stale selected schedule after a successful registration. |
| Concurrency | Luna-optimized, Opus, and RUD-Opus use immediate SQLite write transactions. Opus and Luna-optimized have the strongest coordinated, persisted final-seat evidence. Sonnet and Sol rely more on process gates; Luna's and Sol's test evidence is weaker. Weak tests are not proof of unsafe production code. |
| Idempotency | All implement replay. Luna-optimized, Opus, Sonnet, and RUD-Opus persist global keys with mismatched-request handling. Sol scopes its key index and lookup by workshop. UI-generated keys generally change per new click; Luna-optimized manages a pending attempt more deliberately. |
| Waitlists and scheduling | Browser promotion skipped conflicting earlier waiters in every app. Half-open time boundaries were exercised in Sonnet and Sol. Opus and RUD-Opus also reprocess other waitlists when a participant's cancellation frees their schedule. |
| Notifications | Each has an abstraction and protects committed writes from delivery failure. The development UI log in Opus is optional and receives no requirement bonus. There is **no meaningful winner** for the required notification behavior. |
| API quality | Luna-optimized has the clearest validation and resolvable created-resource paths. Luna's registration creation and Opus's optional workshop creation return Location paths without matching GET endpoints; Opus's registration Location does resolve. |
| Frontend state | Luna-optimized has the most deliberate request sequencing and mutation refresh. RUD-Opus also guards selection races but can conceal a reload error over cached data. Luna does not refresh the selected schedule after mutation; Opus, Sonnet, and Sol have unguarded cross-selection responses. Sol can replace a refresh error with success. |
| Tests | Opus has the broadest credible set, followed closely by Luna-optimized's focused gated/file-backed coverage. RUD-Opus has strong HTTP breadth but one intermittent setup failure and no start barrier. Counts alone do not explain these differences. |
| Architecture and maintainability | The best backends keep rules in services and endpoints thin. Opus, Luna-optimized, and RUD-Opus are particularly clear. Sol is compact but its compressed source and weaker database invariants make changes harder to reason about. |
| Design and mobile use | Luna-optimized leads visual polish and measured reflow; RUD-Opus leads inclusive mobile controls. Luna is well organized but uses very small labels. Opus and Sonnet have severe 390 px table overflow; Sol's form button is clipped. |
| Documentation | All six document run steps and assumptions. Luna-optimized, Opus, and RUD-Opus give the clearest transaction and testing explanations; the first and last also include Storybook. |

# Variant comparison

## Luna → Luna-optimized

| Score | Luna | Luna-optimized | Change |
| --- | ---: | ---: | ---: |
| Code | 6.8 | 8.8 | +2.0 |
| Requirements | 8.5 | 9.2 | +0.7 |
| Visual | 8.3 | 8.8 | +0.5 |
| Responsive | 8.7 | 9.0 | +0.3 |
| Universal Design | 7.1 | 7.7 | +0.6 |
| Design average | 8.0 | 8.5 | +0.5 |
| Total | 23.3 | 26.5 | **+3.2** |

The main gain is engineering correctness: the optimized UI refreshes schedule/count/roster together and guards stale async responses, whereas Luna left the selected schedule stale in the live test. Its backend adds an immediate SQLite transaction, persisted global idempotency record, stronger constraints, and gated file-backed concurrency tests. Both have attractive responsive layouts and small secondary text; Luna-optimized improves focus and status semantics, while its dark dense design still has legibility limits. Both READMEs explain run steps and limitations; Luna-optimized adds useful Storybook states and more detailed test/API documentation. No server-start difference contributes to these deltas.

## Opus → RUD-Opus

| Score | Opus | RUD-Opus | Change |
| --- | ---: | ---: | ---: |
| Code | 8.5 | 8.5 | 0.0 |
| Requirements | 9.1 | 9.2 | +0.1 |
| Visual | 6.9 | 8.2 | +1.3 |
| Responsive | 4.2 | 8.8 | +4.6 |
| Universal Design | 5.2 | 8.0 | +2.8 |
| Design average | 5.4 | 8.3 | +2.9 |
| Total | 23.0 | 26.0 | **+3.0** |

RUD-Opus replaces the overflowing, non-keyboard-selectable table with reachable cards and a well-structured mobile detail view; it adds explicit focus, status announcements, reduced-motion handling, and cancellation confirmation. Its immediate-transaction backend retains the core correctness of Opus and the browser promotion tests passed. Opus still has stronger test evidence through coordinated callers and persisted assertions, and RUD-Opus introduces a cached-refresh error-display defect. RUD-Opus's Storybook and state documentation improve UI development experience; Opus's backend documentation remains excellent. The functional differences are small; the measured responsive and Universal Design gains drive the overall delta.

# Final ranking

| Rank | Implementation | Total /30 | Reading |
| ---: | --- | ---: | --- |
| 1 | Luna-optimized | **26.5** | Strongest overall balance; only 0.5 ahead of RUD-Opus. |
| 2 | RUD-Opus | **26.0** | Best inclusive/mobile experience; strong backend. |
| 3 | Luna | **23.3** | Strong design, with a meaningful stale-schedule defect. |
| 4 | Opus | **23.0** | Strong code and tests, held back by mobile and keyboard access. |
| 5 | Sonnet | **21.6** | Functional and compact, with poor narrow-table usability. |
| 6 | Sol | **21.5** | Functional core, but weaker refresh handling and clipped mobile action. |

Luna and Opus are separated by only 0.3 points; Sonnet and Sol by only 0.1. Those pairs are effectively close calls under these independent category scores.

## Category winners

| Category | Winner | Reason |
| --- | --- | --- |
| Best backend/domain implementation | **Luna-optimized** | Immediate transactions, persistent global replay records, queue position, and database constraints. |
| Best automated tests | **Opus** | Broad file-backed, independently contextual, gated and HTTP concurrency coverage. |
| Best frontend engineering | **Luna-optimized** | Guarded requests, deliberate retry key, coherent mutation refresh. |
| Best architecture | **Opus** | Clear domain service, thin endpoint boundary, and proportionate abstractions. |
| Best API quality | **Luna-optimized** | Validation and resolvable created-resource URLs. |
| Best visual design | **Luna-optimized** | Clear hierarchy and restrained, coherent styling. |
| Best responsive design | **Luna-optimized** | Clean reflow at all three measured sizes with no horizontal overflow. |
| Best Universal Design / accessibility | **RUD-Opus** | Keyboard-reachable selection, focus, announcements, labels, and reduced-motion handling. |
| Best mobile experience | **RUD-Opus** | Readable cards and controls, with no clipping at 390 px. |
| Best runtime robustness | **Luna-optimized** | Most consistent live cross-panel updates plus strong backend safeguards. |
| Best documentation/developer experience | **RUD-Opus** | Detailed README, component Storybook states, and clear UI behavior documentation. |
| Most serious directly observed bug | **Luna** | Successful booking left the selected participant schedule stale. |
| Most serious source-confirmed hidden bug | **Sol** | Refresh failure can be swallowed and followed by a false success message. |
| Most polished overall application | **Luna-optimized** | Best aggregate of code, required behavior, and design. |
| Simplest implementation that still satisfies the assignment well | **Sonnet** | Compact backend and working core flows, despite UI responsiveness gaps. |

There is no meaningful winner for the assignment's required **notification behavior**: all six isolate delivery failure from committed registration changes, and the optional debug log does not change that judgment.
