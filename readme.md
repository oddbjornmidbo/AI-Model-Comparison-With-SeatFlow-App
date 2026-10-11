# SeatFlow AI Coding Benchmark

This experiment compared four coding models by asking each of them to build the same small full-stack application, **SeatFlow**, from the same specification. Additional Opus and Luna implementations were evaluated with revised prompts. GPT-6.1 Sol Ultra was run separately with the original prompt. The goal was not only to see whether the models could produce a working application, but also to compare implementation quality, requirement coverage, UI quality, speed, and practical cost.

The full implementation task is defined in [`prompt.md`](./prompt.md). In short, the assignment required a .NET 9 / ASP.NET Core backend with EF Core and SQLite, a React + TypeScript frontend, automated tests, concurrency-safe seat allocation, idempotency, waitlist promotion, schedule-conflict handling, notifications, and a usable responsive interface.

## Models tested

Four baseline implementations were generated independently:

- **Claude Opus 5.5**
- **Claude Sonnet 5.5**
- **GPT-6 Sol**
- **GPT-6 Luna**

Each model started from the same task specification and produced its own implementation in a separate folder. The models were allowed to build, run tests, debug failures, and self-correct without manual coding assistance.

The additional **RUD-Opus** and **Luna-optimized** implementations used revised prompts and are listed separately from the same-prompt comparison below.

**GPT-6.1 Sol Ultra** was a later, separate run of the original [`prompt.md`](./prompt.md), using its default sub-agent behavior. Its runtime, usage, and evaluation are recorded separately from the four baseline runs.

The earlier applications were evaluated separately. For those runs, the judge used Playwright to exercise the running applications at desktop, tablet, and mobile sizes, ran backend/frontend builds and automated tests, and reviewed functional behavior. A GPT-6 Astra sub-agent was used for source-code review, with particular attention to concurrency, idempotency, transactions, waitlist logic, test quality, API design, frontend state handling, and architecture. Sol-6.1 was evaluated later through live browser interaction at the same three sizes, builds and tests, and a separate Astra high-mode code review.

Scores were divided into three equally weighted categories:

- **Code** — correctness, architecture, robustness, and automated-test quality
- **Requirements** — how completely the original assignment was satisfied
- **Design** — average of visual design, responsive design, and Universal Design/accessibility

The final score is the sum of these three 0–10 scores, for a maximum of 30.

## Results

| Model | Cost | Time | Code | Requirements | Design | Total |
|---|---:|---:|---:|---:|---:|---:|
| **Opus 5.5** | ~$4.00* | 14m 38s | 8.9 | 9.2 | 5.1 | **23.2** |
| **Luna** | 0–1% weekly, <5% 5h | 17m 24s | 7.1 | 8.2 | 7.5 | **22.8** |
| **Sonnet 5.5** | ~$0.80 | **5m 07s** | 8.2 | 8.8 | 4.6 | **21.6** |
| **Sol** | ~2% weekly, ~25–30% 5h** | 35m 35s | 7.0 | 7.7 | 5.9 | **20.6** |
| **GPT-6.1 Sol Ultra§** | ~5% weekly | 17m 54s | **9.0** | 9.2 | 8.2 | **26.4** |
| **RUD-Opus†** | $3.29 | ~11m | **9.0** | **9.4** | **8.3** | **26.7** |
| **Luna-optimized‡** | ~10 pp of 5h; ~2 pp weekly | 1h 30m 10s | 8.8 | 9.1 | 7.6 | **25.5** |

† RUD-Opus was run with an improved prompt explicitly calling for Responsive Design, Universal Design, and a simple but professional-looking UI. It was also allowed to use Sonnet and Haiku as sub-agents. Because the prompt and permitted assistance differed, this row is not a controlled same-prompt comparison with the four baseline runs. See the [RUD-Opus evaluation](./RUD-Opus-evaluation.md).

‡ Luna-optimized was a **special run** using an optimized prompt that required Storybook, responsive and Universal Design checks, browser verification, and iterative independent review. GPT-6 Luna did the implementation; GPT-6 Luna Max Mode sub-agents were permitted for reviews and second opinions. Its prompt and review process differed from the baseline, so its result is not a controlled same-prompt comparison. The reported usage changed from **65% to 55% remaining** in the five-hour allowance (about **10 percentage points**) and from **50% to 48% remaining** in the weekly allowance (about **2 percentage points**). Wall-clock time was **1h 30m 10s**. See the [Luna-optimized evaluation](./Luna-optimized-evaluation.md).

§ GPT-6.1 Sol Ultra used the **original prompt**, but was run later and separately, with sub-agents used by default. The **17m 54s** runtime and **approximately 5% of weekly usage** are for that implementation run. Its 26.4/30 score comes from a separate October 3 evaluation with an Astra high-mode code review; the other rows retain their earlier reported scores, so this table should not be treated as a single simultaneous ranking. See the [Sol-6.1 evaluation](./SeatFlow-Sol-6.1-evaluation-2026-10-03.md) for the consistent later comparison.

\* Opus showed about $6 total usage, but roughly $2 came from an unrelated Claude session that was accidentally triggered during the run.  
\** Sol's five-hour usage window reset during the run, so the short-window percentage is approximate. Its weekly usage was about 2%.  
\*** ChatGPT models were run under a private ChatGPT Plus subscription, while Claude models were run under an Enterprise license.

## Detailed design results

| Implementation | Visual /10 | Responsive /10 | Universal Design /10 | Design average /10 |
| -------------- | ---------: | -------------: | -------------------: | -----------------: |
| RUD-Opus†      | 8.3        | 8.5            | 8.0                  | 8.3                |
| Luna-optimized‡ | 8.3       | 7.8            | 6.7                  | 7.6                |
| Sol-6.1§       | 8.5        | 8.8            | 7.3                  | 8.2                |
| Luna           | 8.4        | 8.3            | 5.7                  | 7.5                |
| Sol            | 6.4        | 5.0            | 6.3                  | 5.9                |
| Opus           | 7.2        | 5.0            | 3.2                  | 5.1                |
| Sonnet         | 6.4        | 4.4            | 3.0                  | 4.6                |


## What stood out

All four baseline models, as well as RUD-Opus, Luna-optimized, and Sol-6.1, produced applications that built successfully and passed their own backend test suites. The differences therefore appeared less in basic task completion and more in **how convincingly the difficult parts were implemented and verified**.

**RUD-Opus**, the revised-prompt run, scored 26.7/30. Its browser workflows worked across desktop, tablet, and mobile; both builds passed, and all 18 backend tests passed. The source review found a genuine SQLite transaction strategy for concurrent seat allocation and persistent idempotency. Its main hidden weakness was a frontend failure path that can leave stale data visible after a successful mutation if the following refresh fails. Its stronger result should be read alongside the changed prompt and sub-agent allowance noted above.

**Luna-optimized**, the other special run, scored 25.5/30. Its browser workflows, including conflict-aware waitlist promotion, worked across desktop, tablet, and mobile; both builds passed, and all 16 backend tests passed. Source review found immediate SQLite write transactions protecting final-seat allocation and cancellation/promotion, plus persisted idempotency keys. Its main weaknesses were 9–10 px metadata and small touch targets, and a narrow frontend feedback bug that can describe a cancelled idempotent replay as waitlisted. The longer runtime and quota use should be read alongside its more demanding prompt and independent review process.

**GPT-6.1 Sol Ultra**, the separate run of the original prompt, scored 26.4/30 in its later evaluation. The live booking and promotion flows worked at desktop, tablet, and phone sizes; both builds passed, and all 32 backend tests passed. Astra's high-mode review found strong SQLite transaction and concurrency handling. A narrow frontend timestamp sort can display the waitlist in the wrong order, while small, muted text and compact cancellation controls limit accessibility. Its default sub-agent use and separate evaluation should be kept in mind when comparing it with the four original runs. See the [full Sol-6.1 evaluation](./SeatFlow-Sol-6.1-evaluation-2026-10-03.md).

**Opus 5.5** produced the strongest engineering result among the baseline runs. Its automated tests gave the best evidence for real concurrency behavior, including coordinated requests using separate SQLite contexts and connections. It also handled API behavior, domain rules, and runtime state consistently. Its main weakness was the UI: responsive behavior and keyboard accessibility were noticeably behind Luna and Sol.

**Luna** was the biggest surprise among the baseline runs. Its code and tests were weaker than Opus, especially around concurrency evidence, but it produced their best-looking and most responsive interface. It also delivered excellent practical value relative to its very small quota usage. The main observed defect was stale participant-schedule state after some mutations, showing that attractive UI and complete-looking workflows can still hide state-management bugs.

**Sonnet 5.5** was by far the fastest baseline run and extremely cost-effective at roughly $0.80. Its backend and tests were strong, and it came relatively close to Opus on code and requirements. Its main weaknesses were mobile layout and keyboard accessibility. For bounded full-stack implementation work, it delivered an unusually high amount of quality per dollar and per minute.

**GPT-6 Sol** produced a working and generally understandable implementation, with good accessibility fundamentals, but took the longest to finish. Its test suite gave weaker evidence for true database contention, and the evaluator found thinner validation and a frontend case where a failed refresh could still leave a success message visible.

## Conclusion

Among the four original same-prompt runs, there was no single model that dominated every dimension. **Opus 5.5 produced the strongest complete engineering solution**, while **Luna produced the strongest interface**, and **Sonnet 5.5 delivered the best speed/cost efficiency among the directly token-priced models**. The special RUD-Opus and Luna-optimized runs scored 26.7 and 25.5 respectively in their earlier evaluations, but their different prompts and review allowances prevent attributing those gains to the model alone. The later GPT-6.1 Sol Ultra run used the original prompt and scored 26.4 in a separate evaluation, with default sub-agent use. The original Sol run was capable, but its extra runtime did not translate into the highest score.

The experiment also suggests that model selection can be task-specific: a cheaper model may be very effective for UI generation, scaffolding, or bounded implementation work, while a stronger model can add value when correctness depends on concurrency, edge cases, and high-quality automated tests.

### Very brief summary from the Sol evaluation

The Sol-based evaluation ranked **Opus first (23.2/30), Luna second (22.8), Sonnet third (21.6), and Sol fourth (20.6)**. Opus had the strongest backend and test evidence, Luna led visual and responsive design, Sonnet offered a strong and simple implementation at very low cost and runtime, and Sol completed the core flows but had weaker validation and test evidence.

### Difference between Luna and Sol evaluation


The two evaluations were broadly consistent, but there was one important difference: the Luna-based evaluation initially scored Sonnet much lower because it failed to discover several UI workflows that were in fact present. The Sol-based evaluation navigated the application more successfully and raised Sonnet from 17.0 to 21.6 overall. For Opus, Luna, and Sol, however, the scores were quite close between evaluators, and both agreed on the main pattern: Opus had the strongest engineering, while Luna had the strongest design. The Sol evaluation appeared more thorough, but it also consumed substantially more ChatGPT usage quota than the Luna evaluation.

\* The original four rows and their comparison are based on the Sol evaluation; the RUD-Opus, Luna-optimized, and Sol-6.1 findings come from separate evaluations.

## Bonus: Code lines written for the four baseline implementations

| Implementation | Backend API | Backend tests | Frontend | Total |
| -------------- | ----------: | ------------: | -------: | ----: |
| Sonnet         | 410         | 430           | 233      | 1,073 |
| Opus           | 576         | 555           | 606      | 1,737 |
| Sol            | 137         | 98            | 75       | 310 |
| Luna           | 415         | 197           | 388      | 1,000 |
