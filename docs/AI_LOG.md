# AI Log

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

### 2026-11-11: Open questions Q1–Q6

| What I proposed | What was wrong | What we changed |
|---|---|---|
| Q1: one configured program time zone defines "today". | Officers work in several regional offices, possibly in different time zones. One zone gives the wrong "today" for some offices. | Added an `Office` table with a time zone. "Today" follows the office. Whether the office hangs off the Grant or the Task is still open (DESIGN.md, section 9). |
| Q3: leave pending tasks on closed grants pending, and exclude them from the overdue list. | The user pointed out that this conflicts with BR-6: a pending, past-due task would be hidden from the overdue list. It also leaves tasks in a state no one can act on. | Added a `Cancelled` task status and `CancelledAtUtc`. Closing a grant cancels its pending tasks. Cancelled tasks are never overdue. |

### 2026-11-11: Offices and task status

| What I proposed | What was wrong | What we changed |
|---|---|---|
| An `Office` table linked to the Grant (option A), and Sydney and Perth as seed offices. | Multiple offices with different time zones is an assumption. The spec doesn't say so, and tying offices to grants is just another guess. | The whole program uses one time zone, `Australia/Sydney`. The `Office` table is removed. The multi-office options are recorded as a reminder and consideration only. |
| A stored `Status` column on `GrantTask`, alongside `CompletedAtUtc` and `CancelledAtUtc`. | It duplicates the timestamps and needs extra rules to keep them consistent. | `Status` is removed from storage. It's derived: `CompletedAtUtc` set → Completed, `CancelledAtUtc` set → Cancelled, neither → Pending. A DB check constraint stops both being set. |

### 2026-11-11: Enum storage and document layout

| What I proposed | What was wrong | What we changed |
|---|---|---|
| Store enums (`TaskPriority`, `GrantStatus`) as strings in the database, so the data is readable and safe to reorder. | The user spotted that `/tasks/overdue` sorts by priority. String columns sort alphabetically (`High` < `Low` < `Medium`), so `ORDER BY Priority DESC` would give Medium, Low, High. | Enums are stored as ints with explicit values (`Low = 1`, `Medium = 2`, `High = 3`), so database order matches business order. The API still serialises them as strings. |
| Reminders (assumptions and considerations) kept in `AI_LOG.md`, and some considerations mixed into the business-rules section of `DESIGN.md`. | It mixed the log with design content. | All of them moved to a new last section of `DESIGN.md`, "Considerations and limitations". `AI_LOG.md` now holds logs only. `CLAUDE.md` was updated to match. |

### 2026-11-12: Operations needs (OP) pass

| What I proposed | What was wrong | What we changed |
|---|---|---|
| Build the cheap parts of OP-1: a SQLite file with WAL and a busy timeout, and safe seeding when several copies start at once. | The user pointed out that SQLite isn't how the tool would run for real. Production would be Kubernetes with several replicas and PostgreSQL or SQL Server, where saved data survives a restart. A request cut off by a restart is covered by OP-3 (safe to repeat), and OP-5 keeps traffic away from unhealthy pods. | OP-1 is not built. It will be explained as a limitation. |
| Build one cheap part of OP-4: show the `traceId` in UI error messages. | The user chose to skip OP-4, since error bodies already carry a `traceId` (§5.1). | OP-4 is not built. |
| A UI banner saying "Can't reach the server. Nothing was saved." | Wrong. After a timeout the client doesn't know whether the server saved the change; it may have committed before the response was lost. I found this while reviewing the user's timeout approach. | The message must say the change may not have been saved and that sending it again is safe. |
| No UI tests: describe a manual check for the server-problem banner in the testing strategy, and state that the risk isn't covered by automated tests. | The user wants a few Vitest tests for the important UI cases. | Added a UI component test level (Vitest + React Testing Library). How one command runs both the .NET and the UI tests is still open. |

### 2026-11-12: Build plan

| What I proposed | What was wrong | What we changed |
|---|---|---|
| `DESIGN.md` §3.2 and BR-3 said the database's max length on `Title` is a second line of defence. | SQLite doesn't enforce maximum lengths, so the claim was false for this app. I found it while planning Part 4; the user confirmed the correction. | The design text now says the domain is the only length check on SQLite, and PostgreSQL or SQL Server would enforce the column lengths. |
| API tests pinned the fake clock to `2026-03-11T01:00:00Z` (Sydney 12:00), and the seed data had no timestamps defined. | At that instant the UTC date and the Sydney date are the same, so if the code were accidentally changed to use the UTC date, only the Part 3 clock tests would notice. The user asked for times where the UTC date and the Sydney date differ, so every level catches that regression. | The fake clock is pinned to `2026-03-10T22:00:00Z` (Sydney 2026-03-11 09:00). Every seeded timestamp is 09:00 Sydney, which is the previous day in UTC. Tests assert literal dates and instants, never values computed with the code under test. |
| Test names such as `Given_ClosedGrantAndInvalidTitle_When_AddingTask_Then_ThrowsBr1`, using `Title` for the task's title. | The user pointed out that `InvalidTitle` next to `ClosedGrant` can be read as the grant's title. | Every test name that refers to the task's title now says `TaskTitle` (for example `Given_ClosedGrantAndInvalidTaskTitle_When_AddingTask_Then_ThrowsBr1`). A naming rule was added to the plan's test conventions. |
| Part 0: fix the failing `dotnet test` (xUnit v3 4.x needs Microsoft.Testing.Platform on the .NET 10 SDK) by adding a `global.json` that opts into that platform and pins the SDK. Before asking, I also tried switching xUnit to VSTest mode with `UseMicrosoftTestingPlatformRunner=false`, which didn't work and was reverted. | The user rejected the `global.json`. | xUnit v3 was downgraded to 3.2.2 and `xunit.runner.visualstudio` to 3.1.5, which run through VSTest with no extra file (PLAN Decision 11). |
| The plan's coverage map labelled each rule with a short name (for example "BR-1 No task on a closed grant"). | The user asked for the full text of each rule and need, so the map can be checked against the spec without switching files. | The coverage map now quotes each BR and OP in full, and summarises the two design rules (§4.7, §3.6). |

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

### 2026-11-11 20:15 (+07:00)

> Q1. I suggest add another table for Office, where it store the Office name and the timezone of the office, since there could be multiple office working on a same task, which could affect the due date. Allow officer that create the task be able to update the office, so the due date or today will be follow the office. Example: Expire today (Sydney), so that people working in other office can know. (Please consider challenge me if you think this is not really a good approach) - Record this to the test strategy as one of the edge case and senario to test. Q2. Agree, task should be read only after the Grant is close. Consider gate this in both UI and Back-end. Update test strategy to add this as one of the case to test. Q3. I am not agree, it would vioalte the Busines rule 6. Please help me update the design. Consider: Add a new status to the Task: Cancelled, along with Pending and Compeleted. Now everytime a grant is closed with the Task still pending. All tasks will be update to canceled. It would be more reasonable than left a task pending without officer could do anything with it. Also the overdue is approriate for the task you could do. But for canceled that, it is reasonable to not appear in the Overdue list. So please record this and update the design (also feel free to challenge me), also add the canceledatutc for the task, also help me add this to the reminder: Consider that a grant coudl be re-opend, as in realife there cuould be a situation where the task is medium, then the grant is close, but later that task for some reason need to be done, then we will still have a way to re-open the grant and continue with the task (record this as limitation and design only, we will not build this.) Q4. Yes Q5. Focus on .NET test first, the requirement require that all test should be run with 1 command, please keep that in mind (All tests run with one command and pass every time). Q6. Yes.

### 2026-11-11 20:52 (+07:00)

> 1. I agree with you. However, Office tie to Grant is also not a good choice. They are just asumption. I suggest we keep the whole program in 1 single timezone as Sydney. Please help me record this edge case of many offices. They are not clarify clearly in the requirements. So there is 2 choices: 1: The Office table option I mentioned earlier. 2. We could also consider the due date is Date Time instead of Date only, so we can ensure the due date is accurate. However this also require that the assumption that there are many office, each office has a different timezone and the timezone will be tie with the computer time of the officer. So please record these as consideration and reminder. We will not implement this. 2. Keep my idea, and state that to README 3. I agree on the racing. However for the Status, how about remove the status completely, we decide the status based on the CompleteAtUTC and CancelledAtUtc, so task with complete date is completed, with cancelled date is cancelled, without both is pending.

### 2026-11-11 21:23 (+07:00)

> Can you help me check the Enum of priority and status. I see that they are stored as String. However, the overdue list sort by priority, so I would make sense to store as int. Please help me check, correct me if I am wrong. Also move the reminder from AI_LOG to design, create a new section for consideration business rules or limiation, and move them to the end, instead of mixing. AI_LOG now should not have anything beside the log.

### 2026-11-12 02:43 (+07:00)

> I think we should handle these OP: OP2, OP3 and OP 5. For OP1, it is not very realistic to handle it with SQL Lite, we use SQL lite because this is a small project. However, in a real devleopment environment, the project should be deploy in a k8s with multiple replica, and sever with a database such as POSTgresql or MS SQL. so the case where data loss is not very realistic as data will be saved to the DB. If that happen, we still have the OP-3 handle it, as repeating the request will not affect the data. Also, enable OP5 could help as well, since the health check ensure the request only go to the health pod. I think we can handle with a small check in the UI, that the time out is a few second, if there is no error reponse return or it is not in range of 400-499. Then we consider something wrong with the server and announce to the Client. that way they know there is something wrong, they can still savely re-send the request. So pelase consider this approach (feel free to correct me if I am wrong). OP2. Yes, this is a good approach/ OP3. Yes handle this. Please consider handle this in both UI and BE. Consider let UI handle the ID, so UI decide an ID for a task, so when there is another task is create again with the same ID, an exception should be return. We will also handle the UI that will not enable the button again when the response from server is not reach the client. OP4. I think we could ignore this, as in the Problem detail follow the RFC9457 standard, we should already return the traceID for the client right? OP5. Handle this. OP6, put it to the consideration and limitation.

### 2026-11-12 02:55 (+07:00)

> 1. I agree, 200 when everything match. 409 when they do not. 2. Agree 3. Yes 4. SQLite file, plain configuration. 5. I don't think we need this. 6. I think it is reasonable for a few Vitest for UI in important cases. 7. Yes.

### 2026-11-12 03:14 (+07:00)

> Choose optin A, and help me add summary for the README based on our converstation.

### 2026-11-12 03:36 (+07:00)

> We can now move to the implementation phase, please help me update the CLAUDE.md to indicate the build phase. Please prepare a plan first before doing any implementation. Propose a plan based on the agreement and the design, split the work in the plan to several small part, so it is easier for me to review, check your tests, consider edge case, etc. Each parts need a definition of done to summary what we will do and how it is consider as done, what are the test cases, make sure you prove the test actually cover the BRs or the OPs. Note: after the plan is approved, and go to the build phase, after each part is finished built. you should update the plan and mark it as build, with summary of what is build/cover, what are the test cases, what does the test case cover. Please make sure the test are follow best practice. EX: AAA, naming convention: Given_When_Then, etc.

### 2026-11-12 16:16 (+07:00)

> 1.I have installed nidejs, 2. Go with xUnit 3. Yes, 4. update and correct it. 5 Agree 6. yes, add swagger ui. 7. by hand. 
>
> Also please help me making sure: The seed data is the same everytime, make sure the date closed or complete is reasonable and testable. Ex: the time set somehow that we can test the edge case that the UTC time is in another date and the correct time is in Sydney, so we can avoid a case where the code is accidentally change to UTC, and the test can still catch it. Also check how SQL lite handle the date store in the DB, and consider coverter. As I remember postgresql store date with UTC timezone.

### 2026-11-12 17:35 (+07:00)

> I have review the Plan, there is several comment: 
>
> Please help me update this table to state full text of the business rules and ops so it is easier for me to check.
>
>
> ```
> Rule / need	Domain	Infrastructure	API	UI
> BR-1 No task on a closed grant	Part 1		Part 6	Part 11
> BR-2 Due date	Part 1	Part 4 (date comparison in SQL)	Parts 3, 6	Part 11
> BR-3 Title	Part 1		Part 6	Part 11
> BR-4 Complete once	Part 2		Part 6	Part 11
> BR-5 Close blocked by pending High	Part 2		Part 6	Part 12
> BR-6 Overdue	Part 2	Part 4	Parts 3, 5	Part 11
> §4.7 Closed grant is read-only	Part 2		Part 6	Parts 11, 12
> §3.6 Dates stored correctly		Part 4	Part 5	Parts 10, 11
> OP-2 Concurrency		Part 4 (version checks)	Part 7 (retry)	
> OP-3 Safe repeats, unreachable server			Part 7	Parts 10, 11
> OP-5 Health checks			Part 8
> ```
>
>
> Also please explain this test: "Given_ClosedGrantAndInvalidTitle_When_AddingTask_Then_ThrowsBr1" does the InvalidTitle here is the title of the Task? If it is, please update the name as it can be miss-understand to title of the grant.

### 2026-11-12 17:40 (+07:00)

> Yes. the plan is approve, please start with the implementation for Part 0.

### 2026-11-12 18:46 (+07:00)

> I accept the Part0. Please go ahead and start with part 1.

### 2026-11-12 19:05 (+07:00)

> Part 1 is accepted, please start with part 2

### 2026-11-12 22:31 (+07:00)

> I reviewed an accept Part2, please continue with Part 3.

### 2026-11-13 01:24 (+07:00)

> I approve this. please move to the next part
