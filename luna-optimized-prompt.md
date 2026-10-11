# Build a complete small application called SeatFlow.

The goal is to test your ability to design, implement, test, review, and iteratively improve a realistic full-stack feature.

Work autonomously. Do not ask clarifying questions unless you are truly blocked. Make reasonable assumptions and document them briefly in README.md.

You are responsible for the complete implementation.

Use only GPT-6 Luna for implementation work.

You may delegate independent code reviews and second opinions to GPT-6 Luna Max Mode sub-agents as described in the review and iteration process below.

---

# Tech stack

## Backend

- .NET 9
- ASP.NET Core Web API
- EF Core
- SQLite
- xUnit

## Frontend

- React
- TypeScript
- Vite
- Storybook

Do not use Docker, authentication, cloud services, or external databases.

---

# The application

SeatFlow manages workshop registrations for a small conference.

A workshop has:

- id
- title
- start time
- end time
- capacity

A participant has:

- id
- name
- email

A registration has:

- id
- workshop id
- participant id
- status: Confirmed, Waitlisted, or Cancelled
- created timestamp

---

# Business rules

1. A participant may register for a workshop only once unless their previous registration was cancelled.

2. If the workshop has available capacity, a new registration becomes Confirmed.

3. If the workshop is full, the registration becomes Waitlisted.

4. Waitlist ordering is FIFO by registration creation time.

5. When a confirmed registration is cancelled, the oldest eligible waitlisted registration must automatically be promoted to Confirmed.

6. A participant must never have two Confirmed workshops whose time ranges overlap.

7. When promoting somebody from a waitlist:
   - skip participants who would have a schedule conflict
   - leave skipped registrations on the waitlist
   - promote the first eligible participant

8. Registration creation must support an `Idempotency-Key` HTTP header.

   Repeating the same request with the same key must return the same registration and must not create duplicates.

9. Capacity must be safe under concurrent registration attempts.

   If two users attempt to take the final available seat at the same time, only one may become Confirmed.

10. Cancelling an already cancelled registration must be idempotent.

---

# Integration boundary

Create an `INotificationService` abstraction.

Notifications should be sent when:

- a registration becomes Confirmed
- a waitlisted participant is promoted
- a registration is cancelled

Provide a simple development implementation that records notifications in memory or logs them.

Notification failures must NOT roll back a successful registration or cancellation.

Do not expose development notification logs or other internal diagnostic information in the user-facing UI unless it is necessary for the stated requirements.

---

# API

Implement at least:

- `GET /api/workshops`
- `POST /api/workshops`
- `GET /api/workshops/{id}/registrations`
- `POST /api/workshops/{id}/registrations`
- `POST /api/registrations/{id}/cancel`
- `GET /api/participants/{id}/schedule`

Use sensible HTTP status codes and validation responses.

Created-resource URLs must resolve to meaningful routes if a `Location` header is returned.

Validate required workshop fields, including start and end times.

---

# Frontend

Create a UI where I can:

- see all workshops and remaining capacity
- select a workshop and see confirmed registrations and the waitlist
- register an existing participant
- cancel a registration
- see a participant's schedule
- clearly distinguish Confirmed, Waitlisted, and Cancelled states

The UI does not need to be fancy, but it must work and be professional, intuitive to use and use modern design standards.

The UI should be responsive and follow basic Universal Design/accessibility principles, including keyboard usability, labelled controls, readable contrast, and mobile reflow.

Use the user's system light/dark preference where practical rather than assuming a fixed theme.

Important actions must remain usable at desktop, tablet, and mobile widths.

Avoid horizontal document overflow on normal mobile widths.

Status must not be communicated by color alone.

Interactive controls must have useful accessible names, visible focus, and reasonable touch targets.

All primary workflows must be operable with a keyboard.

Where the UI already knows that an action is invalid, prefer preventing or clearly disabling that action rather than inviting an avoidable error. For example, a participant already actively registered for a workshop should not appear to be normally registerable again.

Do not hide legitimate backend validation. The server must still enforce all rules independently of the UI.

---

# Frontend state correctness

Treat frontend state consistency as a correctness requirement.

After every successful mutation, identify every visible view derived from the changed data and ensure it is updated or invalidated appropriately.

Examples include:

- workshop capacity
- confirmed registration list
- waitlist
- cancelled registrations
- participant schedule
- status messages

A user must not need to reselect a workshop or participant, or manually refresh the page, to see the correct state after a successful mutation.

Before and after mutations:

- clear obsolete error or success messages appropriately
- do not show an old error after a later successful action
- do not replace a refresh failure with a misleading success state
- do not silently keep stale data if a post-mutation refresh fails
- guard against older asynchronous requests overwriting state for a newer selection

If a registration POST may need to be retried after a lost response, preserve the idempotency key for that logical attempt rather than generating a new key merely because the user retries the same operation.

---

# Storybook

Set up Storybook for the frontend.

Create a Storybook story for every user-facing React component.

Cover relevant component states, including where applicable:

- normal/default state
- empty state
- loading state
- error state
- Confirmed
- Waitlisted
- Cancelled
- full workshop
- available workshop
- disabled/unavailable actions
- validation feedback
- mobile/narrow presentation
- light and dark presentation where applicable

Use Storybook actively during implementation to inspect components rather than treating stories as documentation added only at the end.

Use Storybook interaction/accessibility testing where practical.

Storybook does not replace testing the complete running application.

Do not create unnecessary components or an elaborate design system merely to increase Storybook coverage.

---

# Seed data

Seed:

- at least 4 workshops
- at least 5 participants
- at least two partially overlapping workshops

Use seed data that makes the important workflows reasonably easy to demonstrate.

---

# Automated tests

Write meaningful automated tests.

At minimum cover:

- registering into an available workshop
- registering into a full workshop
- FIFO promotion after cancellation
- skipping an ineligible waitlisted participant because of a schedule conflict
- prevention of overlapping confirmed registrations
- duplicate registration prevention
- idempotency-key behavior
- cancelling twice
- concurrent attempts for the final seat

Also cover important boundary and failure behavior where practical, including:

- workshops whose times touch exactly but do not overlap
- notification failure not rolling back committed state
- concurrent requests using the same idempotency key
- persisted state after concurrent operations
- cancellation followed by waitlist promotion

Prefer testing business behavior rather than implementation details.

---

# Concurrency test quality

A test named "concurrent" is not sufficient evidence by itself.

Tests intended to prove concurrency behavior should create genuine contention where practical.

For SQLite/database concurrency tests:

- use independent callers
- use independent DbContexts/connections where appropriate
- use a real temporary/file-backed SQLite database where appropriate
- synchronize competing operations with a barrier, gate, start signal, or equivalent when practical
- reread persisted state after operations complete
- verify the resulting database state, not only returned objects
- ensure the test would plausibly fail if the implementation were not concurrency-safe

Do not rely solely on two async method calls passed to `Task.WhenAll` if they may execute sequentially.

Do not rely solely on an in-process semaphore as evidence that database-level correctness is preserved across independent requests.

---

# Architecture

Keep the solution reasonably simple.

Avoid unnecessary abstractions and enterprise-style boilerplate.

The domain logic should not live entirely in API controllers.

Use database constraints/indexes where they provide useful protection for important invariants.

Keep transaction boundaries explicit and understandable.

Registration, idempotency persistence, capacity decisions, cancellation, and promotion must remain atomic where required.

Notifications must occur outside the domain transaction in a way that cannot roll back an already committed domain operation.

Avoid fallible post-commit work that can incorrectly make the client believe a committed operation failed, unless that behavior is deliberately handled.

Generated code should compile without warnings that indicate obvious problems.

Do not over-engineer the solution.

---

# README

Add a README containing:

- short introduction to SeatFlow
- how to run the backend
- how to run the frontend
- how to run Storybook
- how to run the tests
- architecture summary
- API endpoint overview
- important assumptions
- concurrency/idempotency approach
- any known limitations

Keep it useful and concise.

---

# Luna Max Mode review and iteration process

The implementation must go through independent review before completion.

You are the primary GPT-6 Luna implementation agent.

Use GPT-6 Luna Max Mode sub-agents only for independent code review, adversarial review, and second opinions.

Do not delegate the main implementation to a sub-agent.

Sub-agents should preferably review rather than edit files directly. You should make the final changes yourself so that architectural ownership remains coherent.

## Review independence

When asking a Luna Max Mode reviewer for an opinion:

- give it the original requirements
- give it access to the relevant source code
- give it build/test results when available
- do not tell it that the implementation is already considered correct
- do not ask it merely to confirm your approach
- ask it to actively look for counterexamples and hidden failures

Treat reviewer findings as hypotheses to verify, not unquestionable instructions.

---

# Required review perspectives

Before declaring the implementation finished, obtain independent Luna Max Mode reviews covering the following perspectives.

## 1. Backend correctness review

Ask a reviewer to inspect:

- business-rule correctness
- transaction boundaries
- concurrent final-seat safety
- cross-workshop schedule conflicts
- FIFO promotion
- conflict skipping
- idempotency
- duplicate prevention
- database constraints
- cancellation behavior
- notification failure isolation
- API validation and status codes
- whether concurrency tests actually prove what they claim

The reviewer should identify:

- confirmed defects
- likely risks
- missing test evidence
- unnecessary complexity
- specific recommendations

## 2. Frontend/state review

Ask an independent reviewer to inspect:

- mutation flows
- stale state
- request races
- error-state clearing
- success-state correctness
- registration retries/idempotency-key handling
- workshop switching
- participant switching
- schedule updates
- duplicate-registration UX
- loading and failure paths
- type safety
- component structure

It should specifically look for cases where:

- a successful write is followed by stale UI
- an old request can overwrite a newer selection
- a failed refresh can be masked by a success message
- an earlier error remains visible after a later success

## 3. UX / Responsive / Universal Design review

Ask an independent reviewer to inspect:

- visual hierarchy
- professional appearance
- intuitive workflow
- keyboard navigation
- labels and accessible names
- visible focus
- semantic HTML
- status communication
- contrast
- touch-target sizing
- desktop layout
- tablet reflow
- mobile reflow
- horizontal overflow
- light/dark theme behavior
- Storybook coverage

The reviewer should distinguish visual preference from actual usability/accessibility defects.

## 4. Test-quality review

Ask an independent reviewer to inspect the automated tests adversarially.

It should answer:

- Which requirements are convincingly proven?
- Which tests can pass without proving their stated behavior?
- Is concurrency genuine?
- Are persisted results reread and verified?
- Are important failure paths missing?
- Are tests overly coupled to implementation details?
- Are frontend/Storybook checks sufficient for the UI risks?

---

# Iterative improvement loop

After the initial implementation and verification, perform a review/improvement loop.

One full iteration consists of:

1. Run builds and relevant tests.
2. Exercise the important application workflows.
3. Obtain one or more targeted Luna Max Mode reviews.
4. Triage every finding.
5. Verify valid findings against code/runtime behavior.
6. Fix confirmed problems and worthwhile risks.
7. Add or improve tests when the finding exposes weak evidence.
8. Rerun affected builds/tests and UI checks.
9. Reassess whether another iteration is likely to materially improve the result.

Continue iterating until you are satisfied that:

- all requirements are implemented
- builds pass
- tests pass
- the difficult backend properties have credible evidence
- normal frontend workflows remain synchronized
- important failure paths are handled honestly
- the UI is professional and responsive
- keyboard accessibility and basic Universal Design requirements are satisfied
- independent reviewers no longer identify material unresolved defects

Perform at most **10 full review/improvement iterations**.

Do not perform iterations merely to reach 10.

Stop earlier when additional review produces only minor stylistic preferences or low-value suggestions.

If a reviewer repeats an issue already investigated and rejected, do not churn the implementation without new evidence.

---

# Final adversarial review

Before finishing, perform one final independent Luna Max Mode review of the finished repository.

Ask the reviewer:

> Assume this implementation is about to be submitted for a strict engineering benchmark. Try to find reasons it should lose points. Focus on concrete correctness defects, missing requirements, misleading tests, stale frontend state, concurrency problems, accessibility failures, responsive defects, unnecessary features, and undocumented limitations. Do not praise the implementation unless needed for context.

Review all material findings.

Fix any remaining high- or medium-impact defect that can reasonably be corrected without destabilizing the solution.

Rerun the relevant verification after final fixes.

---

# Browser verification

Do not rely only on source review and automated tests.

Before finishing, exercise the running application through its actual user workflows.

Verify at least:

- registration into an available workshop
- registration into a full workshop
- waitlisting
- cancellation
- promotion
- duplicate registration attempt
- overlap rejection
- participant schedule updates
- successful action after a previous failed action
- switching workshops
- switching participants
- state after cancellation
- state after promotion

Check desktop, tablet, and mobile layouts.

Check the main workflows with keyboard navigation.

Inspect for browser console errors and failed requests.

Verify that mutation results are reflected consistently in every visible panel.

---

# Before finishing

1. Build the backend.
2. Run the complete backend test suite.
3. Build the frontend.
4. Build/test Storybook.
5. Run any frontend lint/type/test checks that exist.
6. Exercise the application in the browser.
7. Verify desktop, tablet, and mobile layouts.
8. Verify primary keyboard workflows.
9. Complete the Luna Max Mode review/iteration process.
10. Complete the final adversarial review.
11. Fix remaining material findings.
12. Rerun relevant builds and tests.
13. Update README.md.
14. Leave the repository in a clean, runnable state.

---

# Do not spend time on

- authentication
- authorization
- Docker
- CI/CD
- deployment
- elaborate animation
- production observability
- unrelated features
- enterprise-style abstractions
- developer/debug UI that was not requested

---

# Completion standard

Do not stop merely because the application builds or the tests are green.

A passing test suite is evidence, not proof.

Do not claim that a behavior is race-safe, idempotent, responsive, accessible, or correctly synchronized unless the implementation and verification actually support that claim.

Prefer fixing root causes over adding workarounds.

Prefer simple, explicit correctness over cleverness.

Prefer useful user-facing behavior over unnecessary features.

Keep commentary concise while working. Spend tokens primarily on implementation, testing, independent review, browser verification, and fixing real issues.

Stop when the solution is genuinely ready for evaluation, or after 10 full review/improvement iterations, whichever comes first.