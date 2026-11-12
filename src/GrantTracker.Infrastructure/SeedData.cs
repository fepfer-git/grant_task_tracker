using GrantTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Infrastructure;

/// <summary>
/// The CONTEXT.md §8 seed data, as DESIGN.md §8 describes it. Everything is fixed except <c>today</c>:
/// ids, versions, and every timestamp at 09:00 in the program time zone. Runs only when the database is empty.
/// </summary>
public static class SeedData
{
    public static readonly Guid Gr100001Id = new("00000000-0000-0000-0000-000000100001");
    public static readonly Guid Gr100002Id = new("00000000-0000-0000-0000-000000100002");
    public static readonly Guid Gr100003Id = new("00000000-0000-0000-0000-000000100003");
    public static readonly Guid Gr100004Id = new("00000000-0000-0000-0000-000000100004");

    private static readonly TimeOnly SeedTimeOfDay = new(9, 0);

    /// <param name="today">The date the app starts, in the program time zone.</param>
    /// <param name="timeZone">The program time zone. Seeded timestamps are 09:00 on their date in this zone.</param>
    public static async Task SeedAsync(AppDbContext db, DateOnly today, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        if (await db.Grants.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTime At0900(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(SeedTimeOfDay), timeZone);

        // Each task is created on its grant's application date, so every due date agrees with BR-2.
        GrantTask AddSeedTask(Grant grant, int number, string title, TaskPriority priority, DateOnly dueDate)
        {
            var task = grant.AddTask(TaskId(number), title, dueDate, priority, grant.ApplicationDate, At0900(grant.ApplicationDate));
            db.Entry(task).Property(t => t.Version).CurrentValue = TaskVersion(number);
            return task;
        }

        Grant AddSeedGrant(Guid id, string reference, string organisationName, DateOnly applicationDate, int number)
        {
            var grant = new Grant(id, reference, organisationName, applicationDate);
            db.Grants.Add(grant);
            db.Entry(grant).Property(g => g.Version).CurrentValue = GrantVersion(number);
            return grant;
        }

        var gr100001 = AddSeedGrant(Gr100001Id, "GR-100001", "Riverside Community Garden Inc.", today.AddDays(-60), 1);
        AddSeedTask(gr100001, 1, "Verify bank details", TaskPriority.High, today.AddDays(-3));
        AddSeedTask(gr100001, 2, "Request quote for tools", TaskPriority.Medium, today.AddDays(5));
        AddSeedTask(gr100001, 3, "Check eligibility documents", TaskPriority.High, today.AddDays(-50))
            .Complete(At0900(today.AddDays(-52)));

        var gr100002 = AddSeedGrant(Gr100002Id, "GR-100002", "Northside Youth Sports Club", today.AddDays(-20), 2);
        AddSeedTask(gr100002, 4, "Review progress report", TaskPriority.Low, today.AddDays(-1));
        AddSeedTask(gr100002, 5, "Schedule site visit", TaskPriority.Medium, today.AddDays(10));

        var gr100003 = AddSeedGrant(Gr100003Id, "GR-100003", "Harbour Arts Collective", today.AddDays(-200), 3);
        AddSeedTask(gr100003, 6, "Final acquittal review", TaskPriority.High, today.AddDays(-150))
            .Complete(At0900(today.AddDays(-151)));
        gr100003.Close(note: null, At0900(today.AddDays(-145)));

        AddSeedGrant(Gr100004Id, "GR-100004", "Hillview Library Friends Group", today.AddDays(-5), 4);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Tasks 1 to 6, in the order of the CONTEXT.md §8 table.</summary>
    public static Guid TaskId(int number) => new($"00000000-0000-0000-0001-{number:D12}");

    private static Guid GrantVersion(int number) => new($"00000000-0000-0000-0002-{number:D12}");

    private static Guid TaskVersion(int number) => new($"00000000-0000-0000-0003-{number:D12}");
}
