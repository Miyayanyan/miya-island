# Miya Island v0.7.1

Windows 10/11 桌面上的小岛：音乐、任务与提醒、下载、截图，以及独立的悬浮歌词。外观是 Liquid Glass 风格，支持平滑展开、收起和位置记忆。

## 使用

在 [Releases](../../releases) 页面下载，先退出旧版，再双击打开。单个文件，不用安装。

- **`MiyaIsland.exe`（完整版，推荐）**：约 75 MB，下载后直接就能用。
- **`MiyaIsland-Lite.exe`（轻量版）**：只有几 MB，下载快。需要电脑装有 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)；没装的话，第一次打开时 Windows 会提示并带你去微软官网下载，装一次就好。

- **普通条**：鼠标移上去展开；**小胶囊 / 小圆**：单击展开。鼠标离开后会回到你选的收起样式。
- **音乐**：系统媒体会话，上一首 / 播放 / 下一首，系统音量，在线同步歌词，也可以导入本地 LRC。没找到歌词时，点歌词框可以重新查找。
- **任务**：添加、完成、删除；一次性、每天、每周指定几天提醒。提醒卡可以选完成、延后或忽略。
- **下载**：粘贴直接下载链接，选保存位置，查看进度。
- **截图**：小岛先收起，再打开 Windows 截图工具（也可以按 Win + Shift + S）。
- **悬浮歌词**：独立于小岛，可以单行或双行显示。拖到屏幕中线附近会自动吸附并显示参考线（按住 Alt 不吸附）；「⋯」菜单里有一键「水平居中」、锁定位置、字号、对齐、时钟和封面。
- **外观**：Milk / Rose / Lavender / Midnight / Clear Glass 五种玻璃风格，也可以自定义颜色和浓度；背景模糊和模糊强度；呼吸灯颜色；头像与开机问候；安静模式。
- **截图和录屏时隐藏小岛**：可以在设置或托盘菜单里开关。背景模糊需要它开着（模糊是截取小岛后面的画面做的），关掉后模糊会暂停。
- 找不到窗口时，去任务栏右下角的托盘找小岛图标。

音乐软件需要把正在播放的歌曲告诉 Windows（系统媒体控制）。简单的判断方法：放歌时按一下键盘的音量键，如果音量弹窗里能看到歌名和封面，小岛就能识别。Spotify、浏览器里的网页播放器等都支持；部分音乐软件的旧版本不支持，更新到最新版通常就好了。

## 隐私

任务、设置、头像和歌词缓存都只保存在本机 `%LocalAppData%\MiyaIsland`。

查找在线歌词时，会把**歌名、歌手和时长**发给 [LRCLIB](https://lrclib.net)；没找到时，再发给网易云音乐的公开搜索接口。网易云的接口是非官方的，以后可能会失效，失效时小岛会静默跳过它。除此之外不会联网，也不收集任何使用数据。

## 从源码运行

安装 .NET 8 SDK（或 Visual Studio 2022 的“.NET 桌面开发”），打开 `MiyaIsland.sln` 运行。验证与单文件发布：

```powershell
dotnet build -c Release
dotnet test tests/MiyaIsland.Core.Tests -c Release
dotnet publish MiyaIsland.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

`docs/v0.7/` 里是这一版的设计方案、任务说明和验收报告。

## 致谢

- 玻璃效果的视觉参考来自 JoyTaung 的 Figma 社区文件「iOS 26 Liquid Glass Simulation」。本项目的设计稿和代码是另外实现的，没有使用其中的素材。
- 音频控制使用 [NAudio](https://github.com/naudio/NAudio)（MIT）。
- 在线歌词来自 [LRCLIB](https://lrclib.net)。
- 由 Miyayanyan 设计和测试，Claude 与 Codex 协作开发。

## 许可证

[MIT](LICENSE)
