# Grant Task Tracker

## Assumptions and limitations

Decisions where the spec was silent or open to interpretation. The details are in `docs/DESIGN.md`.

- **Cancelled tasks are not overdue (interpretation of BR-6).** Closing a grant cancels any pending Low or Medium tasks (BR-5 already blocks closing while High tasks are pending). BR-6 says a task is overdue when it "hasn't been completed". We read Cancelled as a final state, meaning the task *will never be* completed, not that it *hasn't been completed yet*, so cancelled tasks never appear as overdue. Cancelled tasks only exist on closed grants, so no task that an officer could still act on is hidden from the overdue list.
- **One time zone for the whole program: `Australia/Sydney`.** "Today" for BR-2 (due date not in the past) and BR-6 (overdue) is the current date in Sydney. The spec mentions several regional offices but not whether they're in different time zones. An officer in a different time zone near midnight may see a due date accepted or rejected one day "early" or "late".
- **Grants are never deleted.** The spec doesn't mention deleting grants. A grant that is no longer relevant is closed, with an optional note giving the reason.
- **A closed grant can't be re-opened.** Its tasks are read-only, and any that were pending are cancelled. Re-opening is sketched in `docs/DESIGN.md` but not built.

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

| Problem | What we'd do |
|---|---|
| **No login.** Officers can't see "their" grants, and nothing records who completed a task or closed a grant. | Add a login, link officers to grants, and record who performed each action. |
| **Out-of-date screens.** Overdue highlighting is worked out when the screen loads, so a screen left open past midnight is wrong, and teammates' changes don't appear until a reload. | Reload the grant after each action and when the window regains focus. |
| **Backups.** The database is a single SQLite file. | Use a managed database with scheduled backups. |
| **Growing lists.** The grants list and the overdue list have no pagination. | Add pagination to both endpoints. |
| **Accessibility.** Colour alone isn't enough to mark a task as overdue. | Show an "Overdue" label as well as the colour. |
| **Database changes while several copies are running.** | Make each schema change work with both the old and the new version of the API, and run migrations as a separate deployment step. |

## Before running it for real

- Replace SQLite with PostgreSQL or SQL Server.
- Run database migrations as a separate deployment step, not when each copy of the API starts.
- Seed demo data only in development.
- Let requests in progress finish when a copy of the API is stopped (graceful shutdown).
- Keep logs in a central store (OP-4) and add a login.
- Serve the app over HTTPS. The UI creates task ids with `crypto.randomUUID()`, which browsers allow only over HTTPS or on `localhost`.
