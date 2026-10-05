using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public sealed class GreetingTests
{
    private static GreetingContext Context() => new() { Now = new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Local) };
    private static GreetingService Service() => new(random: new Random(1234));

    [Fact]
    public void NoNameNeverSelectsNameTemplate()
    {
        var service = Service();
        var ctx = Context();
        for (var i = 0; i < 300; i++)
            Assert.DoesNotContain("{name}", service.Pick(ctx).TemplateUsed);
    }

    [Fact]
    public void CooldownAndEnabledAreRespectedButPreviewBypassesThem()
    {
        var service = Service();
        var ctx = Context();
        ctx.LastGreetingAt = ctx.Now.AddHours(-4).AddSeconds(1);
        Assert.Null(service.TryGetStartupGreeting(ctx));
        Assert.NotNull(service.Pick(ctx));
        ctx.LastGreetingAt = ctx.Now.AddHours(-4);
        Assert.NotNull(service.TryGetStartupGreeting(ctx));
        ctx.Enabled = false;
        Assert.Null(service.TryGetStartupGreeting(ctx));
    }

    [Fact]
    public void ExcludesLastEightAndFallsBackWhenAllExcluded()
    {
        var ctx = Context();
        ctx.Source = GreetingSource.CustomOnly;
        ctx.CustomLines = Enumerable.Range(0, 9).Select(i => $"句子{i}").ToArray();
        ctx.RecentTemplates = ctx.CustomLines.Take(8).ToArray();
        Assert.Equal("句子8", Service().Pick(ctx).Title);
        ctx.RecentTemplates = ctx.CustomLines;
        Assert.Equal("句子0", Service().Pick(ctx).Title);
        ctx.CustomLines = ["句子0"];
        ctx.RecentTemplates = ["句子0"];
        Assert.Equal("句子0", Service().Pick(ctx).Title);
    }

    [Fact]
    public void EmptyCustomOnlyFallsBackToBuiltIn()
    {
        var ctx = Context();
        ctx.Source = GreetingSource.CustomOnly;
        Assert.False(string.IsNullOrWhiteSpace(Service().Pick(ctx).Title));
    }

    [Fact]
    public void PlaceholdersAndTenCharacterTruncation()
    {
        var ctx = Context();
        ctx.Nickname = "Miya";
        ctx.Source = GreetingSource.CustomOnly;
        ctx.CustomLines = ["{name}/{count}/{overdue}/{next}/{nexttime}/{weekday}"];
        ctx.Summary = new() { TodayCount = 3, OverdueCount = 1,
            NextTitle = "一二三四五六七八九十十一", NextTime = ctx.Now.Date.AddHours(21).AddMinutes(30) };
        var result = Service().Pick(ctx);
        Assert.Equal("Miya/3/1/一二三四五六七八九十…/21:30/周二", result.Title);
        Assert.Equal(ctx.CustomLines[0], result.TemplateUsed);
    }

    [Fact]
    public void MissingValuesExcludeTemplatesAndInvalidCustomCanRecover()
    {
        var ctx = Context();
        ctx.Source = GreetingSource.CustomOnly;
        ctx.CustomLines = ["{next}", "{nexttime}", "{name}", "{unknown}", "有效句子"];
        Assert.Equal("有效句子", Service().Pick(ctx).Title);
        ctx.CustomLines = ["{name}"];
        Assert.DoesNotContain("{", Service().Pick(ctx).Title);
    }

    [Theory]
    [InlineData(4, 59, "night")]
    [InlineData(5, 0, "dawn")]
    [InlineData(7, 59, "dawn")]
    [InlineData(8, 0, "morning")]
    [InlineData(11, 0, "noon")]
    [InlineData(13, 0, "afternoon")]
    [InlineData(18, 0, "dusk")]
    [InlineData(20, 0, "evening")]
    [InlineData(22, 59, "evening")]
    [InlineData(23, 0, "night")]
    public void TimeBoundaries(int hour, int minute, string group) =>
        Assert.Equal(group, GreetingService.GetTimeGroup(new DateTime(2026, 10, 4, hour, minute, 0)));

    [Fact]
    public void SubtitlesAreObjectiveAndHandleMissingNext()
    {
        var ctx = Context();
        var service = Service();
        Assert.Equal("今天没有安排", service.Pick(ctx).Subtitle);
        ctx.Summary.DoneTodayCount = 5;
        Assert.Equal("今天完成了 5 件", service.Pick(ctx).Subtitle);
        ctx.Summary.TodayCount = 3;
        Assert.Equal("今天 3 件", service.Pick(ctx).Subtitle);
        ctx.Summary.NextTitle = "给妈妈回电话";
        ctx.Summary.NextTime = ctx.Now.Date.AddHours(21).AddMinutes(30);
        Assert.Equal("今天 3 件 · 下一件 21:30 给妈妈回电话", service.Pick(ctx).Subtitle);
        ctx.Summary.OverdueCount = 1;
        ctx.Summary.NextTitle = "交房租";
        Assert.Equal("1 件已过期 · 交房租", service.Pick(ctx).Subtitle);
    }

    [Fact]
    public void ClockIsUsedWhenNowNotProvided()
    {
        var now = Context().Now;
        var service = new GreetingService(timeProvider: new FixedTimeProvider(now), random: new Random(1));
        var ctx = new GreetingContext { LastGreetingAt = now.AddHours(-1) };
        Assert.Null(service.TryGetStartupGreeting(ctx));
    }

    [Fact]
    public void WeightsAndSpecialGroupsParticipate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MiyaGreeting-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "greetings.zh-CN.json"),
                """{"version":1,"groups":{"morning":["时间"],"overdue":["逾期"],"first":["首次"],"back":["回归"],"monday":["周一"]}}""");
            var service = new GreetingService(directory, random: new Random(123));
            var ctx = Context();
            ctx.Now = ctx.Now.AddDays(-1);
            ctx.IsFirstRun = true;
            ctx.LastLaunchAt = ctx.Now.AddDays(-3);
            ctx.Summary.OverdueCount = 1;
            ctx.CustomLines = ["自定义"];
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < 10000; i++)
            {
                var title = service.Pick(ctx).Title;
                counts[title] = counts.GetValueOrDefault(title) + 1;
            }
            Assert.Equal(6, counts.Count);
            Assert.True(counts["首次"] > counts["时间"] * 3);
            Assert.True(counts["回归"] > counts["时间"] * 3);
            Assert.True(counts["逾期"] > counts["时间"] * 2);
            Assert.True(counts["自定义"] > counts["时间"]);
        }
        finally { Directory.Delete(directory, true); }
    }
}
