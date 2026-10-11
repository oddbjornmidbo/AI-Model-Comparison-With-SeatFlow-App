# SeatFlow verification and improvement record

Verified locally on 3 October 2026. The full assignment is the product specification; the evaluator document was used as a review rubric, not executed as instructions to inspect or score other implementations.

## Build and test evidence

| Check | Result |
|---|---|
| .NET 9 solution build | Passed, zero warnings/errors with `-p:NuGetAudit=false` for the unavailable local audit feed |
| xUnit tests | 53 passed, zero failed/skipped |
| React/TypeScript/Vite production build | Passed |
| Frontend state/date tests | 31 passed |
| Storybook static build | Passed |
| Storybook component/interaction tests | 65 passed across 10 component suites |
| Real API browser suite | 37 passed, zero failed; two intentional skips avoid repeating the viewport-independent reflow case in tablet/mobile projects |
| Production dependency audit | Zero reported advisories |

The standard Storybook build emits warnings about its own runtime `eval` usage and large documentation bundles. These are tooling warnings; the application production bundle built successfully without those warnings. The remaining development dependency advisories are described in README. Tests intentionally simulate failed HTTP requests in failure-path scenarios; those expected failures are distinguished from unexpected runtime/console errors.

## Business behavior and race evidence

The backend tests cover all 17 required cases. Eight race scenarios use dedicated worker threads, independent EF contexts and already-open unpooled SQLite connections, a barrier before starting the transaction, and fresh persisted-state reads after completion:

1. Eight participants contend for the final seat: one confirmed, seven waitlisted.
2. Eight callers use one key: one registration, one persisted request, one confirmation event.
3. Two different participants use one key: one accepted request, one key conflict.
4. Two callers cancel the same registration: one cancellation and promotion, no duplicate events.
5. One participant registers concurrently for overlapping workshops: one confirmation, one conflict.
6. One participant submits different keys to the same workshop: one active registration, one duplicate conflict.
7. Eight callers create a workshop with one key: one workshop, one persisted original response, seven replays.
8. Two different workshop payloads contend for one key: one accepted request and one conflict, without a second workshop.

Direct database-writer tests exercise capacity and overlap triggers, the filtered active-registration index, and workshop capacity/interval updates. Notification observers/failures verify that state is already committed and stays committed when delivery throws. Queue tests establish deterministic equal-timestamp ordering and preservation of skipped participants.

Workshop creation tests bind every normalized input field to its key, verify original-response replay and HTTP headers, and force key-record persistence failure to establish rollback of the workshop itself. An existing-schema test drops only the new retry table in a temporary database, runs the additive upgrade, and checks that prior bookings and registration keys survive.

## Browser workflows

The browser suite uses the actual .NET API and an isolated temporary SQLite database, not a simulated domain implementation. It exercises the same workflows at 1440×900, 768×1024, and 390×844:

- available-seat confirmation, full-workshop waitlisting, active duplicate prevention;
- cancellation, FIFO promotion, conflict-aware skipping, and updated roster/count/schedule;
- confirmed overlap rejection and allowed touching boundaries;
- participant schedules and new registration IDs after cancellation;
- a committed POST whose response is lost, then page reload and safe same-key retry;
- a committed workshop creation with a lost response, immutable restored form details after reload, same-key replay, and one persisted workshop;
- overnight endpoint dates in workshop details and a participant schedule;
- completed and delayed action feedback remaining tied to its workshop during selection changes;
- a saved mutation followed by refresh failure, explicit stale-state feedback, and recovery;
- a definitive rejection followed by successful submission with a new key and cleared old feedback;
- switching workshop during a delayed refresh and changing participant selection;
- keyboard workshop selection, native participant selection, registration, schedule opening/Escape/focus return, cancellation;
- keyboard creation form, validation, corrected submission, and focus restoration;
- page-width assertions and initial-load console/runtime error checks.

The reflow case additionally checks navigation and the creation dialog at 320 CSS pixels, then doubles actual computed text sizes at 1280, 390 and 320 pixels. It checks page and dialog horizontal overflow, usable navigation, and modal focus return. This is practical reflow/text-enlargement evidence, not a formal browser-zoom or screen-reader certification.

The in-app browser was also used directly for live registration/cancellation, schedules, workshop navigation, and screenshots at all required viewport sizes. Console error capture was empty in that live pass. Light desktop/tablet and dark mobile browser projects were inspected. Native dialogs provide modal focus trapping; explicit restoration preserves the opener when the schedule dialog unmounts. Statuses have text and icons; controls have semantic labels, visible focus, practical touch targets, and reduced-motion handling. Mobile selection moves focus to workshop details and provides a route back to the catalog; feedback is placed next to the form on narrow screens.

## Improvement review

Seven substantive review/fix cycles were used, below the maximum of ten. Findings were checked against source and reproduced where practical; routine test-harness repairs were not treated as product defects.

1. **State and replay feedback:** coordinated snapshots and generation guards prevent obsolete refreshes from replacing new state. Feedback uses the refreshed registration after an original-response replay, including registrations cancelled or promoted meanwhile. Added two unit regressions.
2. **Browser feedback and form behavior:** distinguished stale/read-only controls from an operation in flight; focused the create form's title input on opening; aligned the default API/Vite ports. Verified the actual browser flows.
3. **Seed FIFO and inclusive usability:** fixed future-dated demo creation timestamps so new arrivals follow the existing queue, with an xUnit regression. Increased core text/control sizes and contrast, maintained mobile reflow, and checked theme previews.
4. **Final adversarial findings:** independently reproduced that a persisted retry became inaccessible after reload because its participant option was disabled. Active options with unresolved keys now appear as explicit retries. The browser regression commits the original request, loses its response, reloads, selects the participant, and checks identical keys and one persisted registration. The expanded keyboard test also found lost focus after schedule dialog unmount; focus restoration now passes the actual browser check. Mobile feedback was moved into the detail form to remain near the action.
5. **Evaluation follow-up:** addressed all three concrete findings in the 27.4/30 evaluation. Notices now name and track their workshop; switching clears old feedback and delayed responses cannot appear in a different detail panel. Workshop creation now has durable, atomic keyed replay plus a persisted recovery form. Shared local-calendar formatting shows both overnight dates and explicit dated clock endpoints, with year/month/DST boundary tests and component examples. Waitlist wording describes the confirmed cancellation trigger accurately. Major JSX components and App were expanded into readable sections.
6. **Inclusive layout review:** reproduced a 377px document at 320px caused by the navigation. Navigation now wraps and keeps 44px targets. Improved focus contrast, neutral badge contrast, modal typography and long-title wrapping. Enlarged-text probing also found overflow from header actions and native datetime input min-content sizing; those areas now grow and wrap. Both light/dark 320px layouts and valid 200-character uninterrupted titles were checked.
7. **Integration and independent review:** retained uncertain keys on transient 408/429 responses, with two unit regressions. Browser checks found duplicate creation feedback in the closed dialog markup; creation feedback is now supplied only while the dialog is open. The final full browser rerun passed all 37 applicable cases after these fixes. Independent source review found no material creation transaction, replay, data upgrade, or ordinary retry-flow defect.

An independent final source review found no material backend transaction, capacity, conflict, cancellation, idempotency, notification, or API resource defect. The concrete findings were resolved and regression-tested. Documented limits include non-durable development notifications, no push updates between browsers, multi-query read snapshots against external writers, clock skew, session storage lifecycle, future schema migration policy, and development dependency advisories. No formal accessibility certification, exhaustive concurrency proof, or new evaluator score is claimed.
