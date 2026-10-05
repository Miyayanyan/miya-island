# Miya Island v0.7 · 给 GPT 的任务包 #2：控件样式

> Claude 负责主导和集成。任务 #1 已经合并，谢谢！这次是第二步：一整套控件样式。

## 0. 起点

- 从最新的 `claude/serene-lamport-3dd2qr`（提交 `989cf6d` 或更新）新建分支 **`gpt/v0.7-controls`**。
- 先读这几个已有文件：
  - `Themes/Tokens.xaml`：所有颜色、字号、圆角的 key。颜色用 `DynamicResource`，字号、圆角、字体用 `StaticResource`。
  - `Themes/Icons.xaml`：图标几何。
  - `Controls/IconView.cs`：画图标的控件，用法 `<c:IconView Width="14" Height="14" Data="{StaticResource Icon.Check}" />`，命名空间 `xmlns:c="clr-namespace:MiyaIsland.Controls"`。

## 1. 分工与规则

- **只新建文件**，不改任何现有文件，包括 `App.xaml`、`MainWindow.xaml`、`Tokens.xaml`。接入（合并字典、给现有控件换样式）由 Claude 来做，因为 Claude 同时在改 `MainWindow`。
- **颜色只用 Tokens 里的 key**，不写死十六进制颜色。真的缺 key，就在你的文件里先定义一个 `x:Key="Brush.xxx"` 并在交付说明里列出来，Claude 会挪进 Tokens。
- **Effect 不能包住文字**：`DropShadowEffect`、`BlurEffect` 只能加在一个单独的装饰 `Border` 上，和放文字的元素是兄弟关系，不是父子关系。WPF 的 Effect 会把子元素整个栅格化，文字会发糊。
- **按压只缩放背景层，不缩放文字**：`RenderTransform`（缩放到 0.97）加在模板里的背景 `Border` 上，`ContentPresenter` 不缩放。
- 动画全部写在 `ControlTemplate.Triggers` 或 `VisualStateManager` 里，不需要后台代码。占位文字可以例外，见 §3.6。
- 每个控件都要有：**正常 / 悬停 / 按下 / 禁用（整体 40% 不透明）/ 键盘焦点**五种状态。焦点统一用 §3.10 的焦点环。
- 推送后 GitHub Actions 必须是绿色的（Windows 编译 + 现有 60 项测试）。

## 2. 新建文件

```
Themes/Controls.xaml            ← 只做一件事：合并下面所有字典（Claude 接入时只加这一行）
Themes/Controls/Buttons.xaml
Themes/Controls/ComboBox.xaml
Themes/Controls/Toggle.xaml
Themes/Controls/Slider.xaml
Themes/Controls/TextBox.xaml
Themes/Controls/ProgressBar.xaml
Themes/Controls/ScrollBar.xaml
Themes/Controls/ToolTip.xaml
Themes/Controls/ListBox.xaml
Themes/Controls/Focus.xaml
Controls/Placeholder.cs         ← 附加属性，见 §3.6
```

## 3. 规格

尺寸单位都是 DIP。悬停 / 按下的过渡统一 120ms，打开 / 关闭 160~240ms。

### 3.1 按钮 `Buttons.xaml`

| Key | 尺寸 | 正常 | 悬停 / 按下 |
|---|---|---|---|
| 隐式 `Button`（玻璃按钮，作为默认） | 高 28，左右内边距 12，胶囊圆角（高度的一半） | `Brush.Control` 底，`Brush.Ink` 文字，1px `Brush.Separator` 内描边，顶部 1px 白 14% 高光线 | `Brush.ControlHover` / 背景缩放 0.97 |
| `Button.Primary` | 同上 | `Brush.Primary` 渐变底，白字，顶部 1px 白 35% 高光 | 叠加 6% 白 / 0.97 |
| `Button.Ghost` | 同上 | 透明底，`Brush.Ink2` 文字 | `Brush.Control` 底，`Brush.Ink` 文字 |
| `Button.Danger` | 同上 | `Brush.Danger` 渐变底，白字 | 叠加 6% 白 / 0.97 |
| `Button.Icon` | 28×28 圆形，内容居中（放 18px 图标） | 透明，`Brush.Ink2` | `Brush.ControlHover`，`Brush.Ink` |
| `Button.Lens` | 40×40 圆形（播放键） | `Brush.Lens` 底 + 底部内高光（下方白 22% 渐变）+ 1px 淡紫描边 `#47C8B9FF` + 外投影（单独 Border，0 3 10 黑 18%） | 0.96 |

- 字号用 `FontSize.Caption`（12），`SemiBold`。
- 内容可以是文字，也可以是「IconView + 文字」的 StackPanel，两者都要居中对齐。

### 3.2 下拉框 `ComboBox.xaml`（最重要）

- **隐式 `ComboBox`**：高 28，圆角 `Radius.S`（10），`Brush.Control` 底，1px `Brush.Separator` 描边，左内边距 10，右侧 14px 的 `Icon.ChevronDown`（`Brush.Ink2`）。打开时箭头旋转 180°，200ms。
- **弹层**：`Popup` 设 `AllowsTransparency="True"`，外层留 12px 边距放投影：
  - 投影用单独一个 Border，两层投影：0 10 28 黑 32%、0 24 60 黑 14%；
  - 内容 Border：`Brush.IslandBase` 底，叠一层 `Brush.Control`；圆角 `Radius.M`（14）；1px 白 10% 描边；内边距 6；
  - 打开动画：透明度 0→1（160ms）+ 向下位移 −6→0（200ms，缓出）。
- **隐式 `ComboBoxItem`**：高 28，左右 8，圆角 8；悬停 `Brush.ControlHover`；选中项右侧显示 14px `Icon.Check`（颜色 `Brush.Accent`）。**不要出现系统蓝色高亮。**
- **ItemsPanel** 用 `VirtualizingStackPanel`，`MaxDropDownHeight` 320。字体列表有两百多项，必须虚拟化。
- **额外提供 `x:Key="FontFamilyItemTemplate"` 的 DataTemplate**：数据是 `System.Windows.Media.FontFamily`，每一项用该字体本身显示它的名字（`FontFamily="{Binding}"`，文字 `{Binding Source}`）。`FontFamilyCombo` 会用到。

### 3.3 开关 `Toggle.xaml`

- `x:Key="ToggleSwitch"`，`TargetType="CheckBox"`。
- 布局：`Content` 在左边、占满剩余宽度，开关在最右边；整行都可以点。
- 开关 36×20，圆形拇指 16（白色，0 1 3 黑 30% 投影），距边 2。
  - 关：轨道 `Brush.Separator`；开：轨道 `Brush.Primary`。
  - 拇指位移 220ms，用 `BackEase`（Amplitude 0.3，EaseOut）做出轻微回弹。

### 3.4 滑块 `Slider.xaml`

- 隐式 `Slider`（水平方向即可）：
  - 轨道高 4，圆角 2，未填充部分 `Brush.Separator`，已填充部分 `Brush.Primary`；
  - 拇指 14 白色圆形，带 E1 投影；悬停时放大到 16；
  - 命中区域高 24。
- 键盘左右键步进，沿用 WPF 默认行为。

### 3.5 输入框 `TextBox.xaml`

- 隐式 `TextBox`：高 30，圆角 10，`Brush.Control` 底，1px `Brush.Separator` 描边，内边距 10,0，文字垂直居中，光标色 `Brush.Ink`。
- 焦点状态：描边改成 `Brush.Accent` 70%，外面再加一圈 3px 的 `Brush.Accent` 25% 光环（单独 Border，不用 Effect）。

### 3.6 占位文字 `Controls/Placeholder.cs`

- 附加属性 `Placeholder.Text`（string），用法：`<TextBox c:Placeholder.Text="添加任务…" />`。
- 输入框为空且没有焦点时，显示 `Brush.Ink3` 颜色的占位文字。实现方式随你：Adorner，或者 TextBox 模板里绑定这个附加属性。
- 只能改这一个 C# 文件，不能动其他代码。

### 3.7 进度条 `ProgressBar.xaml`

- 隐式：高 4，圆角 2，轨道 `Brush.Separator`，填充 `Brush.Primary`。
- 要支持 `IsIndeterminate`：一段 30% 宽的填充左右循环滑动。

### 3.8 滚动条 `ScrollBar.xaml`

- 隐式 `ScrollBar`（竖直方向即可）：宽 6，悬停时 8，没有上下箭头按钮；轨道透明，拇指 `Brush.Ink3`，圆角 3。

### 3.9 提示框 `ToolTip.xaml`

- 隐式：圆角 8，字号 12，内边距 8,5，`Brush.IslandBase` 底叠 `Brush.Control`，1px 白 10% 描边，E2 投影。投影同样用单独的 Border。

### 3.10 列表与焦点 `ListBox.xaml` / `Focus.xaml`

- 隐式 `ListBoxItem`：圆角 8，悬停 `Brush.ControlHover`，选中 `Brush.Lens`。**去掉系统蓝色选中和虚线焦点框。**
- `Focus.xaml`：`x:Key="FocusRing"`，一个 `FocusVisualStyle`。效果是控件外 2px 的 `Brush.Accent` 圆角描边，圆角跟随控件（胶囊控件用大圆角即可）。上面所有控件都用它。

## 4. 交付

- 推到 `gpt/v0.7-controls`，Actions 是绿色的再告诉用户。
- 回复里列出：
  1. 和本文不同的地方；
  2. 新增的 Brush key（如果有）；
  3. 有没有哪个控件你觉得应该做个截图确认。
- Claude 接入后，会在预览版里让用户看实际效果。
