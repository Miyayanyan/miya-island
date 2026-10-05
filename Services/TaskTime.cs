using MiyaIsland.Models;

namespace MiyaIsland.Services;

internal static class TaskTime
{
    internal static DateTime Local(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value.ToLocalTime() : DateTime.SpecifyKind(value, DateTimeKind.Local);

    internal static bool Repeats(TaskItem task) => task.Remind is { } reminder &&
        (reminder.Repeat == RepeatKind.Daily ||
         (reminder.Repeat == RepeatKind.Weekly && reminder.Weekdays is { Count: > 0 }));
}
