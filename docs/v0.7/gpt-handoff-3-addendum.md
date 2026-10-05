# 任务包 #3 · 补充：J-Space 换成「截图」

> 用户刚决定的改动，请并入任务包 #3。可以放进任务 1 一起做，也可以单独一个提交。

## 1. 删除 J-Space
- 删除 `AiPanel`、J-Space 页签、`OpenJSpace_Click`，以及代码里的 J-Space 网址。
- 删除图标 `Icon.Jspace`：`Themes/Icons.xaml` 里那一条，确认没有其他地方引用。

## 2. 新增「截图」（v0.7：调用系统截图）
- 页签顺序：**音乐 / 任务 / 下载 / 截图 / 设置**。
- 新图标：在 `Themes/Icons.xaml` 里加 `Icon.Screenshot`（四个圆角取景框 + 中间一个圆），路径直接用：

```xml
<!-- 截图 -->
<PathGeometry x:Key="Icon.Screenshot" Figures="M 4 8.5 V 6.5 A 2.5 2.5 0 0 1 6.5 4 H 8.5 M 15.5 4 H 17.5 A 2.5 2.5 0 0 1 20 6.5 V 8.5 M 20 15.5 V 17.5 A 2.5 2.5 0 0 1 17.5 20 H 15.5 M 8.5 20 H 6.5 A 2.5 2.5 0 0 1 4 17.5 V 15.5 M 8.8 12 A 3.2 3.2 0 1 1 15.2 12 A 3.2 3.2 0 1 1 8.8 12 Z" />
```

- **面板** `ScreenshotPanel`：
  - 标题：「截图」（`FontSize.Headline`）。
  - 说明（`Brush.Ink2`）：「框选区域、窗口或整个屏幕，截完会自动复制到剪贴板。」
  - 按钮：「开始截图」，`Button.Primary`，带 `Icon.Screenshot`。
  - 底部小字（`Brush.Ink3`）：「也可以随时按 Win + Shift + S」。
- **点击「开始截图」**：
  1. 先让小岛收起（悬浮歌词不动），等收起动画结束，大约 300ms，避免小岛被截进去；
  2. 用 `Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true })` 打开系统截图；
  3. 失败时依次尝试：`snippingtool.exe /clip` → 收起态文字显示「无法打开系统截图工具」；
  4. 不需要等待截图结果。
- **托盘菜单**加一项「截图」，行为相同。

## 3. 以后的版本（这次不做）
小岛自己的截图功能：框选区域，自动保存到「图片」文件夹并复制到剪贴板，小岛里显示最近一张的缩略图。Claude 会写进 v0.7 之后的计划，这次只做系统截图。
