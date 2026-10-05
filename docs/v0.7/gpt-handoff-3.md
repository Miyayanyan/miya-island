# Miya Island v0.7 · 给 GPT 的任务包 #3（大包）

> 任务包 #2 已经合并进主界面，谢谢！用户看了样例，要求**按钮改成圆角矩形**，这是我任务包写错了，已经修好：新增 `Radius.Button = 8`，只有播放键（Lens）保持圆形。**之后所有新界面的按钮都请用圆角矩形。**
>
> 这一包是 v0.7.0 剩下的主体工程，量比较大。**这次你可以修改现有文件**（包括 `MainWindow`、`App`、`FloatingLyricsWindow`、`AppearanceSettings`、csproj）。你做的这段时间，Claude 不改代码，只在你交付后审查，所以不会有冲突。

## 0. 起点与通用规则

- 从最新的 `claude/serene-lamport-3dd2qr`（`1fad02c` 或更新）新建 **`gpt/v0.7-features`**。按下面 4 个任务的顺序做，每个任务至少一个提交，每次推送 Actions 都要绿。
- 先读这几个文件，理解现在的结构：
  - `MainWindow.xaml(.cs)`：固定画布、`MorphTo` / `ApplyMorph` / `SetIslandShape`、`ShowCompact`、`_horizontalCenter`；
  - `FloatingLyricsWindow.xaml(.cs)`；
  - `Themes/*`；
  - `docs/v0.7/*`。
- **必须保留的现有行为**：
  - 呼吸灯：`StartBreathingAnimation()` 的逻辑和参数都不改；
  - 位置锁定；
  - 70%~160% 缩放；
  - 重新居中；
  - 歌词匹配、LRC 导入、下载、J-Space、所有外观设置及其自动保存。
- **旧设置文件要能读**：给 `AppearanceSettings` 新增字段时都要有默认值。
- **视觉规则**：
  - 颜色只用 Tokens；
  - Effect 不包住文字；
  - 按钮用已有样式（`Button.Primary` / `Button.Ghost` / `Button.Icon` / 隐式玻璃按钮）；
  - 开关用 `ToggleSwitch`，下拉用隐式 `ComboBox`，输入框用 `c:Placeholder.Text`。
- **动画**：容器变形统一走现有的 `MorphTo`（一个进度值同时驱动尺寸、圆角、阴影、内容透明度）。不要再直接对窗口的 Width / Height 做动画，窗口大小始终是固定画布。

## 任务 1 · 删除桌宠

- **删除**：`PetWindow.xaml(.cs)`、`Models/PetState.cs`、`Services/PetStateService.cs`、`Assets/MiyaPet.png` 及其在 csproj 里的 Resource，以及 `MainWindow` 里所有桌宠相关代码：`PetPanel`、♛ 页签、`_petWindow`、`_petState`、`SetMusicPlaying`、`RecallToIsland` 调用等。
- 用户电脑上的 `pet.json` **不要删**，只是不再读取。
- 顶部页签变成 5 个：**音乐 / 任务 / 下载 / J-Space / 设置**。原来的“提醒”页签改名为“任务”，图标用 `Icon.Tasks`。
- README 和《先看这里》先不用改，最后由 Claude 统一改。

## 任务 2 · Tasks & Reminders 界面

用你写的 `TaskStore` + `ReminderScheduler` 替换现在的提醒面板。删除 `Models/Reminder.cs` 和 `Services/ReminderService.cs`，迁移已经由 `TaskStore` 负责。

### 数据
- 启动时：`Load()`，然后 `CatchUpOnStartup(now)`。
- 每次改动后：`Save()`。
- 继续用现有的 10 秒定时器检查 `GetDue(now)`。

### 面板布局（展开态内容区宽 406）

**1. 添加行**
- 输入框：占位文字“添加任务…”，按回车直接添加。
- 时间按钮：显示当前选择，例如“不提醒”或“今天 21:30”。点开后是一个玻璃弹层（`Popup`，外观照搬 ComboBox 弹层），里面有：
  - **提醒时间**：不提醒 / 今天（下一个整点或半点）/ 明天 09:00 / 自定义（一个时间输入 + 一个日期输入，格式错误时就地标红提示）；
  - **重复**：三段式“不重复 / 每天 / 每周”；选“每周”时显示 一 ~ 日 七个可以多选的小按钮。
- 添加按钮：`Button.Primary`，内容是 `Icon.Add`。

**2. 任务列表**（可滚动，行高 36）
- 每行依次是：
  - **圆形勾选框**：18px。新建 `Themes/Controls/TaskCheck.xaml`，`x:Key="TaskCheck"` 的 CheckBox 样式。未完成是 1.5px 的 `Brush.Ink3` 圆圈；完成后是 `Brush.Primary` 填充，再用 220ms 画出白色对勾。
  - **标题**：超长时省略号截断。
  - **时间标签**：11px，圆角 6，`Brush.Well` 底，内容是图标 + 文字：
    - 重复任务：`Icon.Repeat` +“每天 21:30”或“一 三 五 19:00”；
    - 一次性任务：`Icon.Bell` +“今天 23:00”或“10-05 09:00”；
    - 今天还没到时间的：橙色 `Brush.Warning`；
    - 已过期：`Brush.Overdue`，文字“已过期 · 昨天 21:00”。
  - **“更多”按钮**：`Button.Icon`，24×24，只在鼠标悬停这一行时显示。菜单里有：编辑（在原位改标题，时间用同一个弹层）/ 删除。
- 已完成的任务折叠在列表底部：“已完成 (n)”，可以展开，旁边有“清除”。
- 列表为空时显示一行 `Brush.Ink3` 的提示：“还没有任务，在上面写一件吧”。

**3. 提醒卡**（到点时）
- 小岛变形成一张卡片：新增一个形状 `RemindShape = (380, 136, 26)`，用 `MorphTo` 过去（展开曲线）。
- 卡片内容是一个新的内容层，和 `ExpandedContent` 一样按最终尺寸排版、用透明度过渡。请把 `ApplyMorph` 里只处理 `ExpandedContent` 的部分改成通用的：**当前要显示的内容层淡入，其他层淡出**。
- 卡片上的内容：
  - 左上角 30px 的透镜圆，里面是 `Icon.Bell`；
  - 标题：14 SemiBold；
  - 第二行：12 `Brush.Ink2`，例如“今天 21:30 · 每天重复”；
  - 右下角三个按钮：「忽略」（Ghost）、「延后 ▾」（菜单：10 分钟 / 30 分钟 / 1 小时 / 明天同一时间）、「完成」（Primary）。
- 卡片显示期间，**鼠标移开不自动收起**，直到点了某个按钮。
- 多个提醒同时到点时排队，一个处理完再显示下一个。
- 处理完以后回到之前的状态：收起或 Compact。收起态文字显示结果，例如“已完成：给妈妈回电话”。

## 任务 3 · MusicSession + 悬浮歌词独立 + 托盘

### `Services/MusicSession.cs`
把 `MainWindow.RefreshMediaAsync` / `UpdateLyrics` / `ImportLyrics_Click` 里的**媒体和歌词逻辑**原样搬进来，`MediaService` 和 `LyricService` 不改。
- 每 500ms 轮询一次，用 `DispatcherTimer`；保留防重入。
- 行为必须完全一致：
  - 换歌判断用 `MakeTrackKey`；
  - 换歌后自动拉取歌词；
  - 进度早于第一句时，当前句显示 `♪`，下一句显示第一句；
  - 四种状态文案原样：`播放音乐后自动匹配歌词` / `正在匹配歌词…` / `暂未找到同步歌词，可导入 LRC` / `歌词获取失败，可稍后重试或导入 LRC`。
- **API 建议**：
  - 事件：`StateChanged(MusicState)`、`TrackChanged(MusicState)`；
  - 方法：`StartAsync()`、`TogglePlayAsync()`、`NextAsync()`、`PreviousAsync()`、`ImportLyricsAsync(path)`；
  - `MusicState` 字段：`HasSession`、`Title`、`Artist`、`IsPlaying`、`Position`、`Duration`、`PreviousLine`、`CurrentLine`、`NextLine`、`HasLyrics`、`TrackKey`。
- **测试**：把“按播放进度定位上一句 / 当前句 / 下一句”抽成纯静态方法 `LyricCursor.Locate(lines, position)`，并写单元测试。

### 接入
- `MainWindow` 和 `FloatingLyricsWindow` 都订阅同一个 `MusicSession`，MainWindow 不再自己轮询。原来 UI 层的规则保持不变：
  - `SetCompactMusicDisplay`；
  - 悬浮歌词打开时，主岛显示待机标题；
  - 歌词窗的文案。

### 窗口生命周期
- `App.xaml` 去掉 `StartupUri`，`ShutdownMode="OnExplicitShutdown"`。由 `App.xaml.cs` 创建 `MusicSession`、`MainWindow`、按需创建的悬浮歌词窗口，以及托盘图标。
- `FloatingLyricsWindow` 去掉 `Owner`。关闭按钮改为隐藏，并记住 `ShowLyrics=false`。
- `AppearanceSettings` 新增 `ShowIsland`（默认 true）和 `ShowLyrics`（默认 false），启动时恢复上次的组合。支持三种情况：只开小岛 / 只开歌词 / 两个都开。
- **托盘图标**：
  - 用 `System.Windows.Forms.NotifyIcon`，项目已经开了 `UseWindowsForms`；
  - 图标：用 `IconView` 画出“胶囊 + 紫色圆点”的样子，用 `RenderTargetBitmap` 转成 16 / 32px 的 Icon，或者你自己做一个 `.ico`；
  - 菜单：显示 / 隐藏灵动岛、显示 / 隐藏悬浮歌词、重新居中、退出；
  - 双击托盘图标 = 显示灵动岛。
- 设置页的「退出」改为 `Application.Current.Shutdown()`。退出前停掉所有定时器，释放 `VolumeService` 和托盘图标。

## 任务 4 · Compact Mode + 位置体验

### 两种 Compact 形状
- **小胶囊**：104×30，圆角 15，**默认**。
- **小圆**：32×32，圆角 16。
- `AppearanceSettings.CompactStyle`：`"Pill"`（默认）或 `"Orb"`。设置页加一个下拉框“Compact 样式：小胶囊 / 小圆”，再加一个开关“启动时为 Compact”（`StartCompact`，默认关）。

### 进入和退出
- 展开态顶栏，在锁按钮旁边加一个 `Icon.Minimize` 按钮（`Button.Icon`），只在展开态显示。点击后变形到 Compact。
- **Compact 下悬停不展开**。单击直接展开到展开态；按下后移动超过 4px 算拖动（锁定时不能拖，但单击仍可展开）。
- 从展开态因为鼠标离开而收起时，回到它原来的状态：从 Compact 展开的回 Compact，从收起态展开的回收起态。

### 内容和动画
- **呼吸灯必须是同一个 `StatusIndicator` 元素**，不要复制一个新的。它的位置随变形平滑移动：
  - 小胶囊：左侧 11px，垂直居中；
  - 小圆：正中；
  - 收起态 / 展开态：保持现在的位置。
- 小胶囊里的内容：
  - 播放时：3 根 2px 宽的律动条（`Brush.Ink2`）+ 当前时间（11 SemiBold 等宽数字）；
  - 有到期任务时：`Icon.Bell` + 数量；
  - 其他情况：只显示时间。
- 小圆里只有呼吸灯。
- Compact 和其他状态之间的变形：弹簧曲线 300ms。

### 位置
- **吸附只在松开鼠标时判断**：
  - 岛的水平中心离工作区中心 ≤ 16 DIP，就用 160ms 滑到正中；
  - 岛的顶部离默认位置（工作区顶部 + 8）≤ 10 DIP，就对齐到默认位置；
  - 按住 Alt 松开时不吸附；
  - 拖动过程中不做磁吸。
- **记住位置**：保存岛本身的位置和所在显示器的名称（`Screen.FromPoint`），新增字段 `IslandLeft`、`IslandTop`、`IslandMonitor`。启动时，如果那块屏幕还在、并且岛至少有 80px 在屏幕内，就恢复；否则默认居中。
- `_horizontalCenter` 继续是唯一的水平定位基准。
- **悬浮歌词**用同样的规则：吸附到水平中心和默认底部位置（工作区底部往上 70 DIP）。

## 交付

- 推到 `gpt/v0.7-features`，每个任务分开提交，Actions 全绿再告诉用户。
- 回复里列出：
  1. 和本文不同的地方；
  2. 你觉得行为上有风险、需要 Claude 重点复审的地方；
  3. 截图：任务面板（有过期、今天、重复、已完成各一条）、提醒卡、小胶囊和小圆、托盘菜单。
- 如果某个任务卡住了，先把前面已经完成的部分推上去，再说明卡在哪里。
