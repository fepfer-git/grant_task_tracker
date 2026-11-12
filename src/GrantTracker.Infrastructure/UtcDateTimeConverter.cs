using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GrantTracker.Infrastructure;

/// <summary>
/// Applied to every <see cref="DateTime"/> and <see cref="Nullable{DateTime}"/> property (DESIGN.md §3.6).
/// SQLite loses <see cref="DateTime.Kind"/>, so values read back are marked UTC without shifting the stored text.
/// A non-UTC value on save means some code used a local time, which is a bug, so the save fails.
/// </summary>
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(value => EnsureUtc(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    public static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new InvalidOperationException(
                $"Only UTC DateTime values can be saved, but this value has Kind = {value.Kind}. Use IAppClock.UtcNow.");
}
