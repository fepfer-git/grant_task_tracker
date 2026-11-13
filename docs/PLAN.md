# Build plan

**Status: Approved (revision 2) on 2026-11-12. In progress: Parts 0 to 7 reviewed; Part 8 built, waiting for review.**

Revision 2 adds the decisions from 2026-11-12 (see "Decisions"), the Swagger UI part (Part 9), the date-storage tests, and a fake clock that catches an accidental switch to UTC.

This plan builds what `docs/DESIGN.md` describes, in small parts that can each be reviewed on their own. Section references (§) point to `docs/DESIGN.md`.

Each part has:
- **Builds:** what the part adds.
- **Done when:** the conditions for marking it Built, on top of the common definition of done below.
- **Tests:** each test case and the rule (BR), need (OP) or design section it covers.
- **Mutation checks:** the code that will be broken on purpose, by hand, to prove the tests catch it.
- **Build record:** filled in when the part is built. It records what was built, the final test list, the mutation results, and any change from this plan.

## Common definition of done

A part is Built only when all of these are true:

1. The solution builds with no warnings. Warnings are treated as errors.
2. `npm test` at the repository root passes, and passes again on a second run. From Part 1 onward, it runs every test written so far.
3. Every new test follows the conventions below.
4. Every planned mutation check has been done by hand: the code was broken on purpose, the named test failed, and the code was restored. The result is recorded in the build record.
5. If anything differs from `docs/DESIGN.md`, it was agreed with you first and `docs/DESIGN.md` was updated.
6. Nothing is committed. You review and commit. I recommend one commit per part, so the history shows how the work progressed.

## Test conventions

| Convention | Rule |
|---|---|
| Structure | Arrange / Act / Assert, marked with `// Arrange`, `// Act`, `// Assert` comments |
| .NET names | `Given_<State>_When_<Action>_Then_<Outcome>`, for example `Given_ClosedGrant_When_AddingTask_Then_ThrowsBr1` |
| Vitest names | `given <state>, when <action>, then <outcome>` |
| Naming the subject | Name the entity a value belongs to, so the name can't be read two ways: `TaskTitle`, not `Title`; `TaskDueDate` is not needed because only tasks have a due date. A field key in the outcome (`ThrowsValidationOnTitle`, `KeyedOnTitle`) is the API's field name and stays as it is. |
| One behaviour per test | Boundaries of one rule share a `[Theory]` with `[InlineData]`. Different rules get different tests. |
| Time | Never the real clock. Domain tests pass fixed dates (`today` = 2026-03-11). API tests use `FakeTimeProvider` pinned to **`2026-03-10T22:00:00Z`**, which is **2026-03-11 09:00 in Sydney**. The UTC date and the Sydney date differ, so an accidental switch to the UTC date fails the tests (§9). |
| Expected values | Dates and instants are written as **literals** in the test (for example `"2026-03-11"`, `"2025-10-16T22:00:00Z"`), never computed with the code under test. Otherwise a bug in the date logic would shift the expected and the actual value together, and the test would still pass. |
| UI time zone | Vitest runs with the time zone set to `America/Los_Angeles`, so treating a `"YYYY-MM-DD"` value as a JavaScript `Date` shows the wrong day and fails. |
| Isolation | Each test creates its own data. API tests get their own app and database. No shared mutable state, so tests run in any order and in parallel. |
| Assertions | xUnit's built-in `Assert`. Assert the outcome the rule cares about (status code, ProblemDetails `code`, field key, timestamps), not incidental details. |

**Which level tests what.** The domain tests cover every rule and every boundary. API tests check that each outcome reaches the client correctly (status code, ProblemDetails `code`, field key), with one test per outcome rather than repeating every boundary. UI tests cover only the few behaviours in §9.

**Tools:**

| Purpose | Tool |
|---|---|
| .NET tests | xUnit v3 (3.x line, run through VSTest by `dotnet test`), with its built-in `Assert`. See Decision 11. |
| Fake clock | `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) |
| API tests | `WebApplicationFactory` (`Microsoft.AspNetCore.Mvc.Testing`) with SQLite in-memory |
| UI tests | Vitest, React Testing Library, `@testing-library/user-event`, jsdom |

## Prerequisites

| Tool | Status on this machine |
|---|---|
| .NET SDK 10 | Installed (10.0.403) |
| Node.js | Installed (26.13.1, npm 11.20.0) |

## Coverage map

Where each rule and need is tested, by part. BR and OP texts are quoted in full from `docs/CONTEXT.md`. §4.7 and §3.6 are our own design rules, summarised from `docs/DESIGN.md`.

| Rule / need | Domain | Infrastructure | API | UI |
|---|---|---|---|---|
| **BR-1:** A task cannot be added to a closed grant. | Part 1 | | Part 6 | Part 11 |
| **BR-2:** A new task's due date cannot be in the past, and cannot be before the grant's application date. | Part 1 | Part 4 (date comparison in SQL) | Parts 3, 6 | Part 11 |
| **BR-3:** The task title is required and must be 100 characters or fewer. | Part 1 | | Part 6 | Part 11 |
| **BR-4:** Completing a task records the date it was completed. Completing a task that is already completed must not change that date. Decide what the API returns in that case and explain why. | Part 2 | | Part 6 | Part 11 |
| **BR-5:** A grant cannot be closed while it has any pending High-priority task. | Part 2 | | Part 6 | Part 12 |
| **BR-6:** A task is overdue when it hasn't been completed and its due date is before today. "Today" must be testable, so don't call `DateTime.Now` directly inside the business logic. | Part 2 | Part 4 | Parts 3, 5 | Part 11 |
| **§4.7 (design):** Once a grant is closed, neither the grant nor its tasks can change. Adding a task returns 409 `BR-1`, completing a pending or cancelled task returns 409 `grant-closed`, and repeats of completed actions return 200. | Part 2 | | Part 6 | Parts 11, 12 |
| **§3.6 (design):** Calendar dates (`ApplicationDate`, `DueDate`) are stored as `'YYYY-MM-DD'` and never converted to or from UTC. Instants are always UTC: a converter marks them UTC on read and refuses non-UTC values on write, so the API always sends them with `Z`. | | Part 4 | Part 5 | Parts 10, 11 |
| **OP-2:** Up to 100 officers use the tool at the same time across all offices, and each grant is usually handled by a small team of officers rather than one person. The business rules in §2.2 must hold at all times. | | Part 4 (version checks) | Part 7 (retry) | |
| **OP-3:** Internet connections in regional offices are unreliable, so the same request may be sent more than once. Repeating a request must be safe, and the screen must tell officers clearly when the server can't be reached, rather than failing silently. | | | Part 7 | Parts 10, 11 |
| **OP-5:** The operations team needs an automated way to tell whether each running copy of the API is working and can reach its data, so they can stop sending officers to a copy that isn't. | | | Part 8 | |

## Parts

| Part | Title | Status |
|---|---|---|
| 0 | Repository scaffolding | Built, reviewed |
| 1 | Domain: creating a task (BR-1, BR-2, BR-3) | Built, reviewed |
| 2 | Domain: complete, close, status, overdue (BR-4, BR-5, BR-6, §4.7) | Built, reviewed |
| 3 | Clock: "today" in Sydney | Built, reviewed |
| 4 | Infrastructure: database, dates, versions, seed data | Built, reviewed |
| 5 | API: read endpoints, errors, start-up | Built, reviewed |
| 6 | API: write endpoints (BR-1 to BR-5 through the API) | Built, reviewed |
| 7 | OP-2 retry and OP-3 safe task creation | Built, reviewed |
| 8 | OP-5 health checks | Built, waiting for review |
| 9 | Swagger UI (Development only) | Not started |
| 10 | UI foundation: app, API client, one test command | Not started |
| 11 | UI: grant screen (tasks, Complete, Add task) | Not started |
| 12 | UI: close a grant | Not started |
| 13 | Documentation: README and testing strategy | Not started |

Parts 0 to 8, 10 and 11 cover what the spec requires. Parts 9 and 12 are spec extras that are in our design. Part 12 comes last among the code parts and can be dropped if time runs short.

---

### Part 0: Repository scaffolding

**Builds:**
- The solution file and four projects, matching §2: `GrantTracker.Api` (controllers), `GrantTracker.Domain`, `GrantTracker.Infrastructure` and `GrantTracker.Tests`. Project references follow §2: Api → Domain and Infrastructure, Infrastructure → Domain, and Domain → nothing.
- `Directory.Build.props`: `net10.0`, nullable reference types on, warnings as errors.
- `.gitignore` additions: `bin/`, `obj/`, `node_modules/`, `*.db`.
- A local tool manifest with `dotnet-ef`, for migrations.
- A root `package.json` whose `test` script runs `dotnet test`. The UI tests are added to it in Part 10.

**Done when:**
- `dotnet build` succeeds with no warnings.
- The project references match §2. Domain references nothing.
- There are no tests yet, so the `npm test` wiring is proven in Part 1.

**Tests:** none.

**Mutation checks:** none.

**Build record:** Built on 2026-11-12.

What was built:

| File | What it holds |
|---|---|
| `GrantTracker.slnx` | The solution, with folders `src/` and `tests/`. The .NET 10 SDK creates the XML `.slnx` format by default. |
| `Directory.Build.props` | Settings shared by every project: `net10.0`, nullable reference types, implicit usings and `TreatWarningsAsErrors`. The project files don't repeat them. |
| `src/GrantTracker.Domain/GrantTracker.Domain.csproj` | No references and no packages. |
| `src/GrantTracker.Infrastructure/GrantTracker.Infrastructure.csproj` | References Domain. The EF Core packages come in Part 4. |
| `src/GrantTracker.Api/` | References Domain and Infrastructure. `Program.cs` only adds and maps controllers. The error handler, clock, database and health checks come in Parts 3 to 8. `appsettings*.json` and `Properties/launchSettings.json` are as the template created them (`http` profile on port 5038). |
| `tests/GrantTracker.Tests/GrantTracker.Tests.csproj` | References all three projects. Packages: `xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5 and `Microsoft.NET.Test.Sdk` 18.12.0. `using Xunit;` is global. |
| `dotnet-tools.json` | The local tool manifest, with `dotnet-ef` 10.0.15. The .NET 10 SDK puts the manifest at the repository root, not in `.config/`. |
| `package.json` | `npm test` runs `dotnet test GrantTracker.slnx`. Vitest is added in Part 10. |
| `.gitignore` | Adds `bin/`, `obj/`, `node_modules/` and `*.db`, plus SQLite's side files `*.db-shm` and `*.db-wal`. |

Removed from the templates:
- The sample `WeatherForecast` controller and model, and the `.http` file.
- `Class1.cs` in both class libraries.
- The `Microsoft.AspNetCore.OpenApi` package. It's added back in Part 9.
- `UseHttpsRedirection` and `UseAuthorization`. The app has no login, and the development profile uses HTTP.

How it was checked:

| Check | Result |
|---|---|
| `dotnet build GrantTracker.slnx --no-incremental` | 0 warnings, 0 errors. |
| `dotnet list reference` on each project | Domain: none. Infrastructure: Domain. Api: Domain and Infrastructure. Tests: all three. |
| `npm test` | Exits 0 with "No test is available", because there are no tests yet. |
| A throwaway test file with one passing and one failing `[Fact]`, deleted afterwards | xUnit found both tests. `npm test` reported 1 passed and 1 failed, and exited 1, so the command fails when a test fails. |
| A throwaway unused variable in Domain, deleted afterwards | The build failed with CS0219 as an error, so warnings are treated as errors. |

Changes from the plan:
- **xUnit v3 is on the 3.x line, not the latest 4.x.** xUnit v3 4.x runs only on Microsoft.Testing.Platform. The .NET 10 SDK's `dotnet test` uses that platform only if the repository opts in through a `global.json`. You chose to stay on 3.x, which still runs through VSTest, rather than add a `global.json` (Decision 11).
- **`dotnet-ef` is pinned to 10.0.15.** By default the install picked 11.0.0, which belongs to EF Core 11 rather than the EF Core 10 this project uses.
- **`.gitignore` also ignores `*.db-shm` and `*.db-wal`,** which SQLite creates next to the database file.

---

### Part 1: Domain: creating a task (BR-1, BR-2, BR-3)

**Builds:**
- Enums `GrantStatus`, `TaskPriority` and `GrantTaskStatus`, with the explicit values from §3.
- Entities `Grant` and `GrantTask` with private setters (§3.1, §3.2).
- `Grant.AddTask(id, title, dueDate, priority, today, nowUtc)`:
  - Trims the title.
  - Checks BR-1 first, then BR-2 and BR-3 together, collecting both field errors (§4 "Order of checks").
  - Sets `CreatedAtUtc` and returns the new task.
- `BusinessRuleException(code)`, and a validation exception that carries one or more field errors (`title`, `dueDate`). §5.1 maps it to a 400.

**Done when:**
- Every BR-1, BR-2 and BR-3 boundary in §7 has a domain test.
- `npm test` runs these tests.

**Tests** (`tests/GrantTracker.Tests/Domain/GrantAddTaskTests.cs`):

| Test | Covers |
|---|---|
| `Given_OpenGrantAndValidInput_When_AddingTask_Then_TaskIsPendingWithCreatedAtUtcSet` | Happy path. Status is Pending, `CreatedAtUtc` is `nowUtc`. |
| `Given_ClosedGrant_When_AddingTask_Then_ThrowsBr1` | BR-1 |
| `Given_ClosedGrantAndInvalidTaskTitle_When_AddingTask_Then_ThrowsBr1` | BR-1 is checked first: adding a task with an empty title to a closed grant throws BR-1, not a title error |
| `Given_DueDateToday_When_AddingTask_Then_TaskIsAdded` | BR-2: the "today" boundary is allowed |
| `Given_DueDateYesterday_When_AddingTask_Then_ThrowsValidationOnDueDate` | BR-2: no due date in the past |
| `Given_DueDateEqualToApplicationDate_When_AddingTask_Then_TaskIsAdded` | BR-2: the application-date boundary is allowed |
| `Given_DueDateOneDayBeforeApplicationDate_When_AddingTask_Then_ThrowsValidationOnDueDate` | BR-2: not before the application date. The grant's application date is set in the future so that only this rule applies. |
| `Given_EmptyOrWhitespaceTaskTitle_When_AddingTask_Then_ThrowsValidationOnTitle` (`""`, `"   "`) | BR-3: title required |
| `Given_TaskTitleOf100CharactersAfterTrimming_When_AddingTask_Then_TaskIsAdded` | BR-3: boundary allowed, including a padded title |
| `Given_TaskTitleOf101CharactersAfterTrimming_When_AddingTask_Then_ThrowsValidationOnTitle` | BR-3: over the limit |
| `Given_TaskTitleWithSurroundingSpaces_When_AddingTask_Then_TitleIsStoredTrimmed` | BR-3: trimmed before storing (§7) |
| `Given_InvalidTaskTitleAndInvalidDueDate_When_AddingTask_Then_BothFieldErrorsAreReported` | BR-2 and BR-3 reported together |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| `dueDate < today` → `dueDate <= today` | `Given_DueDateToday_..._Then_TaskIsAdded` |
| Remove the application-date check | `Given_DueDateOneDayBeforeApplicationDate_...` |
| Remove the BR-1 check | `Given_ClosedGrant_When_AddingTask_Then_ThrowsBr1` |
| Check BR-3 before BR-1 | `Given_ClosedGrantAndInvalidTaskTitle_..._Then_ThrowsBr1` |
| Limit `100` → `101` | `Given_TaskTitleOf101Characters...` |
| Remove `Trim()` | `Given_TaskTitleOf100CharactersAfterTrimming...` (padded case), `..._Then_TitleIsStoredTrimmed` |
| Stop at the first field error | `Given_InvalidTaskTitleAndInvalidDueDate_...` |

**Build record:** Built on 2026-11-12.

What was built (`src/GrantTracker.Domain/`):

| File | What it holds |
|---|---|
| `GrantStatus.cs`, `TaskPriority.cs`, `GrantTaskStatus.cs` | The enums, with explicit values: `Open = 0`, `Closed = 1`; `Low = 1`, `Medium = 2`, `High = 3`; `Pending = 0`, `Completed = 1`, `Cancelled = 2`. `GrantTaskStatus` is never stored, so its values have no meaning outside the code. |
| `Grant.cs` | All the §3.1 fields, with private setters. `Tasks` is a read-only view of a private list. The constructor `(id, reference, organisationName, applicationDate)` creates an open grant with a new `Version`. Its parameter names match the properties, so EF Core can use it in Part 4. |
| `Grant.AddTask(id, title, dueDate, priority, today, nowUtc)` | 1. BR-1: a closed grant throws `BusinessRuleException("BR-1")`. 2. The title is trimmed (`null` counts as empty). 3. BR-3 and BR-2 are both checked, and every message is collected per field: `title` gets "Title is required." or "Title must be 100 characters or fewer."; `dueDate` gets "Due date cannot be in the past." and/or "Due date cannot be before the grant's application date.". 4. Any errors → one `DomainValidationException`. 5. Otherwise the task is created with `CreatedAtUtc = nowUtc`, added and returned. |
| `GrantTask.cs` | All the §3.2 fields, with private setters, and `TitleMaxLength = 100`. The constructor is `internal`, so tasks are created only through `Grant.AddTask`. `Status` is derived from `CompletedAtUtc` and `CancelledAtUtc` (§3.3). |
| `BusinessRuleException.cs` | `Code` (becomes the ProblemDetails `code`, and the end of its `type` URI) and a message. The constant `Br1GrantClosed = "BR-1"`. |
| `DomainValidationException.cs` | `Errors`: field name → messages, keyed `title` and `dueDate`. |

Tests (`tests/GrantTracker.Tests/Domain/GrantAddTaskTests.cs`): 12 tests, 14 cases. `today` is 2026-03-11 and `nowUtc` is `2026-03-10T22:00:00Z`, both written as literals.

| Test | What it checks | Covers |
|---|---|---|
| `Given_OpenGrantAndValidInput_When_AddingTask_Then_TaskIsPendingWithCreatedAtUtcSet` | The task is in `grant.Tasks` with the given id, grant id, title, due date and priority. Status is Pending, `CreatedAtUtc` is `2026-03-10T22:00:00Z`, and the completed and cancelled times are null. | Happy path |
| `Given_ClosedGrant_When_AddingTask_Then_ThrowsBr1` | Code `"BR-1"`, and no task is added | BR-1 |
| `Given_ClosedGrantAndInvalidTaskTitle_When_AddingTask_Then_ThrowsBr1` | An empty task title on a closed grant gives `"BR-1"`, not a title error | BR-1 checked first (§4 order of checks) |
| `Given_DueDateToday_When_AddingTask_Then_TaskIsAdded` | Due 2026-03-11 is accepted | BR-2: the "today" boundary |
| `Given_DueDateYesterday_When_AddingTask_Then_ThrowsValidationOnDueDate` | Due 2026-03-10: exactly one error, on `dueDate`. No task added. | BR-2: not in the past |
| `Given_DueDateEqualToApplicationDate_When_AddingTask_Then_TaskIsAdded` | Application date and due date both 2026-03-16, which is after today, so only this rule applies. Accepted. | BR-2: the application-date boundary |
| `Given_DueDateOneDayBeforeApplicationDate_When_AddingTask_Then_ThrowsValidationOnDueDate` | Application date 2026-03-16 and due date 2026-03-15 (not in the past): one error on `dueDate` | BR-2: not before the application date |
| `Given_EmptyOrWhitespaceTaskTitle_When_AddingTask_Then_ThrowsValidationOnTitle` (`""`, `"   "`) | One error, on `title`. No task added. | BR-3: title required |
| `Given_TaskTitleOf100CharactersAfterTrimming_When_AddingTask_Then_TaskIsAdded` (0 and 2 spaces on each side) | Accepted, and the stored title is 100 characters | BR-3: the 100 boundary, counted after trimming |
| `Given_TaskTitleOf101CharactersAfterTrimming_When_AddingTask_Then_ThrowsValidationOnTitle` | One error, on `title` | BR-3: over the limit |
| `Given_TaskTitleWithSurroundingSpaces_When_AddingTask_Then_TitleIsStoredTrimmed` | `"  Verify bank details  "` is stored as `"Verify bank details"` | BR-3 / §7: stored trimmed |
| `Given_InvalidTaskTitleAndInvalidDueDate_When_AddingTask_Then_BothFieldErrorsAreReported` | An empty title and a due date of yesterday give errors on both `dueDate` and `title` | BR-2 and BR-3 reported together (§4 order of checks) |

Mutation checks. Each was applied to `Grant.cs`, all tests were run, and the file was restored. Checks 8 and 9 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | `dueDate < today` → `dueDate <= today` | `Given_DueDateToday_..._Then_TaskIsAdded` |
| 2 | Remove the application-date check | `Given_DueDateOneDayBeforeApplicationDate_...` |
| 3 | Remove the BR-1 check | `Given_ClosedGrant_..._Then_ThrowsBr1`, `Given_ClosedGrantAndInvalidTaskTitle_..._Then_ThrowsBr1` |
| 4 | Check BR-3 before BR-1 (an empty title throws before the closed check) | `Given_ClosedGrantAndInvalidTaskTitle_..._Then_ThrowsBr1`, `Given_InvalidTaskTitleAndInvalidDueDate_...` |
| 5 | Limit `100` → `101` | `Given_TaskTitleOf101CharactersAfterTrimming_...` |
| 6 | Remove `Trim()` | `Given_TaskTitleOf100CharactersAfterTrimming_...` (2 spaces), `Given_TaskTitleWithSurroundingSpaces_...`, `Given_EmptyOrWhitespaceTaskTitle_...` (`"   "`) |
| 7 | Throw as soon as the title is invalid, without checking the due date | `Given_InvalidTaskTitleAndInvalidDueDate_...` |
| 8 | `dueDate < ApplicationDate` → `dueDate <= ApplicationDate` | `Given_DueDateEqualToApplicationDate_..._Then_TaskIsAdded` |
| 9 | Remove the past-date check | `Given_DueDateYesterday_...`, `Given_InvalidTaskTitleAndInvalidDueDate_...` |

Checks 2 and 9 were first written as `if (false)`. That didn't compile, because unreachable code is a warning and warnings are errors. They were re-run with a condition that is never true (`dueDate < DateOnly.MinValue`).

After restoring, `npm test` passed twice: 14 passed, 0 failed.

Changes from the plan:
- **A minimal `Grant.Close(note, nowUtc)` was built in Part 1.** The BR-1 tests need a closed grant, and the plan only built `Close` in Part 2. It sets `Status = Closed`, `ClosedAtUtc` and the note (trimmed, and empty becomes `null`). It has no tests of its own yet. Part 2 adds the repeat no-op, BR-5 and cancelling pending tasks, with their tests. The alternative was to set the private status by reflection in the tests, which would test a state the code can't otherwise reach.
- **`GrantTask.Status` was built in Part 1, not Part 2,** because the happy-path test checks that a new task is Pending. Part 2 still adds its Theory over all three states.

---

### Part 2: Domain: complete, close, status, overdue (BR-4, BR-5, BR-6, §4.7)

**Builds:**
- `GrantTask.Status`, derived from the two timestamps (§3.3). _Already built in Part 1; Part 2 adds its tests._
- `GrantTask.Complete(nowUtc)` (§4 BR-4 table).
- `GrantTask.IsOverdue(today)`.
- `Grant.Close(note, nowUtc)`, which cancels pending tasks (§4 BR-5).
- `GrantTaskQueries.IsPending` and `IsOverdue(today)` expressions (§3.3). They're tested against SQLite in Part 4.

**Done when:** every row of the BR-4 table, the BR-5 rules, the BR-6 rules and the §4.7 table that the domain owns has a test.

**Tests** (`tests/GrantTracker.Tests/Domain/`):

| Test | Covers |
|---|---|
| `Given_TaskTimestamps_When_ReadingStatus_Then_StatusMatchesTimestamps` (Theory: neither / completed / cancelled) | §3.3 derived status |
| `Given_PendingTaskOnOpenGrant_When_Completing_Then_CompletedAtUtcIsNow` | BR-4 |
| `Given_CompletedTask_When_CompletingAgainLater_Then_CompletedAtUtcIsUnchanged` | BR-4: the date never changes |
| `Given_CompletedTaskOnClosedGrant_When_Completing_Then_NothingChangesAndNoError` | BR-4 repeat on a closed grant, §4.7 |
| `Given_CancelledTask_When_Completing_Then_ThrowsGrantClosed` | §4.7 read-only |
| `Given_PendingHighTask_When_Closing_Then_ThrowsBr5WithBlockingTaskIds` | BR-5 |
| `Given_OnlyCompletedHighTask_When_Closing_Then_GrantIsClosed` | BR-5: completed High tasks don't block |
| `Given_PendingLowAndMediumTasks_When_Closing_Then_TheyAreCancelledAtClosedAtUtc` | Close cancels pending tasks. Completed tasks are untouched. |
| `Given_NoTasks_When_Closing_Then_GrantIsClosed` | §7 grant with no tasks |
| `Given_NoteWithSpacesOrBlank_When_Closing_Then_NoteIsTrimmedOrNull` (Theory) | §4 BR-5 note handling |
| `Given_ClosedGrant_When_ClosingAgain_Then_NoteClosedAtAndTasksAreUnchanged` | BR-5 repeat close, §4.7 |
| `Given_PendingTaskDueYesterday_When_CheckingOverdue_Then_IsOverdue` | BR-6 |
| `Given_PendingTaskDueToday_When_CheckingOverdue_Then_IsNotOverdue` | BR-6: due today isn't overdue |
| `Given_CompletedOrCancelledTaskPastDue_When_CheckingOverdue_Then_IsNotOverdue` (Theory) | BR-6 and §10.4: only pending tasks are overdue |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| `Complete` always sets `CompletedAtUtc` | `Given_CompletedTask_When_CompletingAgainLater_...` |
| Remove the High-priority check in `Close` | `Given_PendingHighTask_When_Closing_Then_ThrowsBr5...` |
| Block on any High task, not only pending ones | `Given_OnlyCompletedHighTask_...` |
| `dueDate < today` → `<=` in `IsOverdue` | `Given_PendingTaskDueToday_...` |
| Drop the `CancelledAtUtc == null` check in `IsOverdue` | `Given_CompletedOrCancelledTaskPastDue_...` (cancelled case) |
| `Close` runs again on a closed grant | `Given_ClosedGrant_When_ClosingAgain_...` |

**Build record:** Built on 2026-11-12.

What was built (`src/GrantTracker.Domain/`):

| Code | Behaviour |
|---|---|
| `GrantTask.Complete(nowUtc)` | In the order of the BR-4 table: 1. Already completed → returns without changing anything, even on a closed grant. 2. The grant is closed, or the task is cancelled → `BusinessRuleException("grant-closed")`. A cancelled task can only exist on a closed grant. 3. Otherwise `CompletedAtUtc = nowUtc`. |
| `GrantTask.IsOverdue(today)` | `Status == Pending && DueDate < today`. |
| `GrantTask.Grant` | A new navigation to the task's grant, set by `AddTask`. `Complete` needs it to apply the read-only rule. The internal constructor now takes the grant instead of the grant id. |
| `GrantTask.Cancel(nowUtc)` | `internal`: only `Grant.Close` calls it. |
| `Grant.Close(note, nowUtc)` | Replaces the minimal Part 1 version. 1. Already closed → returns without changing anything. 2. Any pending High task → `BusinessRuleException("BR-5")` with `Extensions["blockingTaskIds"]` = the ids of those tasks. Nothing changes. 3. Otherwise `Status = Closed`, `ClosedAtUtc = nowUtc`, the note is trimmed (empty or whitespace → `null`), and every pending task gets `CancelledAtUtc = nowUtc`. Completed tasks are untouched. |
| `BusinessRuleException` | New codes `GrantClosed = "grant-closed"` and `Br5PendingHighTasks = "BR-5"`, the key `BlockingTaskIdsKey = "blockingTaskIds"`, and an `Extensions` dictionary that the API will copy into the ProblemDetails body (Part 6). |
| `GrantTaskQueries.cs` | `IsPending` and `IsOverdue(today)` as expressions EF Core can translate. They aren't used or tested yet; Part 4 tests them against SQLite. |

Tests (`tests/GrantTracker.Tests/Domain/`): 13 tests, 20 cases. "Now" is `2026-03-10T22:00:00Z` (2026-03-11 09:00 in Sydney), and `today` is 2026-03-11. Tasks whose due date must be in the past are added with an earlier `today` (2026-03-01), the way the seed data will be.

| File and test | What it checks | Covers |
|---|---|---|
| `GrantTaskStatusTests`: `Given_TaskTimestamps_When_ReadingStatus_Then_StatusMatchesTimestamps` (neither → Pending, `CompletedAtUtc` → Completed, `CancelledAtUtc` → Cancelled) | Each state is reached through the public methods (`Complete`, `Close`), not by setting fields | §3.3 derived status |
| `GrantTaskCompleteTests`: `Given_PendingTaskOnOpenGrant_When_Completing_Then_CompletedAtUtcIsNow` | `CompletedAtUtc` is `2026-03-10T22:00:00Z`, and the status is Completed | BR-4 |
| `…Given_CompletedTask_When_CompletingAgainLater_Then_CompletedAtUtcIsUnchanged` | Completed at `2026-03-05T22:00:00Z`, completed again later: the date stays `2026-03-05T22:00:00Z` | BR-4: the date never changes |
| `…Given_CompletedTaskOnClosedGrant_When_Completing_Then_NothingChangesAndNoError` | No exception. `CompletedAtUtc` is unchanged, and the task wasn't cancelled. | BR-4 repeat on a closed grant, §4.7 |
| `…Given_CancelledTask_When_Completing_Then_ThrowsGrantClosed` | Code `"grant-closed"`. The task stays Cancelled with no `CompletedAtUtc`. | §4.7 read-only |
| `GrantCloseTests`: `Given_PendingHighTask_When_Closing_Then_ThrowsBr5WithBlockingTaskIds` | Pending High and pending Low tasks: code `"BR-5"`, and `blockingTaskIds` is exactly the High task's id. The grant is still Open with no close time or note, and the Low task is still Pending. | BR-5, nothing changes when blocked |
| `…Given_OnlyCompletedHighTask_When_Closing_Then_GrantIsClosed` | A completed High task doesn't block the close | BR-5: only pending High tasks block |
| `…Given_PendingLowAndMediumTasks_When_Closing_Then_TheyAreCancelledAtClosedAtUtc` | The Low and Medium tasks get `CancelledAtUtc` = `ClosedAtUtc` = `2026-03-10T22:00:00Z`. The completed High task keeps its `CompletedAtUtc` and isn't cancelled. | Close cancels pending tasks only |
| `…Given_NoTasks_When_Closing_Then_GrantIsClosed` | Closed, with `ClosedAtUtc` = `2026-03-10T22:00:00Z` | §7 grant with no tasks |
| `…Given_NoteWithSpacesOrBlank_When_Closing_Then_NoteIsTrimmedOrNull` (`"  Duplicate application  "` → `"Duplicate application"`, `"   "`, `""` and `null` → `null`) | The stored note | BR-5 note handling |
| `…Given_ClosedGrant_When_ClosingAgain_Then_NoteClosedAtAndTasksAreUnchanged` | A second close with a different note and a later time: the note, `ClosedAtUtc` and the cancelled task's `CancelledAtUtc` keep the first close's values | BR-5 repeat close, §4.7 |
| `GrantTaskOverdueTests`: `Given_PendingTaskDueYesterday_When_CheckingOverdue_Then_IsOverdue` | Due 2026-03-10, today 2026-03-11: overdue | BR-6 |
| `…Given_PendingTaskDueToday_When_CheckingOverdue_Then_IsNotOverdue` | Due 2026-03-11: not overdue | BR-6: due today isn't overdue |
| `…Given_CompletedOrCancelledTaskPastDue_When_CheckingOverdue_Then_IsNotOverdue` (Completed, Cancelled) | Due 2026-03-10: not overdue | BR-6 and §10.4: only pending tasks are overdue |

Mutation checks. Each was applied, all tests were run, and the file was restored. Checks 7 to 13 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | `Complete` always sets `CompletedAtUtc` | `Given_CompletedTask_When_CompletingAgainLater_...`, `Given_CompletedTaskOnClosedGrant_...` |
| 2 | Remove the High-priority check in `Close` | `Given_PendingHighTask_When_Closing_Then_ThrowsBr5...` |
| 3 | Block on any High task, not only pending ones | `Given_OnlyCompletedHighTask_...`, `Given_PendingLowAndMediumTasks_...` |
| 4 | `DueDate < today` → `<=` in `IsOverdue` | `Given_PendingTaskDueToday_...` |
| 5 | Drop the cancelled check in `IsOverdue` (`CompletedAtUtc == null` instead of Pending) | `Given_CompletedOrCancelledTaskPastDue_...` (Cancelled) |
| 6 | `Close` runs again on a closed grant | `Given_ClosedGrant_When_ClosingAgain_...` |
| 7 | `Complete`: remove the grant-closed check | `Given_CancelledTask_When_Completing_Then_ThrowsGrantClosed` |
| 8 | `Complete`: check grant-closed before already-completed | `Given_CompletedTaskOnClosedGrant_...` |
| 9 | `Close` cancels every task, including completed ones | `Given_PendingLowAndMediumTasks_...`, `Given_CompletedTaskOnClosedGrant_...` |
| 10 | `Close` doesn't cancel pending tasks | `Given_PendingLowAndMediumTasks_...`, `Given_ClosedGrant_When_ClosingAgain_...`, `Given_CancelledTask_...`, the status Theory (Cancelled), the overdue Theory (Cancelled) |
| 11 | `Close` doesn't trim the note | The note Theory (`"  Duplicate application  "`) |
| 12 | `Close` marks the grant closed before checking BR-5 | `Given_PendingHighTask_..._ThrowsBr5...` (the grant must still be Open) |
| 13 | `Status` reads a completed timestamp as Cancelled | The status Theory (Completed), `Given_PendingTaskOnOpenGrant_...`, `Given_CompletedTask_...`, `Given_CompletedTaskOnClosedGrant_...` |

After restoring, `npm test` passed twice: 34 passed, 0 failed (14 from Part 1, 20 from Part 2).

Changes from the plan and design:
- **New navigation `GrantTask.Grant`.** The design names `GrantTask.Complete(nowUtc)`, which must know whether the grant is closed. The navigation is the simplest way to give it that. In Part 4, EF Core needs a constructor without the `Grant` parameter, because it can't pass navigations to a constructor. A private constructor will be added there. In Part 6, the complete service must load the task with its grant.
- **The BR-5 extension field is named `blockingTaskIds`.** The design said only "an extension field". `docs/DESIGN.md` BR-5 now names it and says nothing changes when the close is blocked.
- **The §4.7 row "complete a pending task on a closed grant"** has no domain test: after `Close` there are no pending tasks, so the domain can't reach that state. The race version is covered by Part 7 (OP-2).

---

### Part 3: Clock: "today" in Sydney

**Builds:**
- `IAppClock` and `AppClock` in Api (§2, §4.0). It's built from `TimeProvider` and `App:TimeZoneId` (default `Australia/Sydney`, an IANA id; .NET accepts IANA ids on Windows too).
- The zone is checked at start-up, using options validation that runs at start-up (§4.0). That check is tested in Part 5, because it needs the app host.

**Done when:** "today" is correct around midnight and both daylight-saving changes, whatever the machine's time zone.

**Tests** (`tests/GrantTracker.Tests/Api/AppClockTests.cs`, using `FakeTimeProvider`):

| Test | Covers |
|---|---|
| `Given_UtcInstantOnPreviousUtcDate_When_ReadingToday_Then_ReturnsSydneyDate` (`2026-03-10T22:00Z` → 2026-03-11) | §4.0: BR-2 and BR-6 use the Sydney date, not the UTC date. This is also the pinned instant for the API tests. |
| `Given_OneMinuteBeforeSydneyMidnight_When_ReadingToday_Then_ReturnsSameDay` (`2026-03-10T12:59Z` → 2026-03-10) | §4.0 midnight boundary |
| `Given_SydneyMidnight_When_ReadingToday_Then_ReturnsNextDay` (`2026-03-10T13:00Z` → 2026-03-11) | §4.0 midnight boundary |
| `Given_DaylightSavingEnds_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight` (5 April 2026: `13:59Z` → 5 April, `14:00Z` → 6 April) | §7 daylight saving (AEST, UTC+10) |
| `Given_DaylightSavingStarts_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight` (4 October 2026: `12:59Z` → 4 October, `13:00Z` → 5 October) | §7 daylight saving (AEDT, UTC+11) |
| `Given_FakeClock_When_ReadingUtcNow_Then_ReturnsFakeInstantMarkedUtc` | `UtcNow` comes from `TimeProvider`, never `DateTime.UtcNow`, and is `DateTimeKind.Utc` |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Use the UTC date instead of converting to Sydney | `Given_UtcInstantOnPreviousUtcDate_...`, `Given_SydneyMidnight_...` |
| Hard-code UTC+11 instead of using the time zone | `Given_DaylightSavingEnds_...` |

**Build record:** Built on 2026-11-12.

What was built:

| File | What it holds |
|---|---|
| `src/GrantTracker.Api/Time/AppOptions.cs` | The `App` configuration section: `TimeZoneId`, default `"Australia/Sydney"`. `HasKnownTimeZone()` uses `TimeZoneInfo.TryFindSystemTimeZoneById`. |
| `src/GrantTracker.Api/Time/IAppClock.cs` | `DateTime UtcNow` and `DateOnly Today`. |
| `src/GrantTracker.Api/Time/AppClock.cs` | Finds the zone once, in the constructor. `UtcNow` = `TimeProvider.GetUtcNow().UtcDateTime`, read on every call, with `Kind = Utc`. `Today` = the `TimeProvider` instant converted to the configured zone, then its date. It never uses the machine's time zone or `DateTime.Now`/`UtcNow`. |
| `src/GrantTracker.Api/Program.cs` | Registers `TimeProvider.System`, binds `AppOptions` to `App`, validates the zone with `ValidateOnStart()`, and registers `IAppClock` as a singleton. |
| `src/GrantTracker.Api/appsettings.json` | `"App": { "TimeZoneId": "Australia/Sydney" }`. |
| `tests/GrantTracker.Tests/GrantTracker.Tests.csproj` | Adds `Microsoft.Extensions.TimeProvider.Testing` 10.11.0. |

Tests (`tests/GrantTracker.Tests/Api/AppClockTests.cs`): 6 tests, 8 cases. Each one builds an `AppClock` over a `FakeTimeProvider`.

| Test | Instant (UTC) → expected | Covers |
|---|---|---|
| `Given_UtcInstantOnPreviousUtcDate_When_ReadingToday_Then_ReturnsSydneyDate` | `2026-03-10T22:00Z` → 2026-03-11 | §4.0: BR-2 and BR-6 use the Sydney date, not the UTC date. This is the pinned instant for the API tests. |
| `Given_OneMinuteBeforeSydneyMidnight_When_ReadingToday_Then_ReturnsSameDay` | `2026-03-10T12:59Z` → 2026-03-10 | §4.0 midnight boundary (AEDT) |
| `Given_SydneyMidnight_When_ReadingToday_Then_ReturnsNextDay` | `2026-03-10T13:00Z` → 2026-03-11 | §4.0 midnight boundary (AEDT) |
| `Given_DaylightSavingEnds_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight` | `2026-04-05T13:59Z` → 5 April; `2026-04-05T14:00Z` → 6 April | §7: the first midnight after daylight saving ends is at UTC+10 |
| `Given_DaylightSavingStarts_When_ReadingTodayAroundMidnight_Then_DayChangesAtLocalMidnight` | `2026-10-04T12:59Z` → 4 October; `2026-10-04T13:00Z` → 5 October | §7: the first midnight after daylight saving starts is at UTC+11 |
| `Given_FakeClock_When_ReadingUtcNow_Then_ReturnsFakeInstantMarkedUtc` | Start `2026-03-10T22:00Z`, advance 5 minutes → `2026-03-10T22:05`, `Kind = Utc` | `UtcNow` comes from `TimeProvider`, is read when asked for, and is UTC |

The test strings are parsed with the invariant culture, so the tests don't depend on the machine's culture.

Mutation checks. Each was applied to `AppClock.cs`, all tests were run, and the file was restored. Checks 3 to 7 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | Use the UTC date instead of converting to Sydney | `Given_UtcInstantOnPreviousUtcDate_...`, `Given_SydneyMidnight_...`, both daylight-saving Theories (the "next day" case) |
| 2 | Hard-code UTC+11 | `Given_DaylightSavingEnds_...` (`13:59Z` → 5 April) |
| 3 | Hard-code UTC+10 | `Given_DaylightSavingStarts_...` (`13:00Z` → 5 October), `Given_SydneyMidnight_...` |
| 4 | Use the machine's time zone (`TimeZoneInfo.Local`) | `Given_SydneyMidnight_...`, both daylight-saving Theories |
| 5 | `UtcNow` from `DateTime.UtcNow` | `Given_FakeClock_When_ReadingUtcNow_...` |
| 6 | `UtcNow` loses `DateTimeKind.Utc` (`.DateTime` instead of `.UtcDateTime`) | `Given_FakeClock_When_ReadingUtcNow_...` |
| 7 | `UtcNow` read once, when the clock is created | `Given_FakeClock_When_ReadingUtcNow_...` |

Limit of check 4: it was caught because this machine runs at UTC+07:00. On a machine set to Sydney time, `TimeZoneInfo.Local` would give the right answer and the tests couldn't tell the difference. Code review is the protection there: `AppClock` is the only class that converts to a local date (§2).

Start-up check, by hand: `dotnet run` with `--App:TimeZoneId=Nowhere/Nope` stopped at start-up with `OptionsValidationException: App:TimeZoneId is not a known time zone id.` The automated test is in Part 5, as planned.

After restoring, `npm test` passed twice: 42 passed, 0 failed (34 from Parts 1 and 2, and 8 from Part 3).

Changes from the plan: none. The extra file `AppOptions.cs` holds the `App:TimeZoneId` setting that the plan names.

---

### Part 4: Infrastructure: database, dates, versions, seed data

**Builds:**
- `AppDbContext` and the EF configuration from §3:
  - Required fields and maximum lengths. SQLite doesn't enforce the lengths (§3.6), so there's no test for them here.
  - A unique index on `Reference`.
  - Enums stored as integers.
  - The check constraint `CK_GrantTask_CompletedXorCancelled`.
  - The filtered indexes.
  - `Restrict` on delete.
  - `Status` ignored.
- The UTC converter on every `DateTime` and `DateTime?` property (§3.6): it marks values read back as UTC, and throws when saving a non-UTC value.
- Version renewal in `SaveChanges`, rules 1 and 2 (§3.5).
- The initial migration.
- The seeder (§8): fixed ids and versions, timestamps at 09:00 Sydney, dates relative to `today`, run only when the database is empty.
- A test helper that creates an in-memory SQLite database per test and applies the migrations. Tests then run against the real schema, including the check constraint and the filtered indexes.

**Done when:**
- The constraints that SQLite enforces are proven by tests.
- Dates are stored and read back as §3.6 describes.
- Version conflicts are detected.
- The query expressions agree with the domain.
- The seed matches §8 exactly.

**Tests** (`tests/GrantTracker.Tests/Infrastructure/`):

| Test | Covers |
|---|---|
| `Given_TaskRow_When_SettingBothCompletedAndCancelledInSql_Then_CheckConstraintRejectsIt` | §3.2 check constraint, a second line of defence for §3.3 |
| `Given_UtcInstant_When_SavedAndReadBack_Then_ValueIsUnchangedAndKindIsUtc` (`2026-03-10T22:00:00Z`) | §3.6 UTC converter on read. The value isn't shifted by the machine's time zone. |
| `Given_LocalOrUnspecifiedDateTime_When_Saving_Then_SaveFails` (Theory) | §3.6 UTC converter on write |
| `Given_DueDate_When_Saved_Then_StoredAsZeroPaddedIsoText` (raw SQL read, for example `'2026-03-05'`) | §3.6 `DateOnly` storage |
| `Given_DueDatesAcrossMonthAndYearEnds_When_FilteringDueDateBeforeTodayInSql_Then_ResultIsCorrect` | §3.6 text comparison in SQL (BR-2, BR-6) |
| `Given_SavedTask_When_ModifiedAndSaved_Then_VersionIsRenewed` | §3.5 rule 1 |
| `Given_SavedGrant_When_TaskIsAddedAndSaved_Then_GrantVersionIsRenewed` | §3.5 rule 2 |
| `Given_GrantLoadedInTwoContexts_When_TaskAddedInOneAndGrantClosedInOther_Then_SecondSaveThrowsConcurrencyException` | OP-2 mechanism: the add/close race can't break BR-1 or BR-5 |
| `Given_TaskLoadedInTwoContexts_When_CompletedInOneAndGrantClosedInOther_Then_SecondSaveThrowsAndTaskIsNeverBoth` | OP-2 mechanism: complete/close race |
| `Given_TasksInEveryState_When_QueryingWithIsPendingAndIsOverdue_Then_ResultsMatchDomainProperties` | §3.3 query expressions agree with the domain (BR-6) |
| `Given_TasksOfEachPriority_When_OrderingByPriorityDescendingInSql_Then_OrderIsHighMediumLow` | §3 integer enums sort in business order |
| `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_GrantsMatchSpec` | §8: references, organisations, application dates, status, fixed ids (literal values) |
| `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TasksMatchSpec` | §8: titles, priorities, due dates, statuses (literal values) |
| `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TimestampsAre0900SydneyOnPreviousUtcDay` | §8: for example `ClosedAtUtc` = `2025-10-16T22:00:00Z` (AEDT) and the GR-100003 task `CreatedAtUtc` = `2025-08-22T23:00:00Z` (AEST) |
| `Given_SeededDatabase_When_SeedingAgain_Then_NothingIsAdded` | §8 the seeder can run more than once safely |
| `Given_SameToday_When_SeedingTwoDatabases_Then_BothSeedsAreIdentical` | §8 the seed is the same every time |
| `Given_ExistingReference_When_SavingAnotherGrantWithSameReference_Then_SaveFails` | §3.1 unique `Reference` |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Remove the UTC converter | `Given_UtcInstant_When_SavedAndReadBack_...` (`Kind` is `Unspecified`), `Given_LocalOrUnspecifiedDateTime_...` |
| Seed timestamps with the UTC date instead of 09:00 Sydney | `..._Then_TimestampsAre0900SydneyOnPreviousUtcDay` |
| Seed with a hard-coded UTC+11 offset | `..._TimestampsAre0900Sydney...` (the AEST value is an hour off) |
| Seed with `Guid.NewGuid()` | `Given_SameToday_When_SeedingTwoDatabases_...`, `..._GrantsMatchSpec` |
| Remove rule 2 (adding a task renews the grant's version) | `Given_SavedGrant_When_TaskIsAdded...`, `Given_GrantLoadedInTwoContexts_...` |
| Remove the check constraint | `Given_TaskRow_When_SettingBothCompletedAndCancelled...` |
| Store priority as a string | `Given_TasksOfEachPriority_...` |
| `IsOverdue` expression uses `<=` | `Given_TasksInEveryState_...` |
| Seed GR-100001's High task due at `today - 2` | `..._Then_TasksMatchSpec` |

**Build record:** Built on 2026-11-13.

What was built:

| File | What it holds |
|---|---|
| `src/GrantTracker.Infrastructure/GrantTracker.Infrastructure.csproj` | Adds `Microsoft.EntityFrameworkCore.Sqlite` 10.0.15 and `Microsoft.EntityFrameworkCore.Design` 10.0.15 (private, for `dotnet ef`). Same version as the pinned `dotnet-ef` tool. |
| `src/GrantTracker.Infrastructure/AppDbContext.cs` | `Grants` and `GrantTasks`. Applies the configurations. Registers `UtcDateTimeConverter` for every `DateTime` and `DateTime?` property. Overrides `SaveChanges` and `SaveChangesAsync` to renew versions (§3.5): rule 1, every Modified grant or task gets a new `Version`; rule 2, an Added task renews its grant's `Version` when that grant already exists (Unchanged or Modified). |
| `src/GrantTracker.Infrastructure/UtcDateTimeConverter.cs` | On read, `DateTime.SpecifyKind(value, Utc)`; on write, throws `InvalidOperationException` ("Only UTC DateTime values can be saved, ... Kind = ...") for any value that isn't UTC. |
| `src/GrantTracker.Infrastructure/Configurations/GrantConfiguration.cs` | Table `Grants`: app-chosen `Id`; `Reference` required, max 20, unique index; `OrganisationName` required, max 200; `Status` as int, indexed; `ClosedNote` max 1000; `Version` concurrency token; one-to-many to tasks with `Restrict` on delete, through the `_tasks` field. |
| `src/GrantTracker.Infrastructure/Configurations/GrantTaskConfiguration.cs` | Table `GrantTasks` with `CK_GrantTask_CompletedXorCancelled`; app-chosen `Id`; `Title` required, max 100; `Priority` as int; `Status` ignored; `Version` concurrency token; indexes on `GrantId`, filtered `(GrantId, Priority)` and filtered `DueDate`, both filtered to pending tasks. |
| `src/GrantTracker.Infrastructure/Migrations/20261112182910_InitialCreate.cs` (+ Designer and model snapshot) | The initial migration, generated by `dotnet ef migrations add InitialCreate`. |
| `src/GrantTracker.Infrastructure/SeedData.cs` | `SeedAsync(db, today, timeZone)`. Returns at once if any grant exists. Builds the §8 data through the domain methods (`AddTask` with `today` = the grant's application date, `Complete`, `Close`), sets the fixed versions, and saves once. Every timestamp is 09:00 on its date, converted with the given time zone. |
| `src/GrantTracker.Infrastructure/DesignTimeDbContextFactory.cs` | Lets `dotnet ef` create the DbContext without starting the API. The plan didn't list this file (see "Changes from the plan"). |
| `src/GrantTracker.Domain/GrantTask.cs` | Adds the private constructor EF uses to load a task. It has no `Grant` parameter, because EF can't pass a navigation to a constructor; EF sets `Grant` only when the grant is loaded too. |

Seed ids and versions:

| Rows | Ids | Versions |
|---|---|---|
| Grants GR-100001 to GR-100004 | `00000000-0000-0000-0000-000000100001` to `...100004` (§8) | `00000000-0000-0000-0002-000000000001` to `...000004` |
| Tasks 1 to 6, in the order of CONTEXT.md §8 | `00000000-0000-0000-0001-000000000001` to `...000006` (§8) | `00000000-0000-0000-0003-000000000001` to `...000006` |

Tests (`tests/GrantTracker.Tests/Infrastructure/`): 17 tests, 18 cases. `SqliteTestDatabase.cs` is the helper: one open in-memory SQLite connection per test, the real migrations applied with `Migrate()`, and any number of contexts on that connection (used to play two requests at once). It also reads and writes raw SQL, to see what is really stored.

| File | Test | Covers |
|---|---|---|
| `SchemaTests.cs` | `Given_TaskRow_When_SettingBothCompletedAndCancelledInSql_Then_CheckConstraintRejectsIt` (raw `UPDATE`, SQLite error 275, constraint name in the message) | §3.2 check constraint, second line of defence for §3.3 |
| `SchemaTests.cs` | `Given_ExistingReference_When_SavingAnotherGrantWithSameReference_Then_SaveFails` (`DbUpdateException`, inner SQLite error 2067 on `Grants.Reference`) | §3.1 unique `Reference` |
| `SchemaTests.cs` | `Given_TasksOfEachPriority_When_OrderingByPriorityDescendingInSql_Then_OrderIsHighMediumLow` (added Medium, Low, High) | §3 integer enums sort in business order |
| `DateStorageTests.cs` | `Given_UtcInstant_When_SavedAndReadBack_Then_ValueIsUnchangedAndKindIsUtc` (`2026-03-10T22:00:00Z`) | §3.6 UTC converter on read |
| `DateStorageTests.cs` | `Given_LocalOrUnspecifiedDateTime_When_Saving_Then_SaveFails` (Theory: `Local`, `Unspecified`; nothing stored) | §3.6 UTC converter on write |
| `DateStorageTests.cs` | `Given_DueDate_When_Saved_Then_StoredAsZeroPaddedIsoText` (2026-03-05 → `'2026-03-05'`) | §3.6 `DateOnly` storage |
| `DateStorageTests.cs` | `Given_DueDatesAcrossMonthAndYearEnds_When_FilteringDueDateBeforeTodayInSql_Then_ResultIsCorrect` (today 2026-10-01; 2025-12-31, 2026-01-01, 2026-02-01, 2026-09-30 are before; 2026-10-01, 2026-10-02, 2027-01-01 are not) | §3.6 text comparison in SQL (BR-2, BR-6) |
| `VersionTests.cs` | `Given_SavedTask_When_ModifiedAndSaved_Then_VersionIsRenewed` | §3.5 rule 1 |
| `VersionTests.cs` | `Given_SavedGrant_When_TaskIsAddedAndSaved_Then_GrantVersionIsRenewed` | §3.5 rule 2 |
| `VersionTests.cs` | `Given_GrantLoadedInTwoContexts_When_TaskAddedInOneAndGrantClosedInOther_Then_SecondSaveThrowsConcurrencyException` | OP-2 mechanism: the add/close race can't break BR-1 or BR-5 |
| `VersionTests.cs` | `Given_TaskLoadedInTwoContexts_When_CompletedInOneAndGrantClosedInOther_Then_SecondSaveThrowsAndTaskIsNeverBoth` (task completed only, grant still Open) | OP-2 mechanism: complete/close race; the failed close is rolled back as a whole |
| `GrantTaskQueriesTests.cs` | `Given_TasksInEveryState_When_QueryingWithIsPendingAndIsOverdue_Then_ResultsMatchDomainProperties` (pending due yesterday, today and tomorrow; completed and cancelled due yesterday; literal expected ids) | §3.3 query expressions agree with the domain (BR-6) |
| `SeedDataTests.cs` | `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_GrantsMatchSpec` (ids, references, organisations, application dates 2026-01-10, 2026-02-19, 2025-08-23, 2026-03-06, status, no note) | §8 grants |
| `SeedDataTests.cs` | `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TasksMatchSpec` (ids, grants, titles, priorities, due dates 2026-03-08, 2026-03-16, 2026-01-20, 2026-03-10, 2026-03-21, 2025-10-12, statuses) | §8 tasks |
| `SeedDataTests.cs` | `Given_EmptyDatabaseAndToday20260311_When_Seeding_Then_TimestampsAre0900SydneyOnPreviousUtcDay` (stored text: GR-100003 closed `2025-10-16 22:00:00`; created `2026-01-09 22:00:00`, `2026-02-18 22:00:00`, `2025-08-22 23:00:00` (AEST); completed `2026-01-17 22:00:00`, `2025-10-10 22:00:00`; nothing cancelled) | §8 timestamps, both Sydney offsets |
| `SeedDataTests.cs` | `Given_SeededDatabase_When_SeedingAgain_Then_NothingIsAdded` (second run on 2026-03-12: still 4 grants, 6 tasks, first dates kept) | §8 the seeder can run more than once safely |
| `SeedDataTests.cs` | `Given_SameToday_When_SeedingTwoDatabases_Then_BothSeedsAreIdentical` (every stored column of every row, ids and versions included) | §8 the seed is the same every time |

Mutation checks. Each was applied, all tests were run, and the files were restored. Checks that change the schema (8, 9, 13) also regenerated the migration from the mutated model, because the tests build the database from the migrations. Checks 5, 7, 12 and 13 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | Remove the UTC converter | `Given_UtcInstant_When_SavedAndReadBack_...` (`Kind` is `Unspecified`), `Given_LocalOrUnspecifiedDateTime_...` (both cases) |
| 2 | Seed timestamps at 09:00 UTC on the date instead of 09:00 Sydney | `..._Then_TimestampsAre0900SydneyOnPreviousUtcDay` |
| 3 | Seed with a hard-coded UTC+11 offset | `..._Then_TimestampsAre0900SydneyOnPreviousUtcDay` (the AEST value is an hour off) |
| 4 | Seed ids with `Guid.NewGuid()` | `..._GrantsMatchSpec`, `..._TasksMatchSpec`, `..._TimestampsAre0900Sydney...` (ordered by id), `Given_SameToday_When_SeedingTwoDatabases_...` |
| 5 | Seed versions not fixed (left as `Guid.NewGuid()`) | `Given_SameToday_When_SeedingTwoDatabases_...` |
| 6 | Remove rule 2 (adding a task renews the grant's version) | `Given_SavedGrant_When_TaskIsAdded...`, `Given_GrantLoadedInTwoContexts_...` |
| 7 | Remove rule 1 (a modified grant or task gets a new version) | `Given_SavedTask_When_ModifiedAndSaved_...`, `Given_TaskLoadedInTwoContexts_...` (the close then fails on the check constraint instead of a version conflict) |
| 8 | Remove the check constraint (model and migration) | `Given_TaskRow_When_SettingBothCompletedAndCancelled...` |
| 9 | Store priority as a string (model and migration) | `Given_TasksOfEachPriority_...` |
| 10 | `IsOverdue` expression uses `<=` | `Given_TasksInEveryState_...` |
| 11 | Seed GR-100001's High task due at `today - 2` | `..._Then_TasksMatchSpec` |
| 12 | Seed even when the database isn't empty | `Given_SeededDatabase_When_SeedingAgain_...` |
| 13 | Remove the unique index on `Reference` (model and migration) | `Given_ExistingReference_When_SavingAnotherGrantWithSameReference_...` |

After restoring, `npm test` passed twice: 60 passed, 0 failed (42 from Parts 1 to 3, and 18 from Part 4).

Notes:
- EF wraps the converter's refusal in a `DbUpdateException`; the converter's `InvalidOperationException` is the inner exception. Part 5's error handler will see the `DbUpdateException` and return a 500, which is right for a programming error.
- Rule 2 skips a grant that is itself being added. Its version is new anyway, and renewing it would overwrite the seed's fixed versions. §3.5's wording ("its parent Grant also gets a new Version") doesn't say which case applies; this is the reading I used.
- Keys are configured as never generated by EF. Without this, EF treats a new task with a preset id, found in a loaded grant's task list, as an existing row, and saves it as an UPDATE that fails.

Changes from the plan:
- New file `DesignTimeDbContextFactory.cs`, not listed in the plan. `dotnet ef` needs a way to create the DbContext, and the API doesn't register it until Part 5. The factory is the standard way and keeps the migration tooling independent of the API's start-up.
- New file `SqliteTestDatabase.cs` is the test helper the plan describes.

---

### Part 5: API: read endpoints, errors, start-up

**Builds:**
- `Program.cs`:
  - Dependency injection, including `TimeProvider.System` and `IAppClock`.
  - The DbContext, using `ConnectionStrings:Default` (a SQLite file with default settings, §6.2).
  - JSON settings: enums as strings, `DateOnly` as `YYYY-MM-DD`, `DateTime` as UTC with `Z` (§5).
- Start-up: migrations and seeding run in a hosted service. This lets the health-check test in Part 8 start the app without a working database. If the database can't be reached at start-up, the app fails to start.
- The exception handler and ProblemDetails mapping (§5.1).
- `GET /grants?status=`, `GET /grants/{id:guid}` and `GET /tasks/overdue` (§5), with their DTOs.
- A test factory: `WebApplicationFactory` with an in-memory SQLite database per test and `FakeTimeProvider` pinned to `2026-03-10T22:00:00Z`.

**Done when:**
- The three read endpoints return the §5 shapes for the seed data, with the literal dates of the pinned day.
- Every error body has the §5.1 shape.

**Tests** (`tests/GrantTracker.Tests/Api/`):

| Test | Covers |
|---|---|
| `Given_SeedData_When_ListingGrants_Then_ReturnsAllGrantsWithPendingTaskCounts` (2, 2, 0, 0) | §5 `pendingTaskCount`, which counts Pending only (§7) |
| `Given_StatusFilter_When_ListingGrants_Then_ReturnsOnlyMatchingGrants` (Theory: Open, Closed) | §5 status filter |
| `Given_InvalidStatus_When_ListingGrants_Then_Returns400ProblemDetails` | §5, §5.1 |
| `Given_PinnedClock_When_GettingGrant_Then_TodayIsSydneyDate` (`"2026-03-11"`, not `"2026-03-10"`) | §4.0 end to end: catches an accidental switch to UTC |
| `Given_SeededGrant_When_GettingIt_Then_DatesAreLiteralSydneyDatesAndInstantsEndWithZ` (GR-100001 `applicationDate` `"2026-01-10"`, a task `dueDate` `"2026-03-08"`; GR-100003 `closedAtUtc` `"2025-10-16T22:00:00Z"`) | §3.6, §5 date serialisation, §8 seed |
| `Given_ExistingGrant_When_GettingIt_Then_TasksHaveStatusAndIsOverdue` | §5, BR-6 `isOverdue` |
| `Given_UnknownOrNonGuidId_When_GettingGrant_Then_Returns404ProblemDetails` (Theory) | §5.1, §7 |
| `Given_SeedData_When_ListingOverdueTasks_Then_MostOverdueFirstAndCompletedExcluded` | BR-6 ordering and exclusion |
| `Given_TasksWithSameDueDate_When_ListingOverdueTasks_Then_HigherPriorityFirstThenId` | BR-6 tie-breaking (§4) |
| `Given_TaskDueToday_When_ClockPassesSydneyMidnight_Then_TaskAppearsInOverdueList` | BR-6 end to end with the Sydney clock |
| `Given_UnhandledException_When_Requesting_Then_Returns500WithoutExceptionDetails` | §5.1. A test-only endpoint throws. |
| `Given_AnyError_When_Returned_Then_BodyHasTypeCodeTitleStatusDetailInstanceAndTraceId` | §5.1 consistent error body |
| `Given_InvalidTimeZoneId_When_AppStarts_Then_StartupFails` | §4.0 fail fast |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| `AppClock.Today` uses the UTC date | `Given_PinnedClock_When_GettingGrant_Then_TodayIsSydneyDate`, `..._DatesAreLiteralSydneyDates...` |
| Remove the UTC converter | `..._InstantsEndWithZ` |
| Overdue ordered by due date descending | `Given_SeedData_When_ListingOverdueTasks_...` |
| `pendingTaskCount` counts all tasks | `Given_SeedData_When_ListingGrants_...` |
| Include exception details in the 500 body | `Given_UnhandledException_...` |
| Skip time-zone validation at start-up | `Given_InvalidTimeZoneId_...` |

**Build record:** Built on 2026-11-13.

What was built:

| File | What it holds |
|---|---|
| `src/GrantTracker.Api/Program.cs` | Adds JSON enums as strings; `AddProblemDetails` with `ProblemTypes.ApplyDefaults`; `ApiExceptionHandler`; the DbContext from `ConnectionStrings:Default` (start-up fails if it isn't set); the `DatabaseInitializer` hosted service; `GrantQueryService`. Pipeline: `UseExceptionHandler`, `UseStatusCodePages` (so a route that doesn't match, such as a non-Guid id, also gets a ProblemDetails body), `MapControllers`. |
| `src/GrantTracker.Api/appsettings.json` | `"ConnectionStrings": { "Default": "Data Source=granttracker.db" }` (`*.db` is already in `.gitignore`). |
| `src/GrantTracker.Api/Startup/DatabaseInitializer.cs` | Hosted service: applies the migrations, then `SeedData.SeedAsync` with `IAppClock.Today` and the configured zone. An exception stops the app from starting. |
| `src/GrantTracker.Api/Errors/ApiExceptionHandler.cs` | `NotFoundException` → 404 `not-found`. Anything else → logged, then 500 `internal`, "An unexpected error occurred.", with a fixed `detail` and nothing from the exception. Bodies are built with `ProblemTypes.Create`. The BR, validation, duplicate and concurrency mappings come in Parts 6 and 7, with their tests. |
| `src/GrantTracker.Api/Errors/ProblemTypes.cs` | The codes `validation`, `not-found`, `internal`; `TypeFor(code)` = `https://granttracker.example/problems/<code>`; `Create(status, code, title, detail)`, which sets `type` and the `code` extension field together. `ApplyDefaults` runs for every ProblemDetails body (including the framework's own): for 400, 404 and 500 it replaces the framework's RFC link with our `type` and `code`, and it fills in `detail`, `instance` (the request path) and `traceId` when they're missing. Other framework statuses (for example 405) keep their RFC link and get no `code` (DESIGN.md §5.1). |
| `src/GrantTracker.Api/Errors/NotFoundException.cs` | `Title` ("Grant not found") and the message as `detail`. |
| `src/GrantTracker.Api/Contracts/*.cs` | The response shapes (see below): `GrantListItemDto` (one row of the grant list), `GrantDto` (one grant with its tasks), `TaskDto`, `OverdueTaskDto`. |
| `src/GrantTracker.Api/Services/GrantQueryService.cs` | `ListGrantsAsync(status)`, `GetGrantAsync(id)`, `ListOverdueTasksAsync()`. No tracking. Pending counts and the overdue filter use `GrantTaskQueries` in SQL. |
| `src/GrantTracker.Api/Controllers/GrantsController.cs`, `TasksController.cs` | `GET /grants?status=`, `GET /grants/{id:guid}`, `GET /tasks/overdue`. Thin: bind, call the service, return the DTO. |
| `tests/GrantTracker.Tests/GrantTracker.Tests.csproj` | Adds `Microsoft.AspNetCore.Mvc.Testing` 10.0.15. |

Response shapes. §5 names the required fields (`pendingTaskCount`, `today`, `status`, `isOverdue`, the grant reference and organisation on overdue items); the rest follow the data model in §3:

| Endpoint | Fields |
|---|---|
| `GET /grants` (ordered by reference) | `id`, `reference`, `organisationName`, `applicationDate`, `status`, `closedAtUtc`, `pendingTaskCount` |
| `GET /grants/{id}` | `id`, `reference`, `organisationName`, `applicationDate`, `status`, `closedNote`, `closedAtUtc`, `pendingTaskCount`, `today`, `tasks` (ordered by due date, then priority High first, then id) |
| Each task | `id`, `grantId`, `title`, `dueDate`, `priority`, `status`, `isOverdue`, `createdAtUtc`, `completedAtUtc`, `cancelledAtUtc` |
| `GET /tasks/overdue` | `id`, `grantId`, `grantReference`, `organisationName`, `title`, `dueDate`, `priority` |

Test helpers (`tests/GrantTracker.Tests/Api/`):
- `GrantTrackerApiFactory.cs`: `WebApplicationFactory<Program>` with one open in-memory SQLite connection per factory, replacing the app's DbContext options, and `FakeTimeProvider` pinned to `2026-03-10T22:00:00Z` (exposed as `Clock`). The app's own `DatabaseInitializer` migrates and seeds it, so tests run against the real start-up. Optional setting overrides (used for `App:TimeZoneId`). `UpdateDatabase` adds data directly, for states that no endpoint can create until Part 6. Also holds `ResponseJson`, which reads bodies as JSON so tests assert on the exact text the client gets.
- `ThrowingTestController.cs`: the test-only endpoint `GET /test/throw`, added to the app only in tests.

Tests: 13 tests, 18 cases.

| File | Test | Covers |
|---|---|---|
| `GrantReadEndpointTests.cs` | `Given_SeedData_When_ListingGrants_Then_ReturnsAllGrantsWithPendingTaskCounts` (GR-100001: 2, GR-100002: 2, GR-100003: 0, GR-100004: 0) | §5 `pendingTaskCount`, Pending only (§7) |
| `GrantReadEndpointTests.cs` | `Given_StatusFilter_When_ListingGrants_Then_ReturnsOnlyMatchingGrants` (Theory: Open → GR-100001, 100002, 100004; Closed → GR-100003) | §5 status filter |
| `GrantReadEndpointTests.cs` | `Given_InvalidStatus_When_ListingGrants_Then_Returns400ProblemDetails` (`?status=Archived` → `type` `https://granttracker.example/problems/validation`, `code` `validation`, error keyed on `status`) | §5, §5.1 |
| `GrantReadEndpointTests.cs` | `Given_PinnedClock_When_GettingGrant_Then_TodayIsSydneyDate` (`"2026-03-11"`) | §4.0 end to end |
| `GrantReadEndpointTests.cs` | `Given_SeededGrant_When_GettingIt_Then_DatesAreLiteralSydneyDatesAndInstantsEndWithZ` (`applicationDate` `"2026-01-10"`, `dueDate` `"2026-03-08"`, `createdAtUtc` `"2026-01-09T22:00:00Z"`, GR-100003 `closedAtUtc` `"2025-10-16T22:00:00Z"`) | §3.6, §5 date serialisation, §8 seed |
| `GrantReadEndpointTests.cs` | `Given_ExistingGrant_When_GettingIt_Then_TasksHaveStatusAndIsOverdue` (GR-100001: Check eligibility documents Completed/not overdue, Verify bank details Pending/overdue, Request quote for tools Pending/not overdue, in that order) | §5, BR-6 `isOverdue` |
| `GrantReadEndpointTests.cs` | `Given_UnknownOrNonGuidId_When_GettingGrant_Then_Returns404ProblemDetails` (Theory: unknown Guid, `not-a-guid`; `type` `https://granttracker.example/problems/not-found`, `code` `not-found`) | §5.1, §7 |
| `OverdueTasksEndpointTests.cs` | `Given_SeedData_When_ListingOverdueTasks_Then_MostOverdueFirstAndCompletedExcluded` (Verify bank details 2026-03-08, then Review progress report 2026-03-10, with grant reference and organisation) | BR-6 ordering and exclusion |
| `OverdueTasksEndpointTests.cs` | `Given_TasksWithSameDueDate_When_ListingOverdueTasks_Then_HigherPriorityFirstThenId` (four tasks due 2026-03-09: High A (id …01), High B (…03), Medium, Low) | BR-6 tie-breaking |
| `OverdueTasksEndpointTests.cs` | `Given_TaskDueToday_When_ClockPassesSydneyMidnight_Then_TaskAppearsInOverdueList` (due 2026-03-11: absent at `2026-03-11T12:59Z`, present at `13:00Z`) | BR-6 end to end with the Sydney clock |
| `ErrorResponseTests.cs` | `Given_UnhandledException_When_Requesting_Then_Returns500WithoutExceptionDetails` (no message, exception type or class name in the body) | §5.1 |
| `ErrorResponseTests.cs` | `Given_AnyError_When_Returned_Then_BodyHasTypeCodeTitleStatusDetailInstanceAndTraceId` (Theory: 400, 404 unknown id, 404 non-Guid, 500; literal `type` URI and `code` for each) | §5.1 consistent error body |
| `StartupTests.cs` | `Given_InvalidTimeZoneId_When_AppStarts_Then_StartupFails` (`OptionsValidationException`, with `DatabaseInitializer` removed) | §4.0 fail fast |

Mutation checks. Each was applied, all tests were run, and the file was restored. Checks 6b and 7 to 13 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | `AppClock.Today` uses the UTC date | `Given_PinnedClock_..._TodayIsSydneyDate`, `..._DatesAreLiteralSydneyDates...`, all three overdue tests, and the clock tests from Part 3 |
| 2 | Remove the UTC converter | `..._InstantsEndWithZ`, and the Part 4 converter tests |
| 3 | Overdue ordered by due date descending | `Given_SeedData_When_ListingOverdueTasks_...`, `Given_TasksWithSameDueDate_...` |
| 4 | `pendingTaskCount` counts all tasks | `Given_SeedData_When_ListingGrants_...` |
| 5 | Include exception details in the 500 body | `Given_UnhandledException_...` |
| 6 | Skip time-zone validation at start-up (`ValidateOnStart` removed) | `Given_InvalidTimeZoneId_...` (see the note below) |
| 6b | No time-zone validation at all | `Given_InvalidTimeZoneId_...` (start-up then fails with `TimeZoneNotFoundException`, not the clear options error) |
| 7 | Status filter ignored | `Given_StatusFilter_...` (both cases) |
| 8 | Overdue tie-break without priority | `Given_TasksWithSameDueDate_...` |
| 9 | `isOverdue` always false | `Given_ExistingGrant_..._TasksHaveStatusAndIsOverdue` |
| 10 | 404 keeps the framework's RFC link as `type` | `Given_UnknownOrNonGuidId_...` (`not-a-guid`) |
| 11 | `instance` not filled in | `Given_AnyError_...` (all four cases) |
| 12 | `UseStatusCodePages` removed | `Given_UnknownOrNonGuidId_...` (`not-a-guid`), `Given_AnyError_...` (`not-a-guid`) |
| 13 | `NotFoundException` not mapped (falls through to 500) | `Given_UnknownOrNonGuidId_...` (unknown Guid), `Given_AnyError_...` (unknown Guid) |
| 14 | `type` is the bare code, without the base URI | Every `type` assertion: `Given_AnyError_...` (all four cases), `Given_UnhandledException_...`, `Given_InvalidStatus_...`, `Given_UnknownOrNonGuidId_...` (both cases) |
| 15 | `Create` doesn't send the `code` field | `Given_AnyError_...` (unknown Guid, 500), `Given_UnhandledException_...`, `Given_UnknownOrNonGuidId_...` (unknown Guid) |
| 16 | The framework's own bodies get our `type` but no `code` | `Given_AnyError_...` (400, non-Guid), `Given_InvalidStatus_...`, `Given_UnknownOrNonGuidId_...` (`not-a-guid`) |

Note on check 6: on the first run it **survived**. `DatabaseInitializer` reads the time-zone setting at start-up, so the app still failed with the same options error even without `ValidateOnStart`. The test now removes `DatabaseInitializer`, so only the options check can stop the app, and check 6 fails it. This matters for Part 8, where the health-check test starts the app without the database start-up.

Change after review (2026-11-13, option C): the problem `type` is now an absolute URI, `https://granttracker.example/problems/<code>`, and every error body also carries the short `code` as an extension field (DESIGN.md §5.1). Nothing is published at that address; this is recorded as limitation §10.8. Checks 5 and 10 to 13 were re-run against the new code with the same results, and checks 14 to 16 were added. Afterwards all tests passed twice: 78 passed, 0 failed. The DTOs were also renamed after review: `GrantSummaryDto` → `GrantListItemDto`, `GrantDetailDto` → `GrantDto`.

Manual check: `dotnet run` against a scratch database file migrated, seeded with Sydney's real date (`today` `"2026-11-13"`) and served the three endpoints. A non-Guid id returned the ProblemDetails 404. The scratch file was deleted.

After restoring, `npm test` passed twice: 78 passed, 0 failed (60 from Parts 1 to 4, and 18 from Part 5).

Changes from the plan:
- The DTO fields and the order of a grant's tasks weren't spelled out in §5. They're listed above for review, and can be copied into §5 once agreed.
- The exception handler maps only what Part 5 can test (404 and 500). The other §5.1 rows are added in Parts 6 and 7, with the tests that prove them.
- The start-up test removes `DatabaseInitializer` (see the note on check 6).

---

### Part 6: API: write endpoints (BR-1 to BR-5 through the API)

**Builds:**
- Application services and controllers for:
  - `POST /grants/{id}/tasks`. The body includes `id`, which is required (§5). Repeated creates are handled in Part 7.
  - `PUT /tasks/{id}/complete`.
  - `PUT /grants/{id}/close`, with an optional `note`.
- Input-shape validation: `id` missing or `Guid.Empty`, an unknown priority, or a malformed date (§6.3 step 1).
- Mapping of each exception to the §5.1 status, `type` URI and `code`.

**Done when:** each rule outcome reaches the client with the right status code, `code` and field key. Boundaries were already tested in Parts 1 and 2.

**Tests** (`tests/GrantTracker.Tests/Api/`; the clock is pinned, so `today` is 2026-03-11):

| Test | Covers |
|---|---|
| `Given_OpenGrant_When_PostingValidTask_Then_Returns201WithLocationAndTaskIsSaved` | §5 create |
| `Given_ClosedGrant_When_PostingTask_Then_Returns409Br1` (GR-100003) | BR-1 through the API |
| `Given_DueDateIsUtcDateButNotSydneyDate_When_PostingTask_Then_Returns400KeyedOnDueDate` (`"2026-03-10"`) | BR-2 through the API. This date is "today" in UTC but yesterday in Sydney, so it also catches an accidental switch to UTC. |
| `Given_DueDateIsSydneyToday_When_PostingTask_Then_Returns201` (`"2026-03-11"`) | BR-2 boundary through the API |
| `Given_DueDateBeforeApplicationDate_When_PostingTask_Then_Returns400KeyedOnDueDate` | BR-2 through the API |
| `Given_InvalidTaskTitle_When_PostingTask_Then_Returns400KeyedOnTitle` (Theory: whitespace, 101 characters) | BR-3 through the API |
| `Given_InvalidTaskTitleAndInvalidDueDate_When_PostingTask_Then_Returns400WithBothFieldErrors` | §4 order of checks |
| `Given_MissingOrEmptyId_When_PostingTask_Then_Returns400KeyedOnId` (Theory) | §6.3 step 1 |
| `Given_UnknownPriorityOrMalformedDate_When_PostingTask_Then_Returns400` (Theory) | §7 invalid input |
| `Given_UnknownGrant_When_PostingTask_Then_Returns404` | §5 |
| `Given_PendingTask_When_Completing_Then_Returns200WithCompletedAtUtcFromClock` (`"2026-03-10T22:00:00Z"`) | BR-4 |
| `Given_CompletedTask_When_CompletingAgainAfterClockAdvances_Then_Returns200WithOriginalCompletedAtUtc` | BR-4 repeat, OP-3 |
| `Given_CancelledTask_When_Completing_Then_Returns409GrantClosed` | §4.7 |
| `Given_CompletedTaskOnClosedGrant_When_Completing_Then_Returns200AndNothingChanges` (GR-100003) | BR-4, §4.7 |
| `Given_PendingHighTask_When_ClosingGrant_Then_Returns409Br5WithBlockingTaskIds` (GR-100001) | BR-5 through the API |
| `Given_PendingLowAndMediumTasks_When_ClosingGrantWithNote_Then_Returns200AndTasksAreCancelled` (GR-100002) | BR-5 close path, §8 demo |
| `Given_ClosedGrant_When_ClosingAgain_Then_Returns200AndNothingChanges` | BR-5 repeat, OP-3 |
| `Given_UnknownTaskOrGrant_When_CompletingOrClosing_Then_Returns404` (Theory) | §5 |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Map BR-1 to 400 instead of 409 | `Given_ClosedGrant_When_PostingTask_Then_Returns409Br1` |
| The service passes the UTC date as `today` | `Given_DueDateIsUtcDateButNotSydneyDate_...` |
| Key the BR-2 error on `title` | `Given_DueDateIsUtcDateButNotSydneyDate_..._KeyedOnDueDate` |
| Return 409 for an already-completed task | `Given_CompletedTask_When_CompletingAgain...` |
| Return 409 when closing an already-closed grant | `Given_ClosedGrant_When_ClosingAgain_...` |

**Build record:** Built on 2026-11-13.

Decided with you before building (DESIGN.md updated, Decisions 13 and 14):
- **Close note length.** `Grant.Close` rejects a note over 1000 characters after trimming with a validation error keyed on `note` (400). It's checked after BR-5, like BR-1 before the field rules when adding a task. Before this, nothing enforced §3.1's "max 1000", because SQLite ignores column lengths.
- **`Location` on 201.** There's no endpoint for a single task, so `Location` is `/grants/{grantId}`, where the new task is listed.

What was built:

| File | What it holds |
|---|---|
| `src/GrantTracker.Domain/Grant.cs` | `ClosedNoteMaxLength = 1000`, and the note check in `Close` (after BR-5, before any change). |
| `src/GrantTracker.Infrastructure/Configurations/GrantConfiguration.cs` | `ClosedNote` max length uses `Grant.ClosedNoteMaxLength`. Same value, so the model and migration are unchanged. |
| `src/GrantTracker.Api/Contracts/CreateTaskRequest.cs` | `id`, `title`, `dueDate`, `priority`, all nullable, so a missing field is a field error rather than a JSON error. |
| `src/GrantTracker.Api/Contracts/CloseGrantRequest.cs` | `note`, optional. |
| `src/GrantTracker.Api/Services/GrantCommandService.cs` | `AddTaskAsync` (loads the grant), `CompleteTaskAsync` (loads the task with its grant, which `Complete` needs for §4.7), `CloseGrantAsync` (loads the grant with its tasks, which BR-5 and the cancelling need). Each takes `today` and `nowUtc` from `IAppClock`, calls the domain, saves, and returns a DTO. No rules; no retry yet (Part 7). |
| `src/GrantTracker.Api/Controllers/GrantsController.cs` | `POST /grants/{id:guid}/tasks`: §6.3 step 1 checks (`id` present and not empty, `dueDate` and `priority` present), then 201 with `Location: /grants/{id}`. `PUT /grants/{id:guid}/close`: the body parameter is nullable, so an empty body is allowed. |
| `src/GrantTracker.Api/Controllers/TasksController.cs` | `PUT /tasks/{id:guid}/complete`. |
| `src/GrantTracker.Api/Errors/ApiExceptionHandler.cs` | Adds `DomainValidationException` → 400 `validation` with the field errors, and `BusinessRuleException` → 409 with the rule's `code` (`BR-1`, `grant-closed`, `BR-5`), the §5.1 title, the exception message as `detail`, and the exception's extension fields (BR-5's `blockingTaskIds`). An unknown rule code gets a generic title; no such code exists today. |
| `src/GrantTracker.Api/Errors/ProblemTypes.cs` | Adds `CreateValidation(errors)`. |
| `src/GrantTracker.Api/Errors/InvalidInputResponse.cs` | The 400 for input the JSON reader can't read. ASP.NET Core keys those errors on the JSON path (`$.dueDate`) with a message naming .NET types; this keys them on the field (`dueDate`) with "The value isn't in the expected format." |
| `src/GrantTracker.Api/Program.cs` | Enum names only (`JsonStringEnumConverter(allowIntegerValues: false)`), so `"priority": 3` is a 400 rather than High. The implicit "required" check for non-nullable references is off, because the DTOs mark optional fields with `?` and the check added a misleading `request` error. `InvalidInputResponse` is the invalid-input response. Registers `GrantCommandService`. |
| `tests/GrantTracker.Tests/Api/GrantTrackerApiFactory.cs` | `ResponseJson` renamed `ApiJson`, with `Body(json)` for request bodies written as literal JSON. |

Found by a probe before writing the input tests (the probe test was deleted afterwards): `"priority": 3` was accepted as High, and a malformed date or priority came back keyed `$.dueDate` / `$.priority` with a .NET type name in the message, plus a stray `request` error. Both were fixed as above, and both have tests.

Tests: 3 domain tests (3 cases) and 21 API tests (27 cases), plus 2 API tests (6 cases) added after review (below).

| File | Test | Covers |
|---|---|---|
| `Domain/GrantCloseTests.cs` | `Given_NoteOf1000CharactersAfterTrimming_When_Closing_Then_GrantIsClosedWithNote` | Close note boundary (accepted) |
| `Domain/GrantCloseTests.cs` | `Given_NoteOf1001CharactersAfterTrimming_When_Closing_Then_ThrowsValidationOnNoteAndNothingChanges` | Close note boundary (rejected; grant, note and tasks unchanged) |
| `Domain/GrantCloseTests.cs` | `Given_PendingHighTaskAndTooLongNote_When_Closing_Then_ThrowsBr5` | BR-5 is checked before the note |
| `Api/CreateTaskEndpointTests.cs` | `Given_OpenGrant_When_PostingValidTask_Then_Returns201WithLocationAndTaskIsSaved` (`Location` `/grants/{id}`, trimmed title, `createdAtUtc` `"2026-03-10T22:00:00Z"`, listed on the grant) | §5 create |
| `Api/CreateTaskEndpointTests.cs` | `Given_ClosedGrant_When_PostingTask_Then_Returns409Br1` (GR-100003; `type`, `code` `BR-1`, title "Grant is closed") | BR-1 through the API |
| `Api/CreateTaskEndpointTests.cs` | `Given_DueDateIsUtcDateButNotSydneyDate_When_PostingTask_Then_Returns400KeyedOnDueDate` (`"2026-03-10"`) | BR-2; catches a switch to the UTC date |
| `Api/CreateTaskEndpointTests.cs` | `Given_DueDateIsSydneyToday_When_PostingTask_Then_Returns201` (`"2026-03-11"`) | BR-2 boundary |
| `Api/CreateTaskEndpointTests.cs` | `Given_DueDateBeforeApplicationDate_When_PostingTask_Then_Returns400KeyedOnDueDate` (a grant with application date 2026-03-20, due 2026-03-19; only the application-date message) | BR-2 |
| `Api/CreateTaskEndpointTests.cs` | `Given_InvalidTaskTitle_When_PostingTask_Then_Returns400KeyedOnTitle` (Theory: whitespace, 101 characters) | BR-3 |
| `Api/CreateTaskEndpointTests.cs` | `Given_InvalidTaskTitleAndInvalidDueDate_When_PostingTask_Then_Returns400WithBothFieldErrors` | §4 order of checks |
| `Api/CreateTaskEndpointTests.cs` | `Given_MissingOrEmptyId_When_PostingTask_Then_Returns400KeyedOnId` (Theory: missing, empty GUID) | §6.3 step 1 |
| `Api/CreateTaskEndpointTests.cs` | `Given_UnknownPriorityOrMalformedDate_When_PostingTask_Then_Returns400KeyedOnField` (Theory: `"Urgent"`, `3`, `"2026-3-20"`, `"20/03/2026"`, `"2026-02-30"`) | §6.3 step 1, §7 invalid input |
| `Api/CreateTaskEndpointTests.cs` | `Given_UnknownGrant_When_PostingTask_Then_Returns404` | §5 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_PendingTask_When_Completing_Then_Returns200WithCompletedAtUtcFromClock` (`"2026-03-10T22:00:00Z"`) | BR-4 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_CompletedTask_When_CompletingAgainAfterClockAdvances_Then_Returns200WithOriginalCompletedAtUtc` | BR-4 repeat, OP-3 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_CancelledTask_When_Completing_Then_Returns409GrantClosed` (GR-100002 closed first) | §4.7 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_CompletedTaskOnClosedGrant_When_Completing_Then_Returns200AndNothingChanges` (GR-100003, `"2025-10-10T22:00:00Z"` kept) | BR-4, §4.7 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_PendingHighTask_When_ClosingGrant_Then_Returns409Br5WithBlockingTaskIds` (GR-100001; only "Verify bank details" blocks; grant still Open) | BR-5 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_PendingLowAndMediumTasks_When_ClosingGrantWithNote_Then_Returns200AndTasksAreCancelled` (GR-100002; note trimmed; both tasks Cancelled at `"2026-03-10T22:00:00Z"`, not overdue; `pendingTaskCount` 0) | BR-5 close path, §8 demo |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_ClosedGrant_When_ClosingAgain_Then_Returns200AndNothingChanges` (GR-100003; note stays null, `closedAtUtc` `"2025-10-16T22:00:00Z"`) | BR-5 repeat, OP-3 |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_NoBody_When_ClosingGrantWithoutTasks_Then_Returns200WithNoNote` (GR-100004) | The body is optional |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_NoteOver1000Characters_When_ClosingGrant_Then_Returns400KeyedOnNote` | Close note limit through the API |
| `Api/CompleteAndCloseEndpointTests.cs` | `Given_UnknownTaskOrGrant_When_CompletingOrClosing_Then_Returns404` (Theory: "Task not found", "Grant not found") | §5 |

Mutation checks. Each was applied, all tests were run, and the file was restored. Checks 6 to 17 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | Map BR-1 to 400 instead of 409 | `Given_ClosedGrant_When_PostingTask_Then_Returns409Br1` |
| 2 | The service passes the UTC date as `today` | `Given_DueDateIsUtcDateButNotSydneyDate_...`, `Given_InvalidTaskTitleAndInvalidDueDate_...` |
| 3 | Key the BR-2 error on `title` | `Given_DueDateIsUtcDateButNotSydneyDate_...`, `Given_DueDateBeforeApplicationDate_...`, `Given_InvalidTaskTitleAndInvalidDueDate_...` (API), and three Part 1 domain tests |
| 4 | Return 409 for an already-completed task | `Given_CompletedTask_When_CompletingAgain...`, `Given_CompletedTaskOnClosedGrant_...` (API), and two Part 2 domain tests |
| 5 | Return 409 when closing an already-closed grant | `Given_ClosedGrant_When_ClosingAgain_...` (API and domain) |
| 6 | Note limit off by one (1000 rejected) | `Given_NoteOf1000CharactersAfterTrimming_...` |
| 7 | No note length check | `Given_NoteOf1001CharactersAfterTrimming_...`, `Given_NoteOver1000Characters_When_ClosingGrant_...` |
| 8 | BR-5 extension fields not copied (no `blockingTaskIds`) | `Given_PendingHighTask_When_ClosingGrant_...` |
| 9 | `DomainValidationException` not mapped (500) | All five API validation tests for BR-2, BR-3 and the note |
| 10 | No `Location` header on 201 | `Given_OpenGrant_When_PostingValidTask_...` |
| 11 | Enum numbers allowed | `Given_UnknownPriorityOrMalformedDate_...` (`3`) |
| 12 | Unreadable input keyed on the JSON path | `Given_UnknownPriorityOrMalformedDate_...` (all five cases) |
| 13 | Implicit "required" check left on | `Given_UnknownPriorityOrMalformedDate_...` (all five cases: the extra `request` error) |
| 14 | Empty GUID accepted as `id` | `Given_MissingOrEmptyId_...` (empty GUID) |
| 15 | Complete loads the task without its grant | `Given_PendingTask_When_Completing_...`, `Given_CompletedTask_When_CompletingAgain...`, `Given_CancelledTask_...` |
| 16 | Close loads the grant without its tasks | `Given_PendingHighTask_When_ClosingGrant_...`, `Given_PendingLowAndMediumTasks_...` |
| 17 | Close body required (parameter not nullable) | `Given_NoBody_When_ClosingGrantWithoutTasks_...`, `Given_UnknownTaskOrGrant_...` (close) |
| 18 | `dueDate` "required" check removed | `Given_MissingTaskTitleDueDateOrPriority_...` (missing and null `dueDate`) |
| 19 | `priority` "required" check removed | `Given_MissingTaskTitleDueDateOrPriority_...` (missing and null `priority`) |
| 20 | `pendingTaskCount` counts cancelled tasks (only "not completed") | `Given_GrantClosedWithCancelledTasks_When_ListingGrants_Then_PendingTaskCountIsZero` |

Gaps found in review (2026-11-13). You asked whether a missing `dueDate` or `priority` was tested, and suggested a list test with cancelled tasks. Checks 18 to 20 were first run against the tests as they were, and **all three survived**:
- Without the `dueDate` or `priority` check, a missing value crashes on `.Value` and returns a 500, and no test noticed. Only the `id` check had a test.
- The list test's only zero count was GR-100003, whose task is *completed*. No test had a *cancelled* task, so counting cancelled tasks as pending went unnoticed.

Two tests were added (6 cases), and checks 18 to 20 now fail them:

| File | Test | Covers |
|---|---|---|
| `Api/CreateTaskEndpointTests.cs` | `Given_MissingTaskTitleDueDateOrPriority_When_PostingTask_Then_Returns400KeyedOnField` (Theory: `dueDate` missing or null → "Due date is required."; `priority` missing or null → "Priority is required."; `title` missing → "Title is required."; no task created) | §6.3 step 1, BR-3 |
| `Api/GrantReadEndpointTests.cs` | `Given_GrantClosedWithCancelledTasks_When_ListingGrants_Then_PendingTaskCountIsZero` (GR-100002 closed through the API, then listed: Closed, `pendingTaskCount` 0) | §5 `pendingTaskCount`, §7 (Pending only) |

The missing-`title` case had the same gap at the API level, although the domain already tested it (Part 1), so it was added to the same Theory.

Notes on the first run:
- Check 7 was first written as a pattern that can never match, and the compiler rejected it (CS8518). It was re-run with a condition the compiler can't evaluate.
- Check 17 first removed `[FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]` from the close action, and **survived**: since .NET 7, a nullable body parameter already accepts an empty body, so the attribute did nothing. It was removed, and check 17 now makes the parameter non-nullable, which the tests catch.

After restoring, `npm test` passed twice: 114 passed, 0 failed (78 from Parts 1 to 5, and 36 from Part 6, including the 6 cases added after review).

Changes from the plan:
- `Given_UnknownPriorityOrMalformedDate_..._Then_Returns400` is named `..._Then_Returns400KeyedOnField`, because it now also checks the field key (§6.3 step 1 says these errors are keyed on the field).
- Added tests: the three domain note tests, `Given_NoBody_When_ClosingGrantWithoutTasks_...` and `Given_NoteOver1000Characters_When_ClosingGrant_...`.
- New files not named in the plan: the two request DTOs, `GrantCommandService.cs`, `InvalidInputResponse.cs`, and the test file split (`CreateTaskEndpointTests.cs`, `CompleteAndCloseEndpointTests.cs`).
- A repeated create with the same task id currently fails with a 500 (primary-key violation). That's expected until Part 7, which adds the id lookup and the retry.

---

### Part 7: OP-2 retry and OP-3 safe task creation

**Builds:**
- The retry in the application services (§6.2):
  - Up to 3 attempts, with `ChangeTracker.Clear()` between attempts.
  - It retries on `DbUpdateConcurrencyException`, and on a primary-key violation when inserting a `GrantTask`.
  - After 3 failures it returns 409 `concurrency-conflict`.
- Task creation looks up the id before checking the rules, as in §6.3 steps 2 to 4. A different task with the same id gives 409 `duplicate-task-id`.
- Test helpers:
  - A race interceptor (`SaveChangesInterceptor`). Just before the request's first save, it saves a competing change through a separate DbContext, so each race happens the same way on every run.
  - An interceptor that causes a conflict on every attempt and counts the attempts.

**Done when:**
- Every row of the §6.2 race table has a test.
- Every OP-3 row of §6.5 has a test.

**Tests** (`tests/GrantTracker.Tests/Api/`):

| Test | Covers |
|---|---|
| `Given_TwoOfficersAddingTasksToSameGrant_When_Racing_Then_BothTasksAreSaved` | OP-2: no unnecessary 409 |
| `Given_HighTaskAddedFirst_When_CloseRaces_Then_CloseReturns409Br5` | OP-2 + BR-5 |
| `Given_GrantClosedFirst_When_AddRaces_Then_AddReturns409Br1` | OP-2 + BR-1 |
| `Given_LowTaskAddedFirst_When_CloseRaces_Then_CloseSucceedsAndNewTaskIsCancelled` | OP-2 |
| `Given_TaskCompletedFirst_When_CloseRaces_Then_CloseSucceedsAndCompletedTaskIsUntouched` | OP-2 |
| `Given_GrantClosedFirst_When_CompleteRaces_Then_CompleteReturns409GrantClosed` | OP-2 + §4.7 |
| `Given_TaskCompletedFirst_When_SecondCompleteRaces_Then_Returns200WithFirstCompletedAtUtc` | OP-2 + BR-4 |
| `Given_GrantClosedFirst_When_SecondCloseRaces_Then_Returns200AndFirstNoteIsKept` | OP-2 + BR-5 |
| `Given_ConflictOnEveryAttempt_When_ClosingGrant_Then_Returns409ConcurrencyConflictAfterThreeAttempts` | OP-2 retry limit |
| `Given_TaskCreated_When_SameRequestIsSentAgain_Then_Returns200WithSameBodyAndOneTaskExists` | OP-3 |
| `Given_TaskCreated_When_SameIdSentWithPaddedTaskTitle_Then_Returns200` | OP-3: values are compared after trimming |
| `Given_TaskCreated_When_SameIdSentWithDifferentDetails_Then_Returns409DuplicateTaskId` (Theory: title, due date, priority) | OP-3 |
| `Given_TaskCreated_When_SameIdSentToAnotherGrant_Then_Returns409DuplicateTaskId` | OP-3 |
| `Given_TaskCreatedDueToday_When_RepeatedAfterSydneyMidnight_Then_Returns200NotBr2` | OP-3: lookup before BR-2 |
| `Given_TaskCreatedThenGrantClosed_When_Repeated_Then_Returns200NotBr1` | OP-3: lookup before BR-1 |
| `Given_TaskCreatedThenCompleted_When_Repeated_Then_Returns200WithCompletedTask` | OP-3 (§6.5) |
| `Given_TwoIdenticalCreates_When_Racing_Then_OneTaskExistsAndResponsesAre201And200` | OP-3 + OP-2: the primary-key retry |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Max attempts 3 → 1 | `Given_TwoOfficersAddingTasks...`, `Given_HighTaskAddedFirst_...` |
| Don't clear the change tracker between attempts | The race tests (a retry reuses stale data) |
| Move the id lookup after the rule checks | `..._RepeatedAfterSydneyMidnight_...`, `..._ThenGrantClosed_When_Repeated_...` |
| Compare titles without trimming | `..._SameIdSentWithPaddedTaskTitle_...` |
| Don't retry on a primary-key violation | `Given_TwoIdenticalCreates_When_Racing_...` (it returns 500) |
| Treat any existing id as a match | `..._SameIdSentWithDifferentDetails_...` |

**Build record:** Built on 2026-11-13.

What was built:

| File | What it holds |
|---|---|
| `src/GrantTracker.Api/Services/GrantCommandService.cs` | `WithRetryAsync`: runs load → domain method → save up to `MaxAttempts = 3` times, calling `ChangeTracker.Clear()` after each conflict so the next attempt loads current data. A conflict is a `DbUpdateConcurrencyException`, or a `DbUpdateException` whose inner SQLite error is a primary-key violation (extended code 1555) on an added `GrantTask`. Nothing else is retried. After the third conflict it throws `ConcurrencyConflictException`. All three writes run inside it. `AddTaskAsync` now follows §6.3: look up the id on any grant; the same grant, trimmed title, due date and priority → the existing task as it is now, with `Created = false`; anything different → `DuplicateTaskIdException`; a new id → `Grant.AddTask` (BR-1, BR-2, BR-3). Returns `CreateTaskResult(Task, Created)`. |
| `src/GrantTracker.Api/Controllers/GrantsController.cs` | `POST /grants/{id}/tasks`: 201 with `Location` when created, 200 when the task already existed. |
| `src/GrantTracker.Api/Errors/DuplicateTaskIdException.cs` | → 409 `duplicate-task-id`, "This task id is already used by a task with different details". |
| `src/GrantTracker.Api/Errors/ConcurrencyConflictException.cs` | Wraps the last conflict after 3 attempts → 409 `concurrency-conflict`, "The record was changed by someone else. Reload and try again." It's what §5.1's "`DbUpdateConcurrencyException` after 3 attempts" row maps; it also covers the (unlikely) case where the third conflict was a primary-key violation. |
| `src/GrantTracker.Api/Errors/ApiExceptionHandler.cs`, `ProblemTypes.cs` | The two mappings and their codes. Every §5.1 row is now mapped. |

Test helper (`tests/GrantTracker.Tests/Api/CompetingSaveInterceptor.cs`): a `SaveChangesInterceptor` that the factory adds to every app DbContext (`factory.Race`). It does nothing until armed. Once armed, it runs a competing change just before the request under test saves: after the request has loaded its data, before its first save (or before every save, for the retry-limit test). The competing change is **another real request to the API**, so the other officer's side also goes through the endpoint, the domain and the version renewal. The request under test waits for it to finish, so they never use the one SQLite connection at the same moment. `RequestSaves` counts the request's save attempts. The plan named two interceptors; one with an "every save" switch does both jobs.

Tests: 17 tests, 19 cases.

| File | Test | Covers |
|---|---|---|
| `ConcurrencyRetryTests.cs` | `Given_TwoOfficersAddingTasksToSameGrant_When_Racing_Then_BothTasksAreSaved` (both 201; 2 save attempts; both tasks listed) | OP-2: no unnecessary 409 |
| `ConcurrencyRetryTests.cs` | `Given_HighTaskAddedFirst_When_CloseRaces_Then_CloseReturns409Br5` (grant still Open) | OP-2 + BR-5 |
| `ConcurrencyRetryTests.cs` | `Given_GrantClosedFirst_When_AddRaces_Then_AddReturns409Br1` (no task added) | OP-2 + BR-1 |
| `ConcurrencyRetryTests.cs` | `Given_LowTaskAddedFirst_When_CloseRaces_Then_CloseSucceedsAndNewTaskIsCancelled` | OP-2 |
| `ConcurrencyRetryTests.cs` | `Given_TaskCompletedFirst_When_CloseRaces_Then_CloseSucceedsAndCompletedTaskIsUntouched` (the other officer completes at 22:30 UTC; that `completedAtUtc` is kept, `cancelledAtUtc` null; the other task Cancelled) | OP-2 |
| `ConcurrencyRetryTests.cs` | `Given_GrantClosedFirst_When_CompleteRaces_Then_CompleteReturns409GrantClosed` (task Cancelled, never completed) | OP-2 + §4.7 |
| `ConcurrencyRetryTests.cs` | `Given_TaskCompletedFirst_When_SecondCompleteRaces_Then_Returns200WithFirstCompletedAtUtc` (`"2026-03-10T22:30:00Z"`, not this request's 22:00) | OP-2 + BR-4 |
| `ConcurrencyRetryTests.cs` | `Given_GrantClosedFirst_When_SecondCloseRaces_Then_Returns200AndFirstNoteIsKept` | OP-2 + BR-5 |
| `ConcurrencyRetryTests.cs` | `Given_ConflictOnEveryAttempt_When_ClosingGrant_Then_Returns409ConcurrencyConflictAfterThreeAttempts` (another officer adds a task before every save; `type`, `code`, title; exactly 3 attempts; grant still Open) | OP-2 retry limit |
| `SafeTaskCreationTests.cs` | `Given_TaskCreated_When_SameRequestIsSentAgain_Then_Returns200WithSameBodyAndOneTaskExists` (201 then 200, identical bodies) | OP-3 |
| `SafeTaskCreationTests.cs` | `Given_TaskCreated_When_SameIdSentWithPaddedTaskTitle_Then_Returns200` | OP-3: compared after trimming |
| `SafeTaskCreationTests.cs` | `Given_TaskCreated_When_SameIdSentWithDifferentDetails_Then_Returns409DuplicateTaskId` (Theory: title, due date, priority; the saved task is unchanged) | OP-3 |
| `SafeTaskCreationTests.cs` | `Given_TaskCreated_When_SameIdSentToAnotherGrant_Then_Returns409DuplicateTaskId` | OP-3 |
| `SafeTaskCreationTests.cs` | `Given_TaskCreatedDueToday_When_RepeatedAfterSydneyMidnight_Then_Returns200NotBr2` (clock moved to `2026-03-11T13:00Z`; the task is returned, now overdue) | OP-3: lookup before BR-2 |
| `SafeTaskCreationTests.cs` | `Given_TaskCreatedThenGrantClosed_When_Repeated_Then_Returns200NotBr1` (returned as Cancelled) | OP-3: lookup before BR-1 |
| `SafeTaskCreationTests.cs` | `Given_TaskCreatedThenCompleted_When_Repeated_Then_Returns200WithCompletedTask` | OP-3 (§6.5) |
| `SafeTaskCreationTests.cs` | `Given_TwoIdenticalCreates_When_Racing_Then_OneTaskExistsAndResponsesAre201And200` (the copy gets 201; this request's one save conflicts, the retry finds the task and returns 200 without saving; one task) | OP-3 + OP-2: the primary-key retry |

Mutation checks. Each was applied, all tests were run, and the file was restored. Checks 7 to 13 were added to the plan's list.

| # | Break | Tests that failed |
|---|---|---|
| 1 | Max attempts 3 → 1 | All 9 race tests and `Given_TwoIdenticalCreates_...` |
| 2 | Don't clear the change tracker between attempts | 8 of the 9 race tests (all but the retry-limit test, which fails every attempt anyway) |
| 3 | Move the id lookup after the rule checks | `..._RepeatedAfterSydneyMidnight_...`, `..._ThenGrantClosed_When_Repeated_...` |
| 4 | Compare titles without trimming | `..._SameIdSentWithPaddedTaskTitle_...` |
| 5 | Don't retry on a primary-key violation | `Given_TwoIdenticalCreates_When_Racing_...` (it returns 500). This also shows the race really takes the primary-key path, not only the version check. |
| 6 | Treat any existing id as a match | `..._SameIdSentWithDifferentDetails_...` (all three cases), `..._SameIdSentToAnotherGrant_...` |
| 7 | Grant not compared | `..._SameIdSentToAnotherGrant_...` |
| 8 | Due date not compared | `..._SameIdSentWithDifferentDetails_...` (due date) |
| 9 | Priority not compared | `..._SameIdSentWithDifferentDetails_...` (priority) |
| 10 | `ConcurrencyConflictException` not mapped (500) | `Given_ConflictOnEveryAttempt_...` |
| 11 | Max attempts 3 → 4 | `Given_ConflictOnEveryAttempt_...` (4 attempts) |
| 12 | A repeat returns 201 instead of 200 | All six repeat tests |
| 13 | `DuplicateTaskIdException` not mapped (500) | `..._SameIdSentWithDifferentDetails_...` (all three), `..._SameIdSentToAnotherGrant_...` |

Found while writing the tests:
- `FakeTimeProvider` can't move backwards, so the complete races can't have the other officer act at an earlier time. They act 30 minutes later instead (22:30 UTC). That's still the first completion saved, and it differs from this request's 22:00, so the test can tell whose time was kept.
- `Given_TwoIdenticalCreates_...` first asserted 2 save attempts. The right number is 1: on the retry, the lookup finds the task and returns it without saving. The assertion and its comment were corrected.

After restoring, `npm test` passed twice: 133 passed, 0 failed (114 from Parts 1 to 6, and 19 from Part 7). The Part 6 gap (a repeated create returned 500) is closed.

Changes from the plan:
- One interceptor instead of two (see the test helper above).
- The competing change is a real API request rather than a direct DbContext change, so both sides of each race go through the endpoint.
- New files not named in the plan: `DuplicateTaskIdException.cs`, `ConcurrencyConflictException.cs`, and the test files `ConcurrencyRetryTests.cs` and `SafeTaskCreationTests.cs`.

---

### Part 8: OP-5 health checks

**Builds:**
- `AddHealthChecks().AddDbContextCheck<AppDbContext>()`, tagged `ready`.
- `/health/live` runs no checks. `/health/ready` runs the `ready` checks (§6.4).
- The response body is the status word only.

**Done when:** both endpoints behave as in §6.4, including when the database can't be reached.

**Tests** (`tests/GrantTracker.Tests/Api/HealthCheckTests.cs`):

| Test | Covers |
|---|---|
| `Given_RunningApp_When_CallingLive_Then_Returns200Healthy` | OP-5 liveness |
| `Given_ReachableDatabase_When_CallingReady_Then_Returns200Healthy` | OP-5 readiness |
| `Given_UnreachableDatabase_When_CallingReady_Then_Returns503Unhealthy` | OP-5: take the copy out of the load balancer |
| `Given_UnreachableDatabase_When_CallingLive_Then_StillReturns200` | OP-5: liveness doesn't check the database (no restart storm) |
| `Given_UnreachableDatabase_When_CallingReady_Then_BodyHasNoExceptionDetails` | §6.4 no details leaked |

The unreachable database is a connection string pointing into a folder that doesn't exist. Start-up migration is switched off in this test's app (the hosted service from Part 5 is removed).

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| `/health/live` also runs the database check | `Given_UnreachableDatabase_When_CallingLive_...` |
| `/health/ready` runs no checks | `Given_UnreachableDatabase_When_CallingReady_Then_Returns503...` |

**Build record:** Built on 2026-11-13.

What was built:
- Package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.15 in `GrantTracker.Api` (same version as EF Core).
- `Program.cs`: `AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"])`. `/health/live` maps with `Predicate = _ => false`, so it runs no checks. `/health/ready` runs only the checks tagged `ready`.
- The default response writer is kept on purpose. It writes only the status word (`Healthy` or `Unhealthy`) as `text/plain`, and maps Unhealthy to 503. Nothing else from the report reaches the caller.
- The health endpoints are written in a few lines of `Program.cs`, so no new source file was needed.

Tests (`tests/GrantTracker.Tests/Api/HealthCheckTests.cs`, 5 tests). Each test asserts the status code and the exact body.

| Test | Covers |
|---|---|
| `Given_RunningApp_When_CallingLive_Then_Returns200Healthy` | OP-5 liveness |
| `Given_ReachableDatabase_When_CallingReady_Then_Returns200Healthy` | OP-5 readiness |
| `Given_UnreachableDatabase_When_CallingReady_Then_Returns503Unhealthy` | OP-5: take the copy out of the load balancer |
| `Given_UnreachableDatabase_When_CallingLive_Then_StillReturns200` | OP-5: liveness doesn't check the database |
| `Given_UnreachableDatabase_When_CallingReady_Then_BodyHasNoExceptionDetails` | §6.4: the body is exactly `Unhealthy`; it doesn't contain the database path or the check's name |

The unreachable database is a SQLite file in a temp folder with a new random name that doesn't exist. The test's app replaces the DbContext registration with that connection string and removes `DatabaseInitializer`, as planned. The run doesn't create the folder; this was checked after the run.

Mutation checks (all done, each restored afterwards; script `mutate_part8.sh` in the session scratchpad):

| # | Break | Failing tests |
|---|---|---|
| 1 | `/health/live` maps with no predicate, so it runs the database check (planned) | `Given_UnreachableDatabase_When_CallingLive_Then_StillReturns200` |
| 2 | `/health/ready` runs no checks (planned) | `..._When_CallingReady_Then_Returns503Unhealthy`, `..._Then_BodyHasNoExceptionDetails` |
| 3 | The database check isn't tagged `ready` | `..._When_CallingReady_Then_Returns503Unhealthy`, `..._Then_BodyHasNoExceptionDetails` |
| 4 | A custom response writer adds each check's name, description and exception message | `..._Then_BodyHasNoExceptionDetails`, `..._Then_Returns503Unhealthy`, `Given_ReachableDatabase_...` |
| 5 | Unhealthy maps to 200 instead of 503 | `..._When_CallingReady_Then_Returns503Unhealthy` |
| 6 | The health endpoints aren't mapped | All 5 health tests |

Manual check: the real app, run on a scratch database file, returned `Healthy` with 200 and `text/plain` on both endpoints. Deleting the database file while the app ran wasn't possible on Windows, because the app's pooled connection keeps the file locked. So the outage is tested only by the tests above. The scratch files were deleted afterwards.

Changes from the plan: none to the behaviour. Four mutation checks were added to the two planned ones.

`npm test`: 138 passed (133 before, plus 5), on two runs. Build: 0 warnings.

---

### Part 9: Swagger UI (Development only)

**Builds:**
- The built-in OpenAPI document (`AddOpenApi`, `MapOpenApi` at `/openapi/v1.json`) and Swagger UI (`Swashbuckle.AspNetCore.SwaggerUI` at `/swagger`), only in the Development environment (§1, §5).
- Enums appear as strings and `DateOnly` as a `date` string in the document, matching the JSON the API really sends.

**Done when:** Swagger UI in Development lists every endpoint and can call them against the seed data, and neither endpoint exists outside Development.

**Tests** (`tests/GrantTracker.Tests/Api/OpenApiTests.cs`):

| Test | Covers |
|---|---|
| `Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_ListsEveryEndpoint` | §5 endpoints documented |
| `Given_ProductionEnvironment_When_GettingOpenApiDocumentOrSwagger_Then_Returns404` (Theory) | Development only, so the API isn't described publicly |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Map Swagger in every environment | `Given_ProductionEnvironment_...` |

**Build record:** _not built yet._

---

### Part 10: UI foundation: app, API client, one test command

**Builds:**
- A Vite React TypeScript app in `ui/`.
- TypeScript types for the §5 DTOs and ProblemDetails. `DateOnly` values are typed as plain strings, never `Date` (§5).
- One fetch helper (§6.3) that returns one of three results: success, API error (4xx with ProblemDetails) or server problem (5xx, network error, or no response within 10 seconds).
- A Vite development proxy for `/grants` and `/tasks` to the API, so no CORS setup is needed.
- Vitest with jsdom, and the test time zone set to `America/Los_Angeles`.
- The root `package.json` `test` script runs `dotnet test`, then the UI tests.

**Done when:**
- `npm test` at the root runs the .NET and the UI tests in one command and both pass. The command fails if either fails.

**Tests** (`ui/src/api/*.test.ts`, with `fetch` mocked and fake timers):

| Test | Covers |
|---|---|
| `given a 200 response, when fetching, then returns success with the parsed body` | §6.3 table |
| `given a 400 ProblemDetails with errors, when fetching, then returns an API error with field errors` | §6.3: BR-2 and BR-3 reach the form |
| `given a 409 ProblemDetails, when fetching, then returns an API error with title and detail` | §6.3: BR-1, BR-5 and others |
| `given a 500 response, when fetching, then returns a server problem` | OP-3 |
| `given fetch rejects, when fetching, then returns a server problem` | OP-3 network error |
| `given no response within 10 seconds, when fetching, then aborts and returns a server problem` | OP-3 timeout |
| `given a response just before 10 seconds, when fetching, then returns success` | OP-3 timeout boundary |
| `given a grant response, when fetching, then dueDate and today stay as the exact strings sent` | §5: calendar dates aren't turned into `Date` |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Remove the timeout | `given no response within 10 seconds, ...` |
| Treat 5xx as an API error | `given a 500 response, ...` |
| Timeout of 1 second | `given a response just before 10 seconds, ...` |
| Parse `dueDate` with `new Date()` | `given a grant response, ... exact strings sent` |

**Build record:** _not built yet._

---

### Part 11: UI: grant screen (tasks, Complete, Add task)

**Builds** (CONTEXT §7, DESIGN §4, §6.3):
- A grant drop-down, loaded from `GET /grants`.
- The task list: title, due date, priority and status. Due dates are shown as the API sends them. Overdue tasks are highlighted, using `isOverdue` from the API.
- A Complete button, only on pending tasks of open grants.
- The Add task form:
  - Client-side checks: title required and at most 100 characters after trimming; due date not before the API's `today` and not before the application date. Dates are compared as `"YYYY-MM-DD"` strings.
  - The task id comes from `crypto.randomUUID()`. It's kept until the task is saved, and then replaced for the next task.
  - The form is disabled for closed grants.
  - API errors are shown on the field or as a message.
- The server-problem banner.
- Buttons are disabled while their request is waiting.
- The grant reloads after a success and after any 409.

**Done when:** the screen works end to end against the running API with the seed data, and the tests below pass.

**Tests** (`ui/src/**/*.test.tsx`):

| Test | Covers |
|---|---|
| `given a grant with an overdue task, when the screen loads, then the task is highlighted as overdue` | BR-6 in the UI |
| `given a task due "2026-03-11", when the screen loads, then the due date shown is 11 March 2026` | §5: no day shift (the tests run in `America/Los_Angeles`) |
| `given pending and completed tasks on an open grant, when the screen loads, then only pending tasks have a Complete button` | BR-4 in the UI |
| `given a closed grant, when the screen loads, then the Add task form is disabled and there are no Complete buttons` | BR-1, §4.7 |
| `given an empty or 101-character task title, when submitting, then shows a title error and sends no request` | BR-3 client-side check |
| `given a due date before today or before the application date, when submitting, then shows a due date error and sends no request` | BR-2 client-side check (uses the API's `today`) |
| `given a due date equal to today, when submitting, then the request is sent` | BR-2 boundary on the client |
| `given the API returns 400 keyed on dueDate, when submitting, then the message is shown on the due date field` | BR-2 from the API (spec §5) |
| `given the API returns 409 BR-1, when submitting, then its title and detail are shown` | BR-1 from the API (spec §5) |
| `given no response within 10 seconds, when submitting, then the banner is shown, the values are kept and the button is enabled again` | OP-3 |
| `given a timeout, when sending again, then the same task id is sent` | OP-3 safe resend |
| `given a successful create, when adding the next task, then a new task id is used` | OP-3: no accidental reuse of an id |
| `given a request is waiting, when clicking Add twice, then one request is sent` | OP-3 double-click |
| `given the API returns 500, when completing a task, then the banner is shown` | OP-3 |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Create a new id on every submit | `given a timeout, when sending again, then the same task id is sent` |
| Keep the id after a success | `given a successful create, ... new task id` |
| Don't disable the button while waiting | `given a request is waiting, when clicking Add twice, ...` |
| Ignore `isOverdue` | `given a grant with an overdue task, ...` |
| Format the due date through `new Date()` | `given a task due "2026-03-11", ...` |
| Compare the due date with `<=` instead of `<` | `given a due date equal to today, ...` |

**Build record:** _not built yet._

---

### Part 12: UI: close a grant

**Builds** (§4 BR-5 UI):
- A Close button. It's disabled when the grant is closed or has a pending High task, with a tooltip giving the reason.
- A confirmation dialog with an optional note. The message depends on whether there are pending tasks.
- A 409 shows its `title` and `detail`, then the grant reloads.

**Done when:** closing works end to end against the running API with GR-100001, GR-100002 and GR-100004.

**Tests:**

| Test | Covers |
|---|---|
| `given a pending High task, when the screen loads, then Close is disabled with the reason` | BR-5 in the UI |
| `given a closed grant, when the screen loads, then Close is disabled` | §4.7 |
| `given pending tasks, when clicking Close, then the dialog says they will be cancelled` | §4 BR-5 dialog |
| `given no pending tasks, when clicking Close, then the dialog asks for a plain confirmation` | §4 BR-5 dialog |
| `given the API returns 409 BR-5, when confirming, then its title and detail are shown and the grant reloads` | BR-5 from the API, OP-2 (the state changed) |

**Mutation checks:**

| Break | Expected failing test |
|---|---|
| Enable Close despite a pending High task | `given a pending High task, ...` |
| Don't show the 409 message | `given the API returns 409 BR-5, ...` |

**Build record:** _not built yet._

---

### Part 13: Documentation: README and testing strategy

**Builds:**
- README: how to run the API, the UI and the tests, exactly as a reviewer would follow it.
- README: the testing strategy, about one page (spec §4.2):
  - What each level tests, and why.
  - What isn't tested, and the risk that leaves.
  - How we proved the tests catch bugs, using the mutation results from the build records.
  - What testing we'd add before going live, including automated mutation testing with Stryker.NET.
- README: design decisions, next steps, and time spent. You provide the time spent.
- `AI_USAGE.md` is yours to write, because it asks which parts you wrote yourself. I can draft a starting point from `docs/AI_LOG.md` if you want.

**Done when:** a fresh clone, following only the README, runs the API, the UI and `npm test` successfully.

**Tests:** none. The check is following the README from a clean clone.

**Build record:** _not built yet._

---

## Decisions

Made on 2026-11-12:

1. Node.js is installed.
2. Assertions use xUnit's built-in `Assert`.
3. Infrastructure tests get their own folder, `tests/GrantTracker.Tests/Infrastructure/` (§2 updated).
4. The design text on maximum lengths is corrected: SQLite doesn't enforce them (§3.2, §3.6, BR-3).
5. When adding a task, BR-1 is checked first, then BR-2 and BR-3 are reported together (§4).
6. Swagger UI is added, in Development only (Part 9). No GitHub Actions workflow.
7. Mutation checks are done by hand. Stryker.NET is listed in the README as testing to add before going live.
8. The seed is the same every run, apart from `today`: fixed ids and versions, and every timestamp at 09:00 Sydney (§8).
9. API tests pin the clock to `2026-03-10T22:00:00Z`, where the UTC date and the Sydney date differ, and assert literal dates (§9).
10. A UTC converter on every `DateTime` property marks values read from SQLite as UTC and refuses non-UTC values (§3.6).
11. xUnit v3 stays on the 3.x line (3.2.2) and runs through VSTest, so plain `dotnet test` works without a `global.json`. Moving to 4.x would need a `global.json` that opts into Microsoft.Testing.Platform (Part 0).

Made on 2026-11-13:

12. Problem `type` values are absolute URIs, `https://granttracker.example/problems/<code>`, with the short code in a `code` extension field that the UI and tests switch on (DESIGN.md §5.1). No documentation pages are published at those URIs; this is a recorded limitation (DESIGN.md §10.8).
13. `Grant.Close` rejects a note longer than 1000 characters after trimming: 400 keyed on `note`, checked after BR-5 (DESIGN.md §3.1, BR-5, §5, §5.1).
14. `POST /grants/{id}/tasks` returns `Location: /grants/{id}`, because there is no endpoint for a single task (DESIGN.md §5).
