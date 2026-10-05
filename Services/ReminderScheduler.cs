using System.Globalization;
using MiyaIsland.Models;
using static MiyaIsland.Services.TaskTime;

namespace MiyaIsland.Services;

public sealed class StartupReport
{
    public int OverdueCount { get; init; }
    public IReadOnlyList<string> MissedTitles { get; init; } = [];
}

public sealed class TaskSummary
{
    public int TodayCount { get; init; }
    public int OverdueCount { get; init; }
    public int DoneTodayCount { get; init; }
    public TaskNext? Next { get; init; }
}

public sealed record TaskNext(string Title, DateTime Time);

public sealed class ReminderScheduler
{
    private readonly IEnumerable<TaskItem> _tasks;
    private readonly TimeProvider _timeProvider;
    private readonly Action<string> _log;

    public ReminderScheduler(IEnumerable<TaskItem> tasks, TimeProvider? timeProvider = null, Action<string>? log = null)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _log = log ?? (message => System.Diagnostics.Trace.TraceWarning(message));
    }

    private bool TryRule(TaskItem t, out TimeSpan time, out DateTime? date)
    {
        time = default;
        date = null;
        if (t.Remind is not { } r) return false;
        if (!TimeOnly.TryParseExact(r.Time, "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)) return false;
        time = parsed.ToTimeSpan();
        if (r.Repeat == RepeatKind.Weekly && r.Weekdays is not { Count: > 0 })
            _log("每周提醒未选择星期，按一次性提醒处理。");
        if (!Repeats(t))
        {
            if (!DateTime.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day)) return false;
            date = Local(day);
        }
        return true;
    }

    public DateTime? CurrentOccurrence(TaskItem t, DateTime now)
    {
        now = Local(now);
        if (!TryRule(t, out var time, out var date)) return null;
        if (date.HasValue)
        {
            var once = date.Value + time;
            return once <= now ? once : null;
        }
        for (var i = 0; i <= 7; i++)
        {
            var day = now.Date.AddDays(-i);
            var candidate = day + time;
            if (candidate <= now &&
                (t.Remind!.Repeat == RepeatKind.Daily || t.Remind.Weekdays.Contains(day.DayOfWeek)))
                return candidate;
        }
        return null;
    }

    public DateTime? NextOccurrence(TaskItem t, DateTime after)
    {
        after = Local(after);
        if (!TryRule(t, out var time, out var date)) return null;
        if (date.HasValue)
        {
            var once = date.Value + time;
            return once > after ? once : null;
        }
        var start = after.Date;
        for (var i = 0; i <= 7; i++)
        {
            var day = start.AddDays(i);
            var candidate = day + time;
            if (candidate > after &&
                (t.Remind!.Repeat == RepeatKind.Daily || t.Remind.Weekdays.Contains(day.DayOfWeek)))
                return candidate;
        }
        return null;
    }

    public IReadOnlyList<(TaskItem Task, DateTime Occurrence)> GetDue() =>
        GetDue(_timeProvider.GetLocalNow().DateTime);

    public IReadOnlyList<(TaskItem Task, DateTime Occurrence)> GetDue(DateTime now)
    {
        now = Local(now);
        var result = new List<(TaskItem Task, DateTime Occurrence)>();
        foreach (var t in _tasks.Where(t => !t.Done))
        {
            var occurrence = CurrentOccurrence(t, now);
            if (occurrence is not { } at || (t.LastCompletedFor is { } completed && at <= Local(completed))) continue;
            if (t.SnoozedUntil is { } snoozed)
            {
                if (Local(snoozed) <= now) result.Add((t, at));
            }
            else if (t.LastFiredFor is not { } fired || at > Local(fired)) result.Add((t, at));
        }
        return result;
    }

    public void MarkFired(TaskItem t, DateTime occurrence)
    {
        t.LastFiredFor = Local(occurrence);
        t.SnoozedUntil = null;
    }

    public void Complete(TaskItem t, DateTime now)
    {
        now = Local(now);
        if (Repeats(t))
        {
            t.LastCompletedFor = OccurrenceToday(t, now) ?? CurrentOccurrence(t, now);
            t.Done = false;
        }
        else
        {
            t.Done = true;
            t.CompletedAt = now;
        }
        t.SnoozedUntil = null;
    }

    public void Uncomplete(TaskItem t)
    {
        t.Done = false;
        t.CompletedAt = null;
        t.LastCompletedFor = null;
    }

    public void Snooze(TaskItem t, DateTime until) => t.SnoozedUntil = Local(until);

    public void Dismiss(TaskItem t, DateTime occurrence) => MarkFired(t, occurrence);

    public StartupReport CatchUpOnStartup(DateTime now)
    {
        now = Local(now);
        var titles = new List<string>();
        foreach (var t in _tasks.Where(t => !t.Done))
        {
            if (CurrentOccurrence(t, now) is not { } at || at >= now) continue;
            if (!Repeats(t) && (t.LastCompletedFor is null || Local(t.LastCompletedFor.Value) < at))
                titles.Add(t.Title);
            // 用户主动延后的提醒仍按延后时间触发。
            if (t.LastFiredFor is null || Local(t.LastFiredFor.Value) < at) t.LastFiredFor = at;
        }
        return new StartupReport { OverdueCount = titles.Count, MissedTitles = titles };
    }

    public TaskSummary GetSummary(DateTime now)
    {
        now = Local(now);
        var items = _tasks.ToList();
        var next = items.Where(t => !t.Done)
            .Select(t => (Task: t, At: NextOccurrence(t,
                t.LastCompletedFor is { } completed && Local(completed) > now ? Local(completed) : now)))
            .Where(x => x.At.HasValue)
            .OrderBy(x => x.At)
            .Select(x => new TaskNext(x.Task.Title, x.At!.Value)).FirstOrDefault();
        return new TaskSummary
        {
            TodayCount = items.Count(t => !t.Done && (t.Remind is null ||
                (OccurrenceToday(t, now) is { } at &&
                 (t.LastCompletedFor is null || Local(t.LastCompletedFor.Value) < at)))),
            OverdueCount = items.Count(t => !t.Done && !Repeats(t) &&
                CurrentOccurrence(t, now) is { } at && at < now),
            DoneTodayCount = items.Count(t => (t.CompletedAt is { } done && Local(done).Date == now.Date) ||
                (t.LastCompletedFor is { } completed && Local(completed).Date == now.Date)),
            Next = next
        };
    }

    private DateTime? OccurrenceToday(TaskItem t, DateTime now)
    {
        var occurrence = NextOccurrence(t, now.Date.AddTicks(-1));
        return occurrence is { } at && at.Date == now.Date ? at : null;
    }
}

