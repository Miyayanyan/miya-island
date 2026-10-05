using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public sealed class ReminderSchedulerReviewTests
{
    // 使用隐式 using 和服务命名空间，不借助别名。
    private static DateTime Today(int hour, int minute = 0) =>
        new(2026, 10, 5, hour, minute, 0, DateTimeKind.Local);

    private static TaskItem Repeating(RepeatKind repeat = RepeatKind.Daily) => new()
    {
        Title = "给妈妈回电话",
        Remind = new TaskReminder
        {
            Time = "21:30", Repeat = repeat,
            Weekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]
        }
    };

    [Theory]
    [InlineData(RepeatKind.Daily)]
    [InlineData(RepeatKind.Weekly)]
    public void CompletingBeforeReminderCompletesTodayAndStillRemindsNextTime(RepeatKind repeat)
    {
        var task = Repeating(repeat);
        var scheduler = new ReminderScheduler([task]);
        scheduler.Complete(task, Today(10));

        Assert.False(task.Done);
        Assert.Equal(Today(21, 30), task.LastCompletedFor);
        Assert.Empty(scheduler.GetDue(Today(21, 31)));
        var summary = scheduler.GetSummary(Today(10));
        Assert.Equal(0, summary.TodayCount);
        Assert.Equal(1, summary.DoneTodayCount);
        var nextDay = repeat == RepeatKind.Daily ? 1 : 2;
        Assert.Equal(Today(21, 30).AddDays(nextDay), summary.Next!.Time);
        Assert.Single(scheduler.GetDue(Today(21, 31).AddDays(nextDay)));

        scheduler.Uncomplete(task);
        Assert.Equal(1, scheduler.GetSummary(Today(10)).TodayCount);
        Assert.Equal(0, scheduler.GetSummary(Today(10)).DoneTodayCount);
        Assert.Equal(Today(21, 30), scheduler.GetSummary(Today(10)).Next!.Time);
        Assert.Single(scheduler.GetDue(Today(21, 31)));
    }

    [Fact]
    public void CompletingOnUnscheduledDayUsesMostRecentOccurrence()
    {
        var task = Repeating(RepeatKind.Weekly);
        var scheduler = new ReminderScheduler([task]);
        var tuesday = Today(10).AddDays(1);
        scheduler.Complete(task, tuesday);

        Assert.Equal(Today(21, 30), task.LastCompletedFor);
        var summary = scheduler.GetSummary(tuesday);
        Assert.Equal(0, summary.TodayCount);
        Assert.Equal(0, summary.DoneTodayCount);
        Assert.Equal(Today(21, 30).AddDays(2), summary.Next!.Time);
        Assert.Single(scheduler.GetDue(Today(21, 31).AddDays(2)));
    }

    [Fact]
    public void NextSummaryUsesTonightInsteadOfTomorrow()
    {
        var scheduler = new ReminderScheduler([Repeating()]);
        var summary = scheduler.GetSummary(Today(10));

        Assert.Equal(Today(21, 30), summary.Next!.Time);
        Assert.Equal("给妈妈回电话", summary.Next.Title);
        Assert.Equal(1, summary.TodayCount);
    }

    [Fact]
    public void SummaryFindsEarliestUncompletedOccurrenceAcrossTasks()
    {
        var completed = Repeating();
        completed.LastCompletedFor = Today(21, 30);
        var tomorrow = Repeating();
        tomorrow.Title = "明天";
        tomorrow.Remind!.Time = "08:00";
        var once = new TaskItem
        {
            Title = "今晚",
            Remind = new TaskReminder { Date = "2026-10-05", Time = "22:00" }
        };
        var done = new TaskItem
        {
            Title = "已完成", Done = true, CompletedAt = Today(9),
            Remind = new TaskReminder { Date = "2026-10-05", Time = "11:00" }
        };
        var scheduler = new ReminderScheduler([completed, tomorrow, once, done]);

        var summary = scheduler.GetSummary(Today(10));
        Assert.Equal("今晚", summary.Next!.Title);
        Assert.Equal(Today(22), summary.Next.Time);
        Assert.Equal(2, summary.TodayCount);
        Assert.Equal(2, summary.DoneTodayCount);
    }

    [Fact]
    public void SummarySkipsCompletedOneOffAndCompletedFutureInstances()
    {
        var repeating = Repeating();
        repeating.LastCompletedFor = Today(21, 30).AddDays(3);
        var once = new TaskItem
        {
            Title = "已完成的一次性任务",
            Remind = new TaskReminder { Date = "2026-10-05", Time = "20:00" },
            LastCompletedFor = Today(20)
        };
        var scheduler = new ReminderScheduler([once, repeating]);

        Assert.Equal(Today(21, 30).AddDays(4), scheduler.GetSummary(Today(10)).Next!.Time);
    }

    [Fact]
    public void AtReminderTimeNextIsStrictlyFuture()
    {
        var scheduler = new ReminderScheduler([Repeating()]);
        Assert.Equal(Today(21, 30).AddDays(1), scheduler.GetSummary(Today(21, 30)).Next!.Time);
    }
}
