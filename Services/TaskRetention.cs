using MiyaIsland.Models;
namespace MiyaIsland.Services;
public static class TaskRetention
{
    public static int Prune(IList<TaskItem> tasks, DateTime now, int days)
    {
        if (days <= 0) return 0;
        var expired = tasks.Where(t => t.Done && t.CompletedAt is { } at && at < now.AddDays(-days) && t.Remind?.Repeat is not (RepeatKind.Daily or RepeatKind.Weekly)).ToArray();
        foreach(var task in expired) tasks.Remove(task);
        return expired.Length;
    }
}
