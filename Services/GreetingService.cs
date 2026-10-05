using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public sealed class GreetingService
{
    private const string ResourceName = "MiyaIsland.Assets.greetings.zh-CN.json";
    private const string FallbackTemplate = "今天也一起慢慢来";
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;
    private readonly Dictionary<string, string[]> _groups;
    private readonly object _gate = new();
    private static readonly Regex Placeholder = new(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);

    public GreetingService(string? dataDirectory = null, TimeProvider? timeProvider = null, Random? random = null)
        : this(dataDirectory, timeProvider, random,
            () => typeof(GreetingService).Assembly.GetManifestResourceStream(ResourceName))
    {
    }

    internal GreetingService(string? dataDirectory, TimeProvider? timeProvider, Random? random,
        Func<Stream?> embeddedResource)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? Random.Shared;
        var directory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiyaIsland");
        var path = Path.Combine(directory, "greetings.zh-CN.json");
        _groups = TryLoadGroups(() => File.OpenRead(path)) ??
            TryLoadGroups(embeddedResource) ?? [];
    }

    private static Dictionary<string, string[]>? TryLoadGroups(Func<Stream?> openStream)
    {
        try
        {
            using var stream = openStream();
            if (stream is null) return null;
            var document = JsonSerializer.Deserialize<GreetingDocument>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document?.Version != 1 || document.Groups is not { Count: > 0 } ||
                document.Groups.Values.Any(lines => lines is null || lines.Any(line => line is null)))
                return null;
            return document.Groups;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or JsonException)
        {
            // 覆盖文件或资源不可用时继续降级，不阻止程序启动。
            System.Diagnostics.Trace.TraceWarning($"问候文案不可用：{ex.Message}");
            return null;
        }
    }

    public GreetingResult? TryGetStartupGreeting(GreetingContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var now = Now(ctx);
        if (!ctx.Enabled || (ctx.LastGreetingAt is { } last &&
            now - TaskTime.Local(last) < TimeSpan.FromHours(4))) return null;
        return Pick(ctx);
    }

    public GreetingResult Pick(GreetingContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var now = Now(ctx);
        var values = Values(ctx, now);
        var candidates = new List<(string Template, double Weight)>();
        void Add(IEnumerable<string> lines, double weight)
        {
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                if (Placeholder.Matches(line).Any(m => !values.ContainsKey(m.Groups[1].Value))) continue;
                candidates.Add((line, weight * (line.Contains("{name}", StringComparison.Ordinal) ? 1.5 : 1)));
            }
        }
        void Group(string group, double weight)
        {
            if (_groups.TryGetValue(group, out var lines)) Add(lines, weight);
        }
        var custom = ctx.CustomLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        if (ctx.Source != GreetingSource.CustomOnly || custom.Length == 0)
        {
            Group(GetTimeGroup(now), 1);
            var state = State(ctx.Summary);
            Group(state, state == "overdue" ? 2.5 : 1.2);
            if (ctx.IsFirstRun) Group("first", 4);
            if (ctx.LastLaunchAt is { } launch && now - TaskTime.Local(launch) >= TimeSpan.FromDays(3))
                Group("back", 4);
            if (now.DayOfWeek == DayOfWeek.Monday) Group("monday", 1.6);
            if (now.DayOfWeek == DayOfWeek.Friday) Group("friday", 1.6);
            if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) Group("weekend", 1.6);
        }
        Add(custom, 2);
        if (candidates.Count == 0) Group(GetTimeGroup(now), 1);
        if (candidates.Count == 0) candidates.Add((FallbackTemplate, 1));
        var recent = ctx.RecentTemplates.TakeLast(8).ToHashSet(StringComparer.Ordinal);
        var fresh = candidates.Where(c => !recent.Contains(c.Template)).ToList();
        if (fresh.Count > 0) candidates = fresh;
        double draw;
        lock (_gate) draw = _random.NextDouble() * candidates.Sum(c => c.Weight);
        var template = candidates[^1].Template;
        foreach (var candidate in candidates)
        {
            draw -= candidate.Weight;
            if (draw < 0) { template = candidate.Template; break; }
        }
        return new GreetingResult
        {
            Title = Placeholder.Replace(template, m => values[m.Groups[1].Value]),
            TemplateUsed = template,
            Subtitle = Subtitle(ctx.Summary)
        };
    }

    private DateTime Now(GreetingContext ctx) => TaskTime.Local(
        ctx.Now == default ? _timeProvider.GetLocalNow().DateTime : ctx.Now);

    public static string GetTimeGroup(DateTime now) => now.Hour switch
    {
        >= 5 and < 8 => "dawn",
        >= 8 and < 11 => "morning",
        >= 11 and < 13 => "noon",
        >= 13 and < 18 => "afternoon",
        >= 18 and < 20 => "dusk",
        >= 20 and < 23 => "evening",
        _ => "night"
    };

    private static string State(GreetingSummary summary) => summary.OverdueCount > 0 ? "overdue"
        : summary.TodayCount > 0 ? "todo" : summary.DoneTodayCount > 0 ? "done" : "empty";

    private static Dictionary<string, string> Values(GreetingContext ctx, DateTime now)
    {
        var values = new Dictionary<string, string>
        {
            ["count"] = ctx.Summary.TodayCount.ToString(CultureInfo.InvariantCulture),
            ["overdue"] = ctx.Summary.OverdueCount.ToString(CultureInfo.InvariantCulture),
            ["weekday"] = new[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" }[(int)now.DayOfWeek]
        };
        if (!string.IsNullOrWhiteSpace(ctx.Nickname)) values["name"] = ctx.Nickname.Trim();
        if (!string.IsNullOrWhiteSpace(ctx.Summary.NextTitle))
        {
            var title = ctx.Summary.NextTitle;
            var indices = StringInfo.ParseCombiningCharacters(title);
            values["next"] = indices.Length > 10 ? title[..indices[10]] + "…" : title;
        }
        if (ctx.Summary.NextTime is { } time)
            values["nexttime"] = TaskTime.Local(time).ToString("HH:mm", CultureInfo.InvariantCulture);
        return values;
    }

    private static string Subtitle(GreetingSummary summary) => State(summary) switch
    {
        "overdue" => $"{summary.OverdueCount} 件已过期" +
            (string.IsNullOrWhiteSpace(summary.NextTitle) ? "" : $" · {summary.NextTitle}"),
        "todo" => $"今天 {summary.TodayCount} 件" +
            (summary.NextTime is { } time && !string.IsNullOrWhiteSpace(summary.NextTitle)
                ? $" · 下一件 {TaskTime.Local(time).ToString("HH:mm", CultureInfo.InvariantCulture)} {summary.NextTitle}" : ""),
        "done" => $"今天完成了 {summary.DoneTodayCount} 件",
        _ => "今天没有安排"
    };

    private sealed class GreetingDocument
    {
        public int Version { get; set; }
        public Dictionary<string, string[]>? Groups { get; set; }
    }
}
