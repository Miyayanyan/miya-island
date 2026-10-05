using System.Text.Json;
using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public sealed class FixedTimeProvider(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(now).ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}

public sealed class TaskTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MiyaIsland-tests-" + Guid.NewGuid());
    private static DateTime At(int year = 2026, int month = 10, int day = 4, int hour = 10, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Local);
    private TaskStore Store() => new(_directory, new FixedTimeProvider(At()));
    private static TaskItem Task(RepeatKind repeat = RepeatKind.None, string date = "2026-10-04", string time = "09:00") =>
        new() { Title = "测试", CreatedAt = At(2025, 1, 1),
            Remind = new() { Repeat = repeat, Date = date, Time = time,
                Weekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday] } };
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Fact]
    public void MigratesLegacyAndPreservesOldFile()
    {
        Directory.CreateDirectory(_directory);
        var id = Guid.NewGuid();
        File.WriteAllText(Path.Combine(_directory, "reminders.json"), JsonSerializer.Serialize(new[]
        {
            new { Id = id, Title = "旧提醒", DueAt = At(), Completed = true, Notified = true }
        }));
        var task = Assert.Single(Store().Load());
        Assert.Equal(id, task.Id);
        Assert.Equal("旧提醒", task.Title);
        Assert.True(task.Done);
        Assert.Equal(At(), task.LastFiredFor);
        Assert.Equal("2026-10-04", task.Remind!.Date);
        Assert.Equal("10:00", task.Remind.Time);
        Assert.Equal(RepeatKind.None, task.Remind.Repeat);
        Assert.Equal(DateTimeKind.Local, task.CreatedAt.Kind);
        Assert.True(File.Exists(Path.Combine(_directory, "reminders.v06.bak.json")));
        Assert.False(File.Exists(Path.Combine(_directory, "reminders.json")));
        Assert.Single(Store().Load());
    }

    [Fact]
    public void RoundTripAndBackupUseRequiredJsonShape()
    {
        var store = Store();
        var task = Task(RepeatKind.Weekly);
        task.SnoozedUntil = At();
        store.Save([task]);
        task.Title = "改名";
        store.Save([task]);
        var loaded = Assert.Single(store.Load());
        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal(task.Title, loaded.Title);
        Assert.Equal(task.SnoozedUntil, loaded.SnoozedUntil);
        Assert.Equal(DateTimeKind.Local, loaded.SnoozedUntil!.Value.Kind);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "tasks.json")));
        var reminder = json.RootElement.GetProperty("tasks")[0].GetProperty("remind");
        Assert.Equal("weekly", reminder.GetProperty("repeat").GetString());
        Assert.Equal(1, reminder.GetProperty("weekdays")[0].GetInt32());
        Assert.True(File.Exists(Path.Combine(_directory, "tasks.json.bak")));
    }

    [Fact]
    public void CorruptMainFallsBackAndHealthyBackupSurvivesSave()
    {
        var store = Store();
        store.Save([Task()]);
        store.Save([Task()]);
        File.WriteAllText(Path.Combine(_directory, "tasks.json"), "broken-main");
        Assert.Single(store.Load());
        store.Save([]);
        Assert.Contains("测试", System.Text.RegularExpressions.Regex.Unescape(
            File.ReadAllText(Path.Combine(_directory, "tasks.json.bak"))));
        Assert.Contains(Directory.GetFiles(_directory, "tasks.corrupt-*"),
            p => File.ReadAllText(p) == "broken-main");
    }

    [Fact]
    public void BothCorruptFilesArePreserved()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "tasks.json"), "main-bad");
        File.WriteAllText(Path.Combine(_directory, "tasks.json.bak"), "backup-bad");
        Assert.Empty(Store().Load());
        Assert.Equal(new[] { "backup-bad", "main-bad" },
            Directory.GetFiles(_directory, "tasks.corrupt-*").Select(File.ReadAllText).Order().ToArray());
        Assert.False(File.Exists(Path.Combine(_directory, "tasks.json")));
    }

    [Fact]
    public void CleansOnlyOldCompletedNonRepeatingTasks()
    {
        var old = Task(); old.Done = true; old.CompletedAt = At().AddDays(-8);
        var boundary = Task(); boundary.Done = true; boundary.CompletedAt = At().AddDays(-7);
        var repeating = Task(RepeatKind.Daily); repeating.Done = true; repeating.CompletedAt = old.CompletedAt;
        var unknown = Task(); unknown.Done = true;
        Store().Save([old, boundary, repeating, unknown]);
        var tasks = Store().Load();
        Assert.Equal(3, tasks.Count);
        Assert.DoesNotContain(tasks, t => t.Id == old.Id);
    }

    [Fact]
    public void OccurrencesCrossMonthYearAndWeekStrictly()
    {
        var scheduler = new ReminderScheduler([]);
        Assert.Equal(At(2027, 1, 1, 9), scheduler.NextOccurrence(Task(RepeatKind.Daily), At(2026, 12, 31, 9)));
        Assert.Equal(At(2026, 11, 1, 9), scheduler.NextOccurrence(Task(RepeatKind.Daily), At(2026, 10, 31, 9)));
        Assert.Equal(At(2026, 10, 5, 9), scheduler.NextOccurrence(Task(RepeatKind.Weekly), At(2026, 10, 2, 9)));
        Assert.Equal(At(2026, 10, 7, 9), scheduler.NextOccurrence(Task(RepeatKind.Weekly), At(2026, 10, 5, 9)));
        Assert.Equal(At(2027, 1, 1, 9), scheduler.NextOccurrence(Task(RepeatKind.Weekly), At(2026, 12, 30, 9)));
        var once = Task(date: "2027-01-01");
        Assert.Equal(At(2027, 1, 1, 9), scheduler.NextOccurrence(once, At(2026, 12, 31)));
        Assert.Null(scheduler.NextOccurrence(once, At(2027, 1, 1, 9)));
        Assert.Null(scheduler.CurrentOccurrence(once, At()));
    }

    [Fact]
    public void FiredSnoozedDismissedAndNextOccurrence()
    {
        var task = Task(RepeatKind.Daily);
        var scheduler = new ReminderScheduler([task]);
        var occurrence = Assert.Single(scheduler.GetDue(At())).Occurrence;
        scheduler.MarkFired(task, occurrence);
        Assert.Empty(scheduler.GetDue(At()));
        scheduler.Snooze(task, At().AddMinutes(10));
        Assert.Empty(scheduler.GetDue(At().AddMinutes(9)));
        var second = Assert.Single(scheduler.GetDue(At().AddMinutes(10)));
        scheduler.MarkFired(task, second.Occurrence);
        Assert.Null(task.SnoozedUntil);
        Assert.Empty(scheduler.GetDue(At().AddMinutes(11)));
        scheduler.Dismiss(task, occurrence);
        Assert.Single(scheduler.GetDue(At().AddDays(1)));
    }

    [Fact]
    public void CompletingRepeatOnlyCompletesCurrentOccurrence()
    {
        var task = Task(RepeatKind.Daily);
        var scheduler = new ReminderScheduler([task]);
        scheduler.Complete(task, At());
        Assert.False(task.Done);
        Assert.Equal(At(hour: 9), task.LastCompletedFor);
        Assert.Empty(scheduler.GetDue(At()));
        Assert.Single(scheduler.GetDue(At().AddDays(1)));
        scheduler.Uncomplete(task);
        Assert.Null(task.LastCompletedFor);
        Assert.Single(scheduler.GetDue(At()));
        var once = Task();
        scheduler.Complete(once, At());
        Assert.True(once.Done);
        Assert.Equal(At(), once.CompletedAt);
    }

    [Fact]
    public void StartupSuppressesMissedOccurrences()
    {
        var tasks = new[] { Task(), Task(), Task(), Task(RepeatKind.Daily) };
        var scheduler = new ReminderScheduler(tasks);
        var report = scheduler.CatchUpOnStartup(At());
        Assert.Equal(3, report.OverdueCount);
        Assert.Equal(3, report.MissedTitles.Count);
        Assert.Empty(scheduler.GetDue(At()));
        Assert.Single(scheduler.GetDue(At().AddDays(1)));
    }

    [Fact]
    public void EmptyWeeklyFallsBackAndLogs()
    {
        var task = Task(RepeatKind.Weekly);
        task.Remind!.Weekdays = [];
        var log = new List<string>();
        var scheduler = new ReminderScheduler([task], log: log.Add);
        Assert.Equal(At(hour: 9), scheduler.CurrentOccurrence(task, At()));
        Assert.Null(scheduler.NextOccurrence(task, At()));
        Assert.NotEmpty(log);
    }

    [Fact]
    public void SummaryAndInjectedClock()
    {
        var today = Task();
        var future = Task(date: "2026-10-05");
        var plain = new TaskItem { Title = "普通任务" };
        var scheduler = new ReminderScheduler([today, future, plain], new FixedTimeProvider(At()));
        Assert.Single(scheduler.GetDue());
        var summary = scheduler.GetSummary(At());
        Assert.Equal(2, summary.TodayCount);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(At(2026, 10, 5, 9), summary.Next!.Time);
        scheduler.Complete(today, At());
        Assert.Equal(1, scheduler.GetSummary(At()).DoneTodayCount);
    }

    [Fact]
    public void ConcurrentStoresDoNotTearWrites()
    {
        Parallel.For(0, 20, i => { Store().Save([Task()]); Assert.Single(Store().Load()); });
        Assert.Single(Store().Load());
    }
}
