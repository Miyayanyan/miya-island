using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public sealed class TaskStore
{
    private static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;
    private readonly string _path;
    private readonly object _gate;
    private readonly TimeProvider _timeProvider;
    private readonly int _retentionDays;
    private readonly Action<string> _log;
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public TaskStore(string? dataDirectory = null, TimeProvider? timeProvider = null,
        int retentionDays = 7, Action<string>? log = null)
    {
        if (retentionDays < 0) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        _directory = Path.GetFullPath(dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiyaIsland"));
        _path = Path.Combine(_directory, "tasks.json");
        _gate = Locks.GetOrAdd(_path, _ => new object());
        _timeProvider = timeProvider ?? TimeProvider.System;
        _retentionDays = retentionDays;
        _log = log ?? (message => System.Diagnostics.Trace.TraceWarning(message));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter<RepeatKind>(JsonNamingPolicy.CamelCase, false));
        options.Converters.Add(new LocalDateConverter());
        return options;
    }

    public List<TaskItem> Load()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            List<TaskItem> tasks;
            if (File.Exists(_path) || File.Exists(_path + ".bak"))
            {
                if (!TryRead(_path, out tasks))
                {
                    if (TryRead(_path + ".bak", out tasks))
                    {
                        // 先保留损坏主文件，下一次保存不能用它覆盖健康备份。
                        Preserve(_path);
                    }
                    else
                    {
                        Preserve(_path);
                        Preserve(_path + ".bak");
                        tasks = [];
                    }
                }
            }
            else
            {
                var legacy = Path.Combine(_directory, "reminders.json");
                if (!File.Exists(legacy)) return [];
                var reminders = JsonSerializer.Deserialize<List<LegacyReminder>>(File.ReadAllText(legacy))
                    ?? throw new InvalidDataException("旧提醒文件格式无效，未修改原文件。");
                var now = TaskTime.Local(_timeProvider.GetLocalNow().DateTime);
                tasks = reminders.Select(r =>
                {
                    var due = TaskTime.Local(r.DueAt);
                    return new TaskItem
                    {
                        Id = r.Id, Title = r.Title, Done = r.Completed, CreatedAt = now,
                        CompletedAt = r.Completed ? now : null,
                        Remind = new TaskReminder { Time = due.ToString("HH:mm", CultureInfo.InvariantCulture),
                            Date = due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Repeat = RepeatKind.None },
                        LastFiredFor = r.Notified ? due : null
                    };
                }).ToList();
                Save(tasks);
                File.Move(legacy, UniquePath(Path.Combine(_directory, "reminders.v06.bak.json")));
            }
            var cutoff = TaskTime.Local(_timeProvider.GetLocalNow().DateTime).AddDays(-_retentionDays);
            tasks.RemoveAll(t => t.Done && !TaskTime.Repeats(t) &&
                t.CompletedAt is { } completed && completed < cutoff);
            return tasks;
        }
    }

    private bool TryRead(string path, out List<TaskItem> tasks)
    {
        tasks = [];
        if (!File.Exists(path)) return false;
        try
        {
            var document = JsonSerializer.Deserialize<TaskDocument>(File.ReadAllText(path), Options);
            if (document is null || document.Version != 1 || document.Tasks is null ||
                document.Tasks.Any(t => t is null || t.Title is null))
                throw new JsonException("不支持的任务文件格式。");
            tasks = document.Tasks;
            return true;
        }
        catch (JsonException ex)
        {
            _log($"任务文件损坏：{path}；{ex.Message}");
            return false;
        }
    }

    private void Preserve(string path)
    {
        if (!File.Exists(path)) return;
        var stamp = _timeProvider.GetLocalNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var suffix = path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? ".bak" : "";
        File.Move(path, UniquePath(Path.Combine(_directory, $"tasks.corrupt-{stamp}{suffix}.json")));
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var index = 1;
        while (File.Exists(path + "." + index)) index++;
        return path + "." + index;
    }

    public void Save(IEnumerable<TaskItem> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            // 保存也检测旧文件，避免损坏主文件替换掉有效备份。
            if (File.Exists(_path) && !TryRead(_path, out _)) Preserve(_path);
            var json = JsonSerializer.Serialize(new TaskDocument { Version = 1, Tasks = tasks.ToList() }, Options);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
        }
    }

    private sealed class TaskDocument
    {
        public int Version { get; set; }
        public List<TaskItem>? Tasks { get; set; }
    }

    private sealed class LegacyReminder
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime DueAt { get; set; }
        public bool Completed { get; set; }
        public bool Notified { get; set; }
    }

    private sealed class LocalDateConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            TaskTime.Local(reader.GetDateTime());
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(TaskTime.Local(value));
    }
}
