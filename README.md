## Getting started

### 1. Install the prerequisites

| Tool     | Version                                                            |
| -------- | ------------------------------------------------------------------ |
| .NET SDK | 10.0 (built with 10.0.403)                                         |
| Node.js  | 22.22 or later, 24.15 or later, or 26 and later (built with 26.13) |
| npm      | Comes with Node.js                                                 |

### 2. Install the packages

```
cd grant_task_tracker
npm install
```

Run `npm install` at the repository root. It also installs the UI's packages in `ui/`. The .NET packages are restored the first time you build, test or run.

### 3. Run the tests

```
npm test
```

This runs every test: first the .NET tests (`dotnet test GrantTracker.slnx`), then the UI tests (Vitest). The command fails if any test fails.

To run one half only:

| What            | Command                         |
| --------------- | ------------------------------- |
| .NET tests only | `dotnet test GrantTracker.slnx` |
| UI tests only   | `npm --prefix ui test`          |

### 4. Start the API

In a terminal at the repository root:

```
dotnet run --project src/GrantTracker.Api --launch-profile http
```

The API listens on **http://localhost:5038**. On its first start, it creates `src/GrantTracker.Api/granttracker.db` and fills it with four sample grants. Leave this terminal open.

Check that it's running:

| Address                            | What you should see                                                      |
| ---------------------------------- | ------------------------------------------------------------------------ |
| http://localhost:5038/health/live  | `Healthy`                                                                |
| http://localhost:5038/health/ready | `Healthy` (`Unhealthy` with status 503 if the database can't be reached) |
| http://localhost:5038/grants       | The four sample grants as JSON                                           |
| http://localhost:5038/swagger      | Swagger UI, where you can try every endpoint                             |

Swagger UI and the OpenAPI document (`/openapi/v1.json`) are available only in the Development environment, which the `http` profile uses.

### 5. Start the UI

In a **second** terminal at the repository root:

```
npm --prefix ui run dev
```

Open **http://localhost:5173** in a browser. The UI's development server forwards API calls to http://localhost:5038, so the API from step 4 must be running.

### 6. Try it with the sample data

The sample due dates are counted from the day the database was created, so there are always overdue tasks to see.

| Grant                                       | What to try                                                                                                                                               |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GR-100001 – Riverside Community Garden Inc. | An overdue High-priority task is highlighted. Close grant is disabled while that task is pending. Complete it, and Close grant becomes available.         |
| GR-100002 – Northside Youth Sports Club     | Has an overdue Low-priority task. Close it with a note: the dialog warns that its pending tasks will be cancelled, and they show as Cancelled afterwards. |
| GR-100003 – Harbour Arts Collective         | Already closed. Its tasks are read-only, and you can't add a task.                                                                                        |
| GR-100004 – Hillview Library Friends Group  | Has no tasks. Add one, or close it with a plain confirmation.                                                                                             |

To add a task, choose a grant, fill in the title, due date and priority, and select Add task. A due date in the past is rejected.

To see every overdue task across all grants, select **Show overdue tasks** at the top of the page. The list starts with the most overdue task. Select **Open grant** on a row to go to that grant, where you can complete the task.

### 7. Start again with fresh sample data

Stop the API (Ctrl+C in its terminal), delete the database files, then start the API again (step 4):

```
# PowerShell
Remove-Item src/GrantTracker.Api/granttracker.db*

# macOS, Linux or Git Bash
rm src/GrantTracker.Api/granttracker.db*
```

The sample data is added only when the database is empty, so restarting the API without deleting the file keeps your changes.

### Troubleshooting

- **Port 5038 or 5173 is already in use.** Another program, or an earlier copy of the app, is using the port. Stop it and try again. The UI expects the API on port 5038.
- **The UI shows "We couldn't reach the server…".** The API isn't running, or it stopped. Start it (step 4) and try again.
- **`npm test` fails with a Node.js version error.** Install a Node.js version from the table in step 1.
- **Can't delete `granttracker.db` on Windows.** The API is still running and has the file open. Stop it first.

### Project layout

| Folder                            | Contents                                                                                                       |
| --------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `src/GrantTracker.Domain`         | The grant and task rules (BR-1 to BR-6), with no database or web code                                          |
| `src/GrantTracker.Infrastructure` | EF Core: the database, migrations and sample data                                                              |
| `src/GrantTracker.Api`            | The Web API: endpoints, error responses, health checks and start-up                                            |
| `tests/GrantTracker.Tests`        | .NET tests, in `Domain`, `Infrastructure` and `Api` folders                                                    |
| `ui`                              | The React UI. Its tests sit next to the components (`*.test.tsx`).                                             |
| `docs`                            | The scenario (`CONTEXT.md`), the design (`DESIGN.md`), the build plan (`PLAN.md`) and the AI log (`AI_LOG.md`) |

## Assumptions and limitations

Decisions where the spec was silent or open to interpretation. The details are in `docs/DESIGN.md`.

- **Cancelled tasks are not overdue (interpretation of BR-6).** Closing a grant cancels any pending Low or Medium tasks (BR-5 already blocks closing while High tasks are pending). BR-6 says a task is overdue when it "hasn't been completed". We read Cancelled as a final state, meaning the task _will never be_ completed, not that it _hasn't been completed yet_, so cancelled tasks never appear as overdue. Cancelled tasks only exist on closed grants, so no task that an officer could still act on is hidden from the overdue list.
- **One time zone for the whole program: `Australia/Sydney`.** "Today" for BR-2 (due date not in the past) and BR-6 (overdue) is the current date in Sydney. The spec mentions several regional offices but not whether they're in different time zones. An officer in a different time zone near midnight may see a due date accepted or rejected one day "early" or "late".
- **Grants are never deleted.** The spec doesn't mention deleting grants. A grant that is no longer relevant is closed, with an optional note giving the reason.
- **A closed grant can't be re-opened.** Its tasks are read-only, and any that were pending are cancelled. Re-opening is sketched in `docs/DESIGN.md` but not built.
- **An overdue task can still be completed.** No rule covers this, and BR-2's "due date cannot be in the past" applies only when a task is added. Officers look for overdue tasks so they can deal with them, and completing a task is how they clear it. Blocking it would also mean a grant with an overdue High-priority task could never be closed (BR-5). Whether a task was completed late isn't shown, but it can be worked out from the completion date and the due date.

## Operations needs

The operations team listed six needs (OP-1 to OP-6), and at least two had to be built. We built three: **OP-2, OP-3 and OP-5**. The details are in `docs/DESIGN.md` §6 (built) and §10 (not built).

### Why these three

OP-2 and OP-3 protect the data and the business rules. If they fail, a rule can be broken, a task can be saved twice, or an officer can't tell whether their work was saved. OP-5 is small and keeps officers away from a copy of the API that can't reach its database. The other three needs are about deployment, diagnosis and reporting. Without them nothing is lost or broken, so they're explained below instead of built.

### What we built

- **OP-2: Many officers working at the same time.**
  - Every grant and task has a version that is checked when it's saved, and adding a task changes the grant's version too. If two officers act on the same grant at the same moment (for example, one adds a High-priority task while another closes the grant), the second save fails instead of breaking BR-1 or BR-5.
  - The server then retries that request, up to 3 times, against the new data, and checks the business rules again each time. Officers see an error only when a rule really blocks them, not just because a teammate saved at the same moment.
  - The client doesn't send the version it last saw (`If-Match`). The server always checks the rules against the current data, and there are no endpoints that edit a task's details, so there's nothing for it to protect.
- **OP-3: Unreliable connections.** Every write is safe to send again:
  - Completing a task that's already completed returns 200 with the original completion date (BR-4).
  - Closing a grant that's already closed returns 200 and changes nothing.
  - When creating a task, the UI chooses the task id. Sending the same id again with the same details returns 200 with the saved task. Sending it with different details returns 409. The id is checked before the business rules, so a resend after midnight, or after the grant was closed, still returns the saved task.

  In the UI, a 5xx response, a network error, or no response within 10 seconds shows a banner: "We couldn't reach the server. Your change may not have been saved. It's safe to send it again." A timeout doesn't mean the change failed, so the banner doesn't claim it did. Buttons are disabled while a request is waiting, so a double-click sends once, and the form keeps what the officer typed.

- **OP-5: Health checks.**
  - `/health/live` returns 200 while the process is answering. It doesn't check the database: if it did, a database outage would make Kubernetes restart every copy, which doesn't fix the database.
  - `/health/ready` checks the database and returns 503 when it can't be reached, so the copy is taken out of the load balancer.

### How we'd handle the rest

- **OP-1: Several copies and restarts.** Not built as its own need. The exercise runs one copy of the API on a SQLite file, because it must run with no setup. A real deployment would run several copies on Kubernetes with PostgreSQL or SQL Server. The design already helps:
  - The API keeps no state between requests, so any copy can serve any request.
  - Each request saves in one transaction, so a copy stopped mid-request leaves nothing half-saved.
  - OP-3 makes a request cut off by a restart safe to send again.
  - OP-5 stops new traffic going to a copy that can't reach the database.
- **OP-4: Support investigating problems days later.** Not built. Every error response includes a `traceId`, but it's only useful if the logs that contain it are kept, and by default they aren't. We'd send structured logs with the `traceId` to a central log store, log events for task created, task completed and grant closed, show the `traceId` in UI error messages so officers can quote it, and add a login so the data records who did what.
- **OP-6: Usage counts and overdue-list speed.** Not built. Daily counts of created and completed tasks would come from timestamps the database already stores, through a report endpoint or a dashboard. The speed of the overdue list would come from ASP.NET Core's built-in request-duration metric, exported with OpenTelemetry to a monitoring tool, with an alert when it slows down.

### Problems not on the list
