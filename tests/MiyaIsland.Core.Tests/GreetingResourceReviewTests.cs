using System.Text.Json;
using MiyaIsland.Models;
using MiyaIsland.Services;

namespace MiyaIsland.Core.Tests;

public sealed class GreetingResourceReviewTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MiyaGreeting-review-" + Guid.NewGuid());
    private const string ResourceName = "MiyaIsland.Assets.greetings.zh-CN.json";

    private static GreetingContext Context() => new()
    {
        Now = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Local),
        Nickname = "Miya", IsFirstRun = true,
        LastLaunchAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Local),
        Summary = new GreetingSummary
        {
            TodayCount = 3, OverdueCount = 1, DoneTodayCount = 5,
            NextTitle = "给妈妈回电话",
            NextTime = new DateTime(2026, 10, 5, 21, 30, 0, DateTimeKind.Local)
        }
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void MissingOverrideLoadsAll56EmbeddedTemplates()
    {
        using var stream = typeof(GreetingService).Assembly.GetManifestResourceStream(ResourceName);
        Assert.NotNull(stream);
        using var json = JsonDocument.Parse(stream);
        var expected = json.RootElement.GetProperty("groups").EnumerateObject()
            .SelectMany(group => group.Value.EnumerateArray().Select(line => line.GetString()!))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(56, expected.Count);
        Assert.False(File.Exists(Path.Combine(_directory, "greetings.zh-CN.json")));

        var service = new GreetingService(_directory, random: new Random(1234));
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var ctx = Context();
        // 遍历全部时段、任务状态和星期组，确认资源中的模板实际进入候选。
        foreach (var day in new[] { 5, 9, 11 })
        foreach (var hour in new[] { 5, 8, 11, 13, 18, 20, 23 })
        foreach (var state in new[] { "overdue", "todo", "done", "empty" })
        {
            ctx.Now = new DateTime(2026, 10, day, hour, 0, 0, DateTimeKind.Local);
            ctx.Summary.OverdueCount = state == "overdue" ? 1 : 0;
            ctx.Summary.TodayCount = state == "todo" ? 3 : 0;
            ctx.Summary.DoneTodayCount = state == "done" ? 5 : 0;
            for (var i = 0; i < 200; i++) selected.Add(service.Pick(ctx).TemplateUsed);
        }
        Assert.True(expected.SetEquals(selected), "未加载或无法选择全部 56 句嵌入文案。");
    }

    [Fact]
    public void ValidOverrideTakesPriorityOverEmbeddedCatalog()
    {
        WriteOverride("""{"version":1,"groups":{"morning":["覆盖文案"]}}""");
        Assert.Equal("覆盖文案", new GreetingService(_directory).Pick(Context()).Title);
    }

    [Theory]
    [InlineData("broken-json")]
    [InlineData("""{"version":2,"groups":{"morning":["错误版本"]}}""")]
    [InlineData("""{"version":1,"groups":{"morning":null}}""")]
    [InlineData("""{"version":1,"groups":{"morning":[null]}}""")]
    [InlineData("""{"version":1,"groups":{}}""")]
    public void InvalidOverrideFallsBackToEmbeddedCatalogAndIsPreserved(string content)
    {
        WriteOverride(content);
        var service = new GreetingService(_directory, random: new Random(1));
        var result = service.Pick(Context());
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
        Assert.DoesNotContain("{", result.Title);
        Assert.NotEqual("今天也一起慢慢来", result.TemplateUsed);
        Assert.Equal(content, File.ReadAllText(Path.Combine(_directory, "greetings.zh-CN.json")));
    }

    [Fact]
    public void MissingBothSourcesUsesSafeBuiltInSentence()
    {
        var service = new GreetingService(_directory, null, new Random(1), () => null);
        var result = service.TryGetStartupGreeting(Context());
        Assert.NotNull(result);
        Assert.Equal("今天也一起慢慢来", result.Title);
        Assert.Equal(result.Title, result.TemplateUsed);
    }

    [Theory]
    [InlineData("broken-resource")]
    [InlineData("""{"version":1,"groups":null}""")]
    public void InvalidEmbeddedCatalogUsesSafeBuiltInSentence(string content)
    {
        var service = new GreetingService(_directory, null, new Random(1),
            () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
        Assert.Equal("今天也一起慢慢来", service.Pick(Context()).Title);
    }

    [Fact]
    public void UnreadableSourcesDoNotBlockStartup()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "greetings.zh-CN.json"));
        var service = new GreetingService(_directory, null, new Random(1),
            () => throw new IOException("模拟资源读取失败"));
        Assert.Equal("今天也一起慢慢来", service.TryGetStartupGreeting(Context())!.Title);
    }

    private void WriteOverride(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "greetings.zh-CN.json"), content);
    }
}
