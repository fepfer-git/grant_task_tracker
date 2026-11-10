# AI Log

## Reminders

### 2026-11-11: Grants are never deleted

- **Assumption:** Grants cannot be deleted. A grant exists permanently, and its lifecycle ends when it is closed.
- **Why:** The spec (`docs/CONTEXT.md`) doesn't mention deleting grants. A grant has no due date, so an old grant that is no longer relevant is handled by closing it with an optional closed note that gives the reason. That keeps the history readable for other officers and for support (OP-4).
- **Consequence:** No delete endpoint. The Grant → GrantTask FK uses `Restrict`, not cascade.

## Correction log

### 2026-11-11: Architecture and data model proposal

| What I proposed | What was wrong | What we changed |
|---|---|---|
| Called the task entity `TaskItem`. | The user prefers a domain name. | Renamed to `GrantTask`. |
| EF Core in-memory provider (the user's original choice). I pushed back but still sketched the model around it. | The in-memory provider doesn't enforce unique indexes, max length, FKs, or concurrency tokens properly. | Switched to SQLite. |
| Four projects (Api, Application, Domain, Infrastructure). | Too much ceremony for the size of the app. | The user chose three projects: Api (including application services), Domain, Infrastructure, plus a single test project. |
| I asked whether tasks can be added to closed grants, whether due dates can be in the past or before the application date, and how closing a grant with open tasks works. | I didn't read `docs/CONTEXT.md` first, as `CLAUDE.md` requires. BR-1, BR-2 and BR-5 already answer most of those questions. | I read `CONTEXT.md`. The proposal now follows the BRs, and only the true gaps go back to the user. |
| I didn't consider OP-1 (several API copies, restarts at any time) when choosing the database. | SQLite **in-memory** in the running app would lose all data on restart and wouldn't be shared between copies. | SQLite in-memory is used only in tests. The app uses a file-based SQLite database (pending user confirmation). |

### 2026-11-11: Business rules round

| What I proposed | What was wrong | What we changed |
|---|---|---|
| I pushed OP-1 concerns (file-based SQLite, WAL, multiple copies, migrations) into the data-model decisions. | Out of scope for this round. The user wants the BR design finished first, then a separate pass over the OPs to decide what to handle and what goes to the README as a limitation. | OPs are deferred. DESIGN.md marks OP topics as "not yet decided". |
| I suggested the API could require a ClosedNote when tasks are still pending. | The user decided the note is optional in every case. | ClosedNote is optional in the API. The UI always asks for it but never requires it. |

## Prompt log

### 2026-11-11 01:49 (+07:00)

> We’re starting docs/DESIGN.md. This session covers only architecture and the data model. Here are decisions I’ve already made: Stack: "C# / .NET 10, Entity Framework Core with In-memory, handle error with ProblemDetails, using a middleware that catch all exception thrown by controllers or services. React + TypeScript." A simple layer architecture. Data model: For Grant, it should have these attribute: Id, Reference, OrganisationName, ApplicationDate, Status (enum: Closed, Open), Version (handle concurency). Task: Id, GrantId, Title (limit 100), DueDate, Priority, CreatedAtUtc, CompletedAtUtc. Propose the project structure and the data model: entities, fields with types, constraints, indexes, and how each fits the decisions. Then challenge me: point out anything I’ve missed, anything risky, and any decision you’d push back on, with reasons. Don’t write the file yet.

### 2026-11-11 03:15 (+07:00)

> Update `TaskItem` to `GrantTask`. Update to use SQL-Lite. 3: What happens to open tasks when a grant is closed (block the close, or leave them)? -> When a grant is close when there are high-priority task => throw exception. If there are no high-priority task, we will handle that in UI, we will alert user everytime they close the grant, of there is no tasks left. Just simply close with a simple confirmation pop-up. If there are still tasks, a confirmation pop-up should have a message something like: "There are still tasks not closed yet, ... do you still want to close the grant" for example. Also, I think we should added another field for the Grand called: Closed Description or Closed note as optional, so when there are still tasks, the officer can note, the reason they close it when there are still task, so it not cause confuse to other officer reading the history. 5. A grant should not be deleted, the original requirement is not specify, so I assume that this is not a case, please help me record this to the AI_LOG (reminder section), asume that a grant will be exist permanently until it is close, since there is no due-date for the Grant. And officer can close it and put the reason, so we could cover a case when the grant is create long ago, and not relevant anymore nowaday. 7. We will take this later. 8. I agree 9. I agree. 1. Is the in-memory database a deliberate, permanent decision, or just for the first version? Would you accept SQLite in-memory? -> change to SQL Lite.  2. What are the rules for tasks on Closed grants, and for closing a grant that has open tasks? -> I answer above. 3. Should Task get a Version too? -> Yes. 4. Can grants be deleted, or only closed? -> I answer above  5. Three or four projects? -> Go with this simple Layer architecture: ```src/
> ├─ GrantTracker.Api/              # controllers, DTOs, application services, error mapping, Program.cs
> ├─ GrantTracker.Domain/           # Grant, GrantTask, rules as methods; no dependencies
> └─ GrantTracker.Infrastructure/   # AppDbContext, EF config, migrations, seeding, idempotency table
> tests/
> └─ GrantTracker.Tests/            # Domain/ (no database), Api/ (SQLite in-memory)
> ui/src/``` - Freely to correct me if I am wrong or you need more clarification. 6. Guid. 7. Low, Medium, High. One more small detail. Update Claude.md, so that you will record the prompt I send you to the AI_Log as well.

### 2026-11-11 04:24 (+07:00)

> 1. We will handle the OPs later. This round, we will focus on gather technical decision and design for the Business Rules, complete the detail design with all business rules agreements, data model, edge cases, testing strategy. Then we will take a look at the OPs first, and decide which should be handel and worth handle, which one is limitation and should go to the README.md. 2. Correct, the seeds data contains data for all scenario we should handle from BRs, so this is correct. 3. You are correct to spot this, record this to the design, Add new task to grant or update grant should update the version to avoid race-condition. 4. For BR-5, throw exception with 409 error response follow the ProblemDetails/RFC 9457 so it should have title. However we should also handle this at UI level as well, by disable the close grant button when there are still high task open. 5. No, the close note is optional in all case. The UI should always ask for close note/reason, however it is optional and decide by the user. 6. Yes, return 200 for that. Also, handle the UI by disable the button, so user cannot closed a task that is already closed via UI. When they do that via swagger or API request, return 200. 7. I agree.
