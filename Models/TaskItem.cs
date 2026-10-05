namespace MiyaIsland.Models;

public enum RepeatKind { None, Daily, Weekly }

public sealed class TaskReminder
{
    public string Time { get; set; } = "09:00";
    public string? Date { get; set; }
    public RepeatKind Repeat { get; set; }
    public List<DayOfWeek> Weekdays { get; set; } = [];
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool Done { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TaskReminder? Remind { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public DateTime? LastFiredFor { get; set; }
    public DateTime? LastCompletedFor { get; set; }
}
