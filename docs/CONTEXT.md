# Context

Source: `spec/Software-Developer-Technical-Exercise.pdf`. This file records only what the spec states about the product. Gaps and ambiguities are in "Open questions" at the end. Section numbers (§) are the spec's.

## 1. Background (§1)

- A community grants program funds local not-for-profit organisations.
- When an organisation applies, a grant record is opened. A grants officer manages it until it is closed.
- The officer creates tasks along the way. Examples given: "Check eligibility documents", "Verify bank details", "Review progress report".
- Grants officers need a simple tool to: see their grants, add tasks, mark tasks as complete, and spot overdue tasks.
- The scenario, organisations and all data are fictional.

## 2. Users

- **Grants officers**: use the tool in several regional offices. Up to 100 use it at the same time across all offices. Each grant is usually handled by a small team of officers rather than one person (§3, OP-2).
- **Operations team**: runs and supports the tool (§3).
- **Support team**: a small team who didn't build the tool. They investigate problems officers report, often days after they happened (§3, OP-4).
- **Program manager**: wants usage numbers (§3, OP-6).

## 3. Domain fields (§1)

**Grant**

- Reference number
- Applying organisation's name
- Application date
- Status: open or closed

**Task**

- Title
- Due date
- Priority: Low, Medium or High
- Can be marked as completed

§2.2 BR-4 also states that completing a task records the date it was completed.

## 4. Business rules (§2.2)

| Rule | Description                                                                                                                                                                           |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| BR-1 | A task cannot be added to a closed grant.                                                                                                                                             |
| BR-2 | A new task's due date cannot be in the past, and cannot be before the grant's application date.                                                                                       |
| BR-3 | The task title is required and must be 100 characters or fewer.                                                                                                                       |
| BR-4 | Completing a task records the date it was completed. Completing a task that is already completed must not change that date. Decide what the API returns in that case and explain why. |
| BR-5 | A grant cannot be closed while it has any pending High-priority task.                                                                                                                 |
| BR-6 | A task is overdue when it hasn't been completed and its due date is before today. "Today" must be testable, so don't call `DateTime.Now` directly inside the business logic.          |

## 5. Endpoints (§2.1)

| Endpoint                   | Purpose                                                                                          |
| -------------------------- | ------------------------------------------------------------------------------------------------ |
| `GET /grants?status=Open`  | List grants, with an optional status filter. Include the number of pending tasks for each grant. |
| `GET /grants/{id}`         | Get one grant with its tasks.                                                                    |
| `POST /grants/{id}/tasks`  | Add a task to a grant.                                                                           |
| `PUT /tasks/{id}/complete` | Mark a task as completed.                                                                        |
| `PUT /grants/{id}/close`   | Close a grant.                                                                                   |
| `GET /tasks/overdue`       | List all overdue tasks across all grants, with the most overdue first.                           |

The spec says route names, HTTP methods and response shapes can be chosen.

## 6. Operations needs (§3)

The spec calls these "deliberately brief" and "not a complete list".

| Need | Description                                                                                                                                                                                                                                          |
| ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| OP-1 | Usage peaks sharply during funding rounds, so the team will run several copies of the API side by side and may restart any of them at any time without warning. Officers must not lose work or notice when this happens.                             |
| OP-2 | Up to 100 officers use the tool at the same time across all offices, and each grant is usually handled by a small team of officers rather than one person. The business rules in §2.2 must hold at all times.                                        |
| OP-3 | Internet connections in regional offices are unreliable, so the same request may be sent more than once. Repeating a request must be safe, and the screen must tell officers clearly when the server can't be reached, rather than failing silently. |
| OP-4 | Once the tool is live, a small support team who didn't build it will look into problems that officers report, often days after they happened.                                                                                                        |
| OP-5 | The operations team needs an automated way to tell whether each running copy of the API is working and can reach its data, so they can stop sending officers to a copy that isn't.                                                                   |
| OP-6 | The program manager wants to know how many tasks are created and completed each day, and whether the overdue list gets slower as data grows, without asking a developer to run database queries.                                                     |

## 7. Front-end requirements (§5)

- Keep it small: one screen for working on a grant's tasks is enough.
- A simple way to choose a grant, such as a drop-down list.
- The grant's tasks, with overdue tasks clearly highlighted.
- An Add task form with client-side validation. It must also show errors returned by the API (for example BR-1 and BR-2).
- A Complete button on each pending task.
- Any tooling (Vite is fine) and any component library, or none. "Function matters more than appearance."

Optional extras the spec lists (§6) that touch the product:

- A grants list screen with an Open / Closed / All filter, and a Close grant button that shows the reason if the API refuses (BR-5).
- More of the operations team's needs from §3.
- Swagger / OpenAPI, pagination.
- A Dockerfile or docker-compose file, or a GitHub Actions workflow that builds and runs the tests.

## 8. Seed data (§6)

Seed the database at start-up. Set due dates relative to the date the app starts, so the overdue logic can always be demonstrated.

| Grant reference | Organisation                    | Application date | Status | Tasks (title / priority / due / status)                          |
| --------------- | ------------------------------- | ---------------- | ------ | ---------------------------------------------------------------- |
| GR-100001       | Riverside Community Garden Inc. | today - 60 days  | Open   | Verify bank details / High / today - 3 days / Pending            |
|                 |                                 |                  |        | Request quote for tools / Medium / today + 5 days / Pending      |
|                 |                                 |                  |        | Check eligibility documents / High / today - 50 days / Completed |
| GR-100002       | Northside Youth Sports Club     | today - 20 days  | Open   | Review progress report / Low / today - 1 day / Pending           |
|                 |                                 |                  |        | Schedule site visit / Medium / today + 10 days / Pending         |
| GR-100003       | Harbour Arts Collective         | today - 200 days | Closed | Final acquittal review / High / today - 150 days / Completed     |
| GR-100004       | Hillview Library Friends Group  | today - 5 days   | Open   | (no tasks)                                                       |

## 9. Technical constraints

- Stack (header): C# / .NET (ASP.NET Core Web API, any current version), Entity Framework Core, React + TypeScript. (§2.3, §5)
- Design the data model yourself. (§2.3)
- Use EF Core with SQLite or the EF Core in-memory provider, so the solution runs with no external setup. (§2.3)
- Return validation and business-rule errors with appropriate HTTP status codes and a consistent error body (for example `ProblemDetails`). (§2.3)
- Separate the API, business logic and data access. "Keep it simple; don't over-engineer." (§2.3)
- Seed the database at start-up with the §6 data. (§2.3)
- Automated tests (§4.1):
  - Cover BR-1 to BR-6, including the date logic.
  - Tests at more than one level.
  - At least one test for a risk identified from §3.
  - All tests run with one command and pass every time.
  - Tests must not depend on the actual date or on test order.
