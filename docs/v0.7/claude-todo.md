# Claude 的待办（合并 GPT 任务包 #5 之后由 Claude 自己做）

- [x] **展开态拖动太难**（用户反馈）：只有顶栏能拖，而且顶栏空白区域很可能因为没有背景而无法命中，只有文字能拖。
  - 顶栏整条可以拖：设 `Background="Transparent"`，按钮除外；
  - 展开态面板的空白区域也可以拖：不是 Button / TextBox / Slider / ComboBox / ListBox 条目 / ScrollBar 的地方；
  - 锁定时不能拖；松手后照常吸附；Compact 状态下的点击 / 拖动规则不变。
