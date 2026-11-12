# Build plan

**Status: Approved (revision 2) on 2026-11-12. In progress: Part 0 reviewed; Part 1 built, waiting for review.**

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
| Assertions | xUnit's built-in `Assert`. Assert the outcome the rule cares about (status code, ProblemDetails `type`, field key, timestamps), not incidental details. |

**Which level tests what.** The domain tests cover every rule and every boundary. API tests check that each outcome reaches the client correctly (status code, ProblemDetails `type`, field key), with one test per outcome rather than repeating every boundary. UI tests cover only the few behaviours in §9.

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
| 1 | Domain: creating a task (BR-1, BR-2, BR-3) | Built, waiting for review |
| 2 | Domain: complete, close, status, overdue (BR-4, BR-5, BR-6, §4.7) | Not started |
| 3 | Clock: "today" in Sydney | Not started |
| 4 | Infrastructure: database, dates, versions, seed data | Not started |
| 5 | API: read endpoints, errors, start-up | Not started |
| 6 | API: write endpoints (BR-1 to BR-5 through the API) | Not started |
| 7 | OP-2 retry and OP-3 safe task creation | Not started |
| 8 | OP-5 health checks | Not started |
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
| `BusinessRuleException.cs` | `Code` (becomes the ProblemDetails `type`) and a message. The constant `Br1GrantClosed = "BR-1"`. |
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

**Build record:** _not built yet._

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

**Build record:** _not built yet._

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

**Build record:** _not built yet._

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
| `Given_AnyError_When_Returned_Then_BodyHasTypeTitleStatusDetailInstanceAndTraceId` | §5.1 consistent error body |
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

**Build record:** _not built yet._

---

### Part 6: API: write endpoints (BR-1 to BR-5 through the API)

**Builds:**
- Application services and controllers for:
  - `POST /grants/{id}/tasks`. The body includes `id`, which is required (§5). Repeated creates are handled in Part 7.
  - `PUT /tasks/{id}/complete`.
  - `PUT /grants/{id}/close`, with an optional `note`.
- Input-shape validation: `id` missing or `Guid.Empty`, an unknown priority, or a malformed date (§6.3 step 1).
- Mapping of each exception to the §5.1 status and `type`.

**Done when:** each rule outcome reaches the client with the right status code, `type` and field key. Boundaries were already tested in Parts 1 and 2.

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

**Build record:** _not built yet._

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

**Build record:** _not built yet._

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

**Build record:** _not built yet._

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
