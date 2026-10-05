namespace MiyaIsland.Models;

public enum GreetingSource { BuiltInAndCustom, CustomOnly }

public sealed class GreetingContext
{
    // 不指定时由服务的 TimeProvider 提供当前时间。
    public DateTime Now { get; set; }
    public string? Nickname { get; set; }
    public bool IsFirstRun { get; set; }
    public DateTime? LastLaunchAt { get; set; }
    public DateTime? LastGreetingAt { get; set; }
    public bool Enabled { get; set; } = true;
    public GreetingSource Source { get; set; }
    public IReadOnlyList<string> CustomLines { get; set; } = [];
    // 从旧到新排列，末尾是最近使用的模板。
    public IReadOnlyList<string> RecentTemplates { get; set; } = [];
    public GreetingSummary Summary { get; set; } = new();
}

public sealed class GreetingSummary
{
    public int TodayCount { get; set; }
    public int OverdueCount { get; set; }
    public int DoneTodayCount { get; set; }
    public string? NextTitle { get; set; }
    public DateTime? NextTime { get; set; }
}

public sealed class GreetingResult
{
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string TemplateUsed { get; init; } = "";
}
