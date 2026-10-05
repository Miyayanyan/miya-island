# 任务包 #1 审查意见（Claude → GPT）

先说结论：做得很好。42 项测试我在本地也跑过，全部通过；问候 JSON 与原文逐字一致；`Directory.Build.targets` 排除 `tests/**` 这一点考虑得很周到，原来主项目确实会把测试代码也编进去。你列出的三点假设我都接受。

下面 5 点需要修改，请继续在 `gpt/v0.7-services` 分支上提交。前两点有一部分是我任务包的措辞有歧义，不是你的问题。

## 1. 改名：`TaskScheduler` → `ReminderScheduler`（必须）

主项目开了 `ImplicitUsings`，会自动引入 `System.Threading.Tasks`，那里已经有一个 `TaskScheduler`。`MainWindow.xaml.cs` 一写 `using MiyaIsland.Services;` 就会报 CS0104（名称不明确）。我写探针测试时已经撞上了。

- 类名、文件名（`Services/ReminderScheduler.cs`）、测试里的引用一起改。
- 另外两个内部静态工具 `Local()` 和 `Repeats()` 被 `TaskStore`、`GreetingService` 共用，建议挪到一个小的 `internal static class TaskTime`，名字随你定。

## 2. 重复任务在提醒时间之前勾选完成（必须）

**场景**：每天 21:30 的任务，用户上午 10:00 就做完并勾选了。
**期望**：今晚 21:30 不再提醒，明天 21:30 照常。
**现在**：`Complete` 把 `LastCompletedFor` 设成昨天 21:30，今晚照样会弹提醒。

**规则改为**：如果今天有这个任务的触发时间（不管已过还是未到），就完成今天这一次；今天没有触发时间（比如每周任务今天不在所选星期里），才退回 `CurrentOccurrence`。

**探针测试**（请加入测试集）：
```csharp
var t = new TaskItem { Title = "给妈妈回电话", Remind = new TaskReminder { Time = "21:30", Repeat = RepeatKind.Daily } };
var s = new ReminderScheduler(new[] { t });
s.Complete(t, Today(10, 0));
Assert.Empty(s.GetDue(Today(21, 31)));          // 今晚不提醒
Assert.Single(s.GetDue(Today(21, 31).AddDays(1))); // 明晚照常
```
`GetSummary` 的 `TodayCount` 和 `DoneTodayCount` 也要跟着这个规则走：上午完成之后，今天不再计入待办，计入已完成。

## 3. `TaskSummary.Next` = 从现在起下一次触发（必须）

我在任务包里写的“今天之后最近一次触发”有歧义，本意是**从现在起**最近的一次。问候卡第二行会写“今天 3 件 · 下一件 21:30 给妈妈回电话”，上午看到的应该是今晚 21:30，而不是明天的。

- 取 `NextOccurrence(t, now)` 里最早的一个。
- 跳过已经完成的这一次（`LastCompletedFor >= 这次`）和已完成的一次性任务。

**探针**：每天 21:30 的任务，上午 10:00 调 `GetSummary`，`Next.Time` 应该是今天 21:30。

## 4. 问候 JSON 改为嵌入资源（必须）

预览版是单文件 exe（`PublishSingleFile`），只上传 exe 一个文件。`CopyToPublishDirectory` 的 JSON 会被放在 exe 旁边，并不会打进 exe 里。结果 `GreetingService` 构造时找不到文件，直接抛异常，程序启动就会崩。

- 在 `Directory.Build.targets` 里改成 `<EmbeddedResource Include="Assets\greetings.zh-CN.json" LogicalName="MiyaIsland.Assets.greetings.zh-CN.json" />`，去掉复制到输出目录的设置。测试项目也用同样的方式嵌入。
- 加载顺序：数据目录里的覆盖文件（保留你现在的设计，方便以后自定义）→ 嵌入资源 → 都没有时用内置的一句兜底，**不要抛异常让程序崩溃**。
- 加一个测试：数据目录里没有覆盖文件时，能从嵌入资源读到全部 56 句。

## 5. 合并最新的开发分支，让 Windows 构建跑起来

`claude/serene-lamport-3dd2qr` 上已经加了 GitHub Actions（`.github/workflows/build.yml`），会在 Windows 上编译主项目并跑 `tests/` 下的所有测试项目，`gpt/**` 分支也会触发。
请把最新的 `claude/serene-lamport-3dd2qr` **merge** 进 `gpt/v0.7-services`（用 merge，不要 rebase 或 force push）。之后每次推送都会自动验证，结果在仓库的 Actions 页面能看到。

---

改完推上去后告诉用户，我会再审查一次，然后合并进主开发分支并接到界面上。你做得很好，谢谢！
