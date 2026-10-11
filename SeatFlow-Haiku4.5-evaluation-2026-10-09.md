# SeatFlow Haiku 4.5 evaluation

## Result

**15.3/30.** Haiku 4.5 delivered a usable basic SeatFlow application, but a live concurrent request reproduced overbooking of the final seat. The implementation also misses the required .NET 9 runtime, relies on volatile idempotency keys, and does not complete waitlist promotion if a cancellation notification fails. The workshop overview and participant schedule adapt reasonably to a phone; registration tables overflow and cut off actions.

| Category | Score | Reason |
| --- | ---: | --- |
| Code | **4.5/10** | Clear, compact service/controller structure and passing sequential tests, but no protection for final-seat allocation, unsafe idempotency storage, and non-atomic cancellation/promotion. |
| Requirements | **6.0/10** | Core sequential booking, waitlisting, conflict-aware promotion, routes, seed data, builds, and README are present. Concurrent capacity safety fails; .NET 9, notification failure isolation, visible Cancelled state, and direct schedule access are missing. |
| Visual | **5.6/10** | Clear cards, status cues, and an understandable roster, with basic visual styling. An unused starter stylesheet overrides the header heading color in light mode, reducing contrast. |
| Responsive | **4.8/10** | Overview and schedule reflow at 768 px and 390 px. The phone registration tables widen the document to about 466 px and put action buttons offscreen. |
| Universal Design | **4.0/10** | Native controls, headings, table headers, and text status cues help. Unassociated select labels, generic error messages, an empty initial Schedule tab, missing Cancelled state, and clipped phone actions impede use. |
| **Design average** | **4.8/10** | Mean of Visual, Responsive, and Universal Design, rounded to one decimal. |
| **Total** | **15.3/30** | Code + Requirements + Design. |

## Scope and method

This is a **separate evaluation** of the `Haiku4.5` implementation against the [original SeatFlow prompt](/Users/oddbjornmidbo/Source/App-test/prompt.md), using the same Code + Requirements + Design rubric as the earlier assessments. The user reports an implementation run of **9m 34s** and **$1.03** in credits. They also report that actual elapsed wall time was longer because the run required frequent human acceptance and could not proceed fully autonomously. These are implementation-run figures, not time or cost measured during this evaluation.

I started the API and frontend before evaluating them and assigned **no penalty for servers being stopped**. Because port 5000 was occupied by an unrelated service and the frontend hardcodes that port, I ran the API on port 5192 with a disposable SQLite working directory and used a temporary Vite rewrite for the evaluation browser. The implementation source was not changed. I exercised the local API and interface at **1440 × 900**, **768 × 1024**, and **390 × 844**, reviewed source and tests, ran fresh builds and the backend suite, and obtained a separate **GPT-6 Astra high-mode** source review. The API behavior checks changed only the disposable evaluation database.

## Live behavior

- **Sequential booking worked.** On a seeded two-seat workshop, Alice and Bob were confirmed; Carol and David joined the waitlist. Duplicate registration and overlapping confirmed registration were rejected. Cancelling Alice promoted Carol, and a second cancellation returned successfully. In a conflict-aware FIFO scenario, David was skipped while waitlisted because of his overlapping confirmed workshop, and the next eligible participant, Eve, was promoted after Bob cancelled. Carol's schedule displayed her confirmed workshop. A sequential repeat with the same `Idempotency-Key` returned the original registration ID.
- **Concurrent final-seat allocation failed.** In a separate disposable API database, I created a one-seat workshop and sent two registration requests concurrently. Both responses were **201 Confirmed**; the roster then contained **two Confirmed registrations for capacity one**. This directly violates the prompt's explicit contention requirement. [Capacity read/write](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:32), [database configuration](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Data/SeatFlowContext.cs:14)
- **UI behavior had gaps.** Workshop cards and a selected roster loaded, registration and waitlist updates were visible, and a confirmed participant's schedule loaded. Clicking the Schedule tab before selecting a participant produced an empty main area. Cancelled registrations disappeared from the roster instead of showing a Cancelled status. A rejected registration showed only **“Registration failed”**, without the API's useful reason; that banner remained visible after navigating to the schedule. [Schedule gating and feedback](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/App.tsx:94), [roster filtering](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/components/WorkshopDetail.tsx:29)

The registration conflict check also runs before deciding whether a full workshop would waitlist the participant. In a live request, a participant with an overlapping confirmed workshop was rejected from that full workshop's waitlist, even though the specification prohibits overlapping **Confirmed** workshops. [Conflict and status order](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:47)

## Layout and accessibility

The desktop overview presents workshops in clear three-column cards, and tablet/phone widths stack those cards sensibly. The participant schedule also fits a **390 px** viewport. In the selected workshop, however, the confirmed and waitlist tables stay wider than the phone screen: the document measured about **466 px** and the right-side **View Schedule** and **Cancel** controls were cut off in the initial viewport. The tablet roster fit at **768 px** but was dense. The retained Vite starter [index.css](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/index.css:1) also conflicts with the app's own styles and can render the main heading dark against the dark header in light color scheme.

The interface uses semantic headings, native buttons/selects, table headers, and text along with color for availability. The workshop registration select has no associated label; the schedule's label is not bound to its select; repeated action names lack participant context. Error feedback discards the server's specific explanation. No screen reader or browser-zoom audit was performed. [Workshop form](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/components/WorkshopDetail.tsx:53), [schedule selector](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/components/ParticipantSchedule.tsx:28)

## Builds and tests

| Fresh check | Result |
| --- | --- |
| .NET backend build | **Passed**, 0 warnings, 0 errors, but targets **.NET 8** rather than required .NET 9 |
| xUnit backend suite | **10/10 passed**, 0 skipped |
| React/TypeScript/Vite production build | **Passed** with Node 22 |
| Frontend lint | **Passed** with two React immutability warnings in `App.tsx` |
| Frontend automated tests | **None supplied** |

The ten service tests cover useful ordinary cases, including FIFO promotion and skipping a conflicting waitlisted participant. They use EF InMemory and a single context per test. **No test covers concurrent attempts for the final seat**, despite the README's claim of concurrency test coverage and database-level protection. There are no notification-failure, SQLite integration, HTTP, or restart-idempotency tests. [Service tests](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Tests/RegistrationServiceTests.cs:51), [README claims](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/README.md:83)

## Astra high-mode source review

The service/controller/model split is compact and readable. All six required API routes exist; EF Core/SQLite, React/TypeScript/Vite, xUnit, four workshops, five participants, and overlapping seed times are present. The sequential overlap check uses half-open intervals, so workshops that meet exactly at a boundary are allowed. [Overlap rule](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:148)

The main code findings were:

1. **Capacity and duplicate decisions have no atomic protection.** Registration loads and counts existing rows, then saves a new row separately, with no transaction, lock, concurrency token, or database constraint. The live overbooking result confirms the capacity risk. Concurrent duplicate and conflict races are also plausible from the same sequence. [Registration service](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:32)
2. **Cancellation, notification, and promotion can leave inconsistent state.** The cancellation is committed before awaiting notification and before promotion. A notification exception stops promotion, and retrying the already cancelled registration returns immediately. A fresh registration can also take the vacancy before promotion, after which promotion does not recheck capacity. Notification failure after registration commit can return an error for a persisted registration. These failure paths were identified from source, not reproduced in the live session. [Cancellation and promotion](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:96)
3. **Idempotency keys are volatile and unsynchronized.** The static `Dictionary` is written after saving the registration. Restarting loses keys; concurrent same-key requests can both miss the map. The API does not verify that a reused key matches the same request. Sequential replay worked during this evaluation, but the reliability requirement is not met. [Idempotency map](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Services/RegistrationService.cs:11)
4. **Several smaller requirements are incomplete.** Projects target .NET 8; creating a workshop does not validate that its end follows its start; a newly confirmed registration does not refresh an already opened participant schedule; and the frontend hides Cancelled registrations. [API target](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/SeatFlow.Api.csproj:4), [workshop validation](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/SeatFlow.Api/Controllers/WorkshopsController.cs:38), [schedule refresh](/Users/oddbjornmidbo/Source/App-test/Haiku4.5/frontend/src/App.tsx:98)

## Screenshots

Nine device captures are in the existing [screenshot folder](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08): **overview, waitlist, and schedule** at desktop, tablet, and phone sizes. Each filename identifies **Haiku 4.5**, device, and function. The folder date is from the earlier screenshot collection; these Haiku 4.5 images were captured during this separate **9 October 2026** evaluation, before the servers were stopped. The in-app browser captured seven images; after its connection failed, a user-approved local Chrome process captured the desktop waitlist and schedule. The phone [waitlist view](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/haiku-4.5-phone-waitlist.png) shows the clipped action column; the [schedule view](/Users/oddbjornmidbo/Source/App-test/seatflow-screenshots-2026-10-08/haiku-4.5-phone-schedule.png) shows the retained generic error banner.

## Comparison note

This run used the **original prompt** and was evaluated separately. Earlier model scores were not recomputed. Against the recorded [Haiku 5.5 result](/Users/oddbjornmidbo/Source/App-test/SeatFlow-Haiku5.5-evaluation-2026-10-08.md), Haiku 4.5's overview adapts better to phone width, but its reproduced final-seat race and incomplete notification/idempotency handling create a much larger correctness gap. The reported cost was **$1.03** versus Haiku 5.5's **$0.41**, with implementation times of **9m 34s** and **9m 53s** respectively; Haiku 4.5's actual wall time was longer due to human acceptance steps.
