using System.Globalization;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public static class TaskPresentation
{
    public static bool IsCompleted(TaskItem task, DateTime now) => task.Done ||
        (task.LastCompletedFor is { } at && at.Date == now.Date);

    public static string Label(TaskItem task, DateTime now)
    {
        if (task.Remind is not { } rule) return string.Empty;
        if (rule.Repeat == RepeatKind.Daily) return $"每天 {rule.Time}";
        if (rule.Repeat == RepeatKind.Weekly && rule.Weekdays.Count > 0)
        {
            string[] names = ["日", "一", "二", "三", "四", "五", "六"];
            return string.Join(" ", rule.Weekdays.OrderBy(d => ((int)d + 6) % 7).Select(d => names[(int)d])) + " " + rule.Time;
        }
        if (!DateTime.TryParseExact(rule.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return rule.Time;
        var day = date.Date == now.Date ? "今天" : date.Date == now.Date.AddDays(-1) ? "昨天" : date.ToString("MM-dd");
        var overdue = DateTime.TryParseExact($"{rule.Date} {rule.Time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) && due < now && !IsCompleted(task, now);
        return (overdue ? "已过期 · " : "") + day + " " + rule.Time;
    }

    public static bool TryCreateReminder(string date, string time, RepeatKind repeat, IEnumerable<DayOfWeek> weekdays,
        out TaskReminder? reminder, out string error)
    {
        reminder = null;
        error = string.Empty;
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            error = "日期请用 yyyy-MM-dd";
        else if (!TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            error = "时间请用 HH:mm";
        var days = weekdays.Distinct().ToList();
        if (error.Length == 0 && repeat == RepeatKind.Weekly && days.Count == 0) error = "请选择至少一个星期";
        if (error.Length > 0) return false;
        reminder = new TaskReminder { Date = date, Time = time, Repeat = repeat, Weekdays = days };
        return true;
    }
}
