# Miya Island v0.7 · 给 GPT 的任务包 #1

> 由 Claude 整理。Claude 负责主导和集成，按 Claude 的 v0.7 方案执行。这份任务包是完整的，不需要看其他资料就能开始。

## 0. 背景

- **项目**：Miya Island，Windows 10/11 桌面灵动岛（WPF，C#）。
- **仓库**：`Miyayanyan/MiyaIsland`；基线分支 `claude/serene-lamport-3dd2qr`，提交 `66fd417` = v0.6.1 原样导入。
- **项目设置**：`net8.0-windows10.0.19041.0`，`UseWPF`，`UseWindowsForms`，`Nullable=enable`，`ImplicitUsings=enable`，唯一的 NuGet 依赖是 `NAudio 2.2.1`。
- **现有代码风格**：file-scoped namespace，`sealed class`，私有字段 `_camelCase`，`System.Text.Json`，数据放在 `%LocalAppData%\MiyaIsland\`，注释用简短中文。
- **v0.7 期间删除桌宠**，用 Tasks & Reminders 替代。现在的提醒数据是 `reminders.json`，格式如下（`Models/Reminder.cs`）：

```csharp
public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public bool Completed { get; set; }
    public bool Notified { get; set; }
}
```

## 1. 分工

| | Claude | GPT（这份任务包） |
|---|---|---|
| 范围 | 所有 UI、XAML、窗口、动画、玻璃、图标、`MainWindow` / `FloatingLyricsWindow` / `App`、所有现有文件、最终集成 | **新建的、不含 UI 的服务层文件** + 单元测试 |
| 原则 | 把 GPT 的服务接到界面上 | **不修改任何现有文件**；不引用 `System.Windows.*`（WPF） |

这样两边不会改到同一个文件，合并没有冲突。

## 2. 硬性约束（每个任务都适用）

1. **不改现有文件**，只新建文件。`AppearanceSettings.cs` 由 Claude 改，不要动。
2. **不加新的 NuGet 包**（测试项目除外：xUnit 相关可以）。
3. 服务层**不能引用 WPF 类型**，这样测试项目可以纯 `net8.0` 运行。
4. **时间可注入**：用 .NET 8 自带的 `TimeProvider`（`System.TimeProvider`），默认 `TimeProvider.System`；所有“现在”都从它取，不要直接写 `DateTime.Now`。
5. **数据目录可注入**：构造函数接收目录路径，默认 `%LocalAppData%\MiyaIsland`；测试用临时目录。
6. 统一用**本地时间**（`DateTime`，Kind=Local）。
7. 交付**完整文件**，不要 diff。
8. 回复最开头先列出**你做了哪些和本文不同的假设或改动**，没有就写“无”。

## 3. 任务 A · Tasks & Reminders 数据层（v0.7.0，最高优先级）

### 新建文件
- `Models/TaskItem.cs`（模型 + `RepeatKind` 枚举 + `TaskReminder`）
- `Services/TaskStore.cs`（读写、备份、迁移）
- `Services/TaskScheduler.cs`（重复规则、到点判断、完成 / 延后 / 忽略、启动补偿、摘要）

### 数据格式 `tasks.json`（version 1）
```json
{
  "version": 1,
  "tasks": [
    {
      "id": "8f1c…",
      "title": "给妈妈回电话",
      "done": false,
      "createdAt": "2026-10-04T09:12:00+08:00",
      "completedAt": null,
      "remind": { "time": "21:30", "date": null, "repeat": "weekly", "weekdays": [1, 3, 5] },
      "snoozedUntil": null,
      "lastFiredFor": "2026-10-03T21:30:00+08:00",
      "lastCompletedFor": null
    }
  ]
}
```
- `remind` 可以为 `null`（不提醒的普通任务）。
- `repeat`：`"none" | "daily" | "weekly"`。`none` 时 `date` 必填（`yyyy-MM-dd`）；`weekly` 时 `weekdays` 用 `DayOfWeek` 数值（0=周日…6=周六），为空时按 `none` 处理并记日志。
- `time` 用 `HH:mm`，按 InvariantCulture 解析。
- JSON 用 camelCase，枚举存成小写字符串。

### `TaskStore`
- `Load()`：
  1. 有 `tasks.json` 就读；损坏时改读 `tasks.json.bak`；两个都坏，就把坏文件改名为 `tasks.corrupt-yyyyMMddHHmmss.json`，返回空列表。**绝不静默覆盖用户数据。**
  2. 没有 `tasks.json` 但有 `reminders.json` 时迁移：每条 `Reminder` 转成一次性任务（`date` + `time` 取自 `DueAt`，`done = Completed`，`Notified == true` 时 `lastFiredFor = DueAt`）。保存成功后，把 `reminders.json` 改名为 `reminders.v06.bak.json`。
  3. 加载时清理“已完成且 `completedAt` 早于 N 天”的一次性任务（N 由参数传入，默认 7）。
- `Save(IEnumerable<TaskItem>)`：先写 `tasks.json.tmp`，再 `File.Replace(tmp, tasks.json, tasks.json.bak)`；目标文件不存在时用 `File.Move`。内部加锁，线程安全。

### `TaskScheduler`
```csharp
DateTime? CurrentOccurrence(TaskItem t, DateTime now);     // 最近一次 <= now 的触发时间，没有则 null
DateTime? NextOccurrence(TaskItem t, DateTime after);      // 严格晚于 after 的下一次
IReadOnlyList<(TaskItem Task, DateTime Occurrence)> GetDue(DateTime now);
void MarkFired(TaskItem t, DateTime occurrence);
void Complete(TaskItem t, DateTime now);   // 一次性：done=true + completedAt；重复：lastCompletedFor=当前这次，任务保持未完成
void Uncomplete(TaskItem t);               // 取消勾选
void Snooze(TaskItem t, DateTime until);   // UI 提供 10 分钟 / 30 分钟 / 1 小时 / 明天同一时间
void Dismiss(TaskItem t, DateTime occurrence); // = 只跳过这一次，不完成
StartupReport CatchUpOnStartup(DateTime now);
TaskSummary GetSummary(DateTime now);
```
规则：
- `GetDue`：未完成，有当前触发时间，且该时间 > `lastFiredFor`、> `lastCompletedFor`，同时（`snoozedUntil` 为空或 <= now）。延后到点后，即使 `lastFiredFor` 已等于这次，也要再触发一次（触发后清空 `snoozedUntil`）。
- `CatchUpOnStartup`：程序关着期间错过的一次性提醒**不触发**，标记为逾期（`lastFiredFor` 设成它的时间），计入 `OverdueCount`；重复任务把 `lastFiredFor` 设成最近一次过去的触发时间，避免启动时连弹。返回 `StartupReport { OverdueCount, MissedTitles }`。
- `GetSummary`：
  - `TodayCount` = 今天有触发的未完成任务 + 没有提醒的未完成任务；
  - `OverdueCount` = 已过时间、仍未完成的一次性任务；
  - `Next` = 今天之后最近一次触发（标题 + 时间），可以为空。
- “明天同一时间”= 这次触发时间 + 1 天。

### 测试（必须）
新建 `tests/MiyaIsland.Core.Tests/MiyaIsland.Core.Tests.csproj`（xUnit，`net8.0`），**用 `<Compile Include="..\..\Models\TaskItem.cs" Link="…" />` 链接源文件**，不要引用 WPF 主项目。至少覆盖：
1. 迁移：`reminders.json` → `tasks.json`，字段正确，旧文件改名。
2. 主文件损坏时读备份；两个都坏时不覆盖，坏文件改名保留。
3. `Save` 后再 `Load`，内容一致；会生成 `.bak`。
4. 每天 / 每周（多选周一三五）/ 一次性的 `NextOccurrence`，包括跨周、跨月、跨年。
5. `GetDue` + `MarkFired`：同一次只触发一次。
6. `Snooze` 到点后再次触发；`Dismiss` 后这次不再触发，下一次照常。
7. 重复任务 `Complete` 后保持未完成，下一次照常提醒。
8. `CatchUpOnStartup`：错过 3 个一次性 + 1 个每日任务，不连弹，`OverdueCount = 3`。
9. 7 天前完成的一次性任务在加载时被清理。

## 4. 任务 B · 启动问候引擎（v0.7.1，A 完成后再做）

### 新建文件
- `Models/GreetingContext.cs`
- `Services/GreetingService.cs`
- `Assets/greetings.zh-CN.json`（内容见第 6 节，**原样使用**）

### 规则
- **出现条件**：启用；距离 `LastGreetingAt` ≥ 4 小时；每次启动最多一次（由调用方保证只调一次）。
- **候选**：
  - 当前时段组：dawn 5–8，morning 8–11，noon 11–13，afternoon 13–18，dusk 18–20，evening 20–23，night 23–5；
  - 任务状态组：有逾期 → `overdue`；今天有任务 → `todo`；今天的都完成了 → `done`；没有任务 → `empty`；
  - 特殊组：第一次启动 `first`；距上次启动 ≥ 3 天 `back`；周一 `monday`；周五 `friday`；周六日 `weekend`。
- **权重**：时段 1；任务状态 1.2（`overdue` 2.5）；`monday / friday / weekend` 1.6；`first / back` 4；自定义句子 2。填了称呼时，带 `{name}` 的句子再 ×1.5。
- **称呼为空时**，排除所有含 `{name}` 的句子。
- **不重复**：排除最近用过的 8 句（按模板原文比较）；全部被排除时忽略这条规则。
- **文案来源**：`BuiltInAndCustom`（默认）或 `CustomOnly`；`CustomOnly` 但自定义为空时退回内置。
- **占位符**：`{name}` `{count}` `{overdue}` `{next}`（超过 10 个字截断加“…”）`{nexttime}`（HH:mm）`{weekday}`（周一…周日）。缺值时，含该占位符的句子不进候选。
- **第二行**（`Subtitle`）是客观摘要：
  - `todo`：`今天 3 件 · 下一件 21:30 给妈妈回电话`
  - `overdue`：`1 件已过期 · 交房租`
  - `done`：`今天完成了 5 件`
  - `empty`：`今天没有安排`
- **随机**：注入 `Random`，方便测试。

### API
```csharp
GreetingResult? TryGetStartupGreeting(GreetingContext ctx); // 不满足出现条件时返回 null
GreetingResult Pick(GreetingContext ctx);                   // “试一下”用，不检查 4 小时
// GreetingResult { string Title; string Subtitle; string TemplateUsed }
// 由调用方把 TemplateUsed 存进 RecentGreetings、更新 LastGreetingAt
```
`GreetingContext` 字段：`Now`、`Nickname`、`IsFirstRun`、`LastLaunchAt`、`LastGreetingAt`、`Enabled`、`Source`、`CustomLines`、`RecentTemplates`、`Summary`（`TodayCount`、`OverdueCount`、`DoneTodayCount`、`NextTitle`、`NextTime`）。

### 测试
1. 称呼为空时永远不会出现含 `{name}` 的句子。
2. 4 小时内返回 null。
3. 最近 8 句不重复。
4. `CustomOnly` + 空列表时退回内置。
5. 占位符替换和 10 字截断。
6. 各时段边界：4:59 → night，5:00 → dawn，22:59 → evening，23:00 → night。

## 5. 任务 C · 开机自启动（v0.7.1，很小）

新建 `Services/AutostartService.cs`（Windows 专用，可以用 `Microsoft.Win32.Registry`）：
- 位置：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `MiyaIsland`，值为 `"<exe 完整路径>" --autostart`。
- API：`bool IsEnabled()`、`string? GetRegisteredExePath()`、`void Enable(string exePath)`、`void Disable()`、`bool IsPathStale(string currentExePath)`（路径大小写不敏感比较）。
- 当前 exe 路径用 `Environment.ProcessPath`，由调用方传入。
- 注册表操作失败时抛出带中文说明的异常，由 UI 显示。

## 6. `Assets/greetings.zh-CN.json`（原样使用）

```json
{
  "version": 1,
  "groups": {
    "dawn": [
      "早安，{name}。今天也慢慢醒过来就好",
      "{name}起得好早，先喝杯温水吧",
      "天刚亮，{name}今天想先做哪件事？",
      "早安。新的一天，从一件小事开始",
      "这么早就见面啦，今天会是安静的一天"
    ],
    "morning": [
      "早上好，{name}今天打算做点什么？",
      "{name}，很高兴今天又见到你",
      "上午好，状态不错的话先做最难的那件",
      "早上好，今天也一起慢慢来",
      "早呀，今天的第一首歌放什么？"
    ],
    "noon": [
      "中午了，{name}记得好好吃饭",
      "先吃饭，事情不会跑掉的",
      "{name}，午饭想好吃什么了吗？",
      "午安，吃完饭可以眯一小会儿"
    ],
    "afternoon": [
      "下午好，{name}，困了就起来走两步",
      "下午茶时间到了吗？",
      "今天已经过去一大半，你做得很好",
      "{name}，喝口水再继续吧",
      "下午好，再专注一小会儿就好"
    ],
    "dusk": [
      "傍晚好，{name}，今天辛苦了",
      "天快黑了，给自己放首歌吧",
      "{name}，晚饭吃点好的",
      "一天快结束了，剩下的慢慢来"
    ],
    "evening": [
      "晚上好，{name}，今天过得怎么样？",
      "{name}回来啦，欢迎回家",
      "晚上的时间留给喜欢的事吧",
      "晚上好，要不要听点轻一点的歌？"
    ],
    "night": [
      "夜深了，{name}，早点休息好吗",
      "这么晚还在呀，别熬太久",
      "深夜了，音乐小声一点，心情放松一点",
      "{name}，剩下的事交给明天的你"
    ],
    "todo": [
      "今天有 {count} 件事，先从最小的那件开始吧",
      "{name}，今天有 {count} 件事等你，一件一件来",
      "下一件是「{next}」，{nexttime} 提醒你",
      "今天的清单有 {count} 件，不着急，慢慢划掉"
    ],
    "overdue": [
      "有 {overdue} 件事过期了，要不要现在处理掉？",
      "{name}，「{next}」还在等你哦",
      "过期的事先别焦虑，挑一件最快的解决它",
      "先把「{next}」做完，后面就轻松了"
    ],
    "done": [
      "今天的事都做完了，{name}真棒",
      "清单已清空，剩下的时间都是你的",
      "全部完成，可以心安理得地发呆了",
      "{name}今天好厉害，奖励自己一首歌吧"
    ],
    "empty": [
      "今天还没有安排，想到什么随时加进来",
      "空白的一天也很好，{name}想做点什么？",
      "今天很自由，想做什么都可以"
    ],
    "monday": [
      "周一好，{name}，这周也会顺顺利利的",
      "新的一周开始了，先定一件最重要的事"
    ],
    "friday": [
      "周五啦，{name}再坚持一下就是周末",
      "周五了，今天可以对自己好一点"
    ],
    "weekend": [
      "周末好，{name}，今天允许自己慢一点",
      "周末也来看我啦，那就陪你一起"
    ],
    "back": [
      "{name}好久不见，有点想你",
      "欢迎回来，这几天过得还好吗？"
    ],
    "first": [
      "你好，我是 Miya Island，以后请多指教",
      "初次见面，{name}，我会安静地待在屏幕顶上"
    ]
  }
}
```

## 7. 交付方式

- **能推 GitHub**：从 `claude/serene-lamport-3dd2qr` 新建分支 `gpt/v0.7-services`，按任务分开提交（A、B、C 各一个提交），推上去后把分支名告诉用户。
- **不能推**：把所有新文件按路径完整贴出来，或者打成 zip 交给用户，再转交给 Claude。
- Claude 会审查、合并、接上 UI，有问题会通过用户反馈给你。
