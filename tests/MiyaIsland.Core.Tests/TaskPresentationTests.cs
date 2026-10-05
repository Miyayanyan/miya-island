using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public class TaskPresentationTests
{
    [Theory]
    [InlineData("2026-02-30", "09:00", false)]
    [InlineData("2026-10-04", "24:00", false)]
    [InlineData("2026-10-04", "09:00", true)]
    public void ValidatesCalendarAndClock(string date, string time, bool expected)
        => Assert.Equal(expected, TaskPresentation.TryCreateReminder(date, time, RepeatKind.None, [], out _, out _));

    [Fact]
    public void WeeklyRequiresDays() => Assert.False(TaskPresentation.TryCreateReminder("2026-10-04", "09:00", RepeatKind.Weekly, [], out _, out _));

    [Fact]
    public void RecurringCompletionExpiresNextDay()
    {
        var task = new TaskItem { LastCompletedFor = new DateTime(2026,10,4,21,30,0) };
        Assert.True(TaskPresentation.IsCompleted(task, new DateTime(2026,10,4)));
        Assert.False(TaskPresentation.IsCompleted(task, new DateTime(2026,10,5)));
    }

    [Fact]
    public void DisplaysYesterdayOverdue()
    {
        var task = new TaskItem { Remind = new TaskReminder { Date = "2026-10-03", Time = "21:00" } };
        Assert.Equal("已过期 · 昨天 21:00", TaskPresentation.Label(task, new DateTime(2026,10,4,8,0,0)));
        task.Done = true;
        Assert.Equal("昨天 21:00", TaskPresentation.Label(task, new DateTime(2026,10,4,8,0,0)));
    }
}
