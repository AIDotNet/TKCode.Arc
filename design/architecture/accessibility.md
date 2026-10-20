# 可访问性

> 焦点可见性、AutomationPeer 与属性、键盘模型、播报、减少动效与减少透明度的统一规则。Arc 原版的 ARIA 约定在这里翻译为 Avalonia 的对应概念。

## 1. 焦点可见性（S7）

Arc 的规则：**指针焦点不画焦点环；键盘焦点显示统一的焦点环。**

| 元素 | 焦点表现 |
|---|---|
| 按钮、开关、复选框、选项卡等 | 键盘焦点时显示 2px 轮廓（`FocusRing`、`FocusRingWidth`），偏移 2px（`FocusRingOffset`）；位于裁剪容器内的组件用 −2px |
| 文本框 | 不画焦点环，用边框颜色与光标表示焦点（文本框在鼠标点击时也会匹配 focus-visible） |
| 菜单项、列表项 | 不画焦点环，用与悬停相同的高亮底色表示键盘位置 |
| 只由代码聚焦的容器（对话框面板、区域） | 不画焦点环 |

实现：

- Avalonia 12 提供 `:focus-visible` 伪类。Core 提供一个 `FocusRing` 装饰 presenter：它在宿主进入 `:focus-visible` 时显示，圆角跟随宿主（同心圆角：宿主圆角 + 偏移）。
- 各控件模板放置 `FocusRing`，并写入样式 `^:focus-visible /template/ arc|FocusRing`。
- 不允许手写轮廓、阴影光晕等替代焦点样式（Arc 规则）。

## 2. AutomationPeer 与属性

| Arc（ARIA） | Avalonia |
|---|---|
| 原生 `<button>`、`role="switch"` 等 | 选择正确的基类（Button、ToggleButton），或重写 `OnCreateAutomationPeer` 返回相应的 Peer。Avalonia 没有 Switch 角色，开关返回 Toggle 模式的 Peer，屏幕阅读器会按切换按钮朗读；可以在 Name 或 HelpText 中补充"开关"语义 |
| `aria-label` | `AutomationProperties.Name` |
| `aria-describedby` | `AutomationProperties.HelpText`（Description / Error 文本） |
| `aria-labelledby` / `<label for>` | `AutomationProperties.LabeledBy` |
| `aria-invalid` + `role="alert"` | `:error` 伪类 + `AutomationProperties.LiveSetting = Assertive` |
| `aria-live="polite"`（计数、搜索结果数） | `AutomationProperties.LiveSetting = Polite` |
| `aria-busy` + `aria-disabled`（加载中保持焦点） | 自定义属性 `IsLoading`：保持可聚焦但吞掉点击；Peer 报告 `IsEnabled=false` 的同时不移除焦点 |
| `aria-hidden`（装饰图标、动画中的逐字元素） | `AutomationProperties.AccessibilityView = Raw`（或不进入 Peer 树） |
| `aria-activedescendant` | Avalonia 没有等价机制。焦点留在输入框的列表弹层，在活动项变化时通过隐藏的 `LiveSetting = Polite` 文本播报当前项（见 [浮层与输入](overlays-and-input.md#23-关闭与焦点)） |
| `aria-pressed`（图例开关） | Toggle 模式 Peer |

图标按钮必须提供具体的名称，例如 "Delete invoice" 而不是 "Delete"（Arc 规则）；组件规格中把名称列为必填属性或给出默认本地化文案。

## 3. 键盘模型

- 复合控件（Tabs、RadioGroup、SegmentedControl、菜单、列表、日历）只有一个 Tab 停靠点，内部用方向键移动，由 S9 提供（见 [非动效基元](primitives.md#3-列表键盘导航s9p1)）。
- 浮层打开时焦点移入，必要时设置焦点陷阱，Escape 关闭，关闭后焦点回到触发器（见 [浮层与输入](overlays-and-input.md)）。
- 键盘快捷键不抢占文本框中的按键。
- 每个组件规格都包含"键盘交互"表，内容来自 Arc 组件文档的 Keyboard interactions 一节。

## 4. 播报

- 异步结果（复制成功、保存成功、搜索结果数量）通过 `LiveSetting` 播报。
- 动画中的文字（逐字、逐词、滚动数字）对辅助技术隐藏，另提供完整的纯文本供读取，避免重复朗读。
- 图表：绘图对辅助技术隐藏，另提供摘要与数据表的可访问表示（见 [图表基础](../components/charts/chart-foundation.md)）。

## 5. 对比度

- 文字 ≥ 4.5:1；24px 以上或 18.5px medium 字重 ≥ 3:1；图标与控件边框 ≥ 3:1。
- 由调色板测试自动检查（见 [Token 验证](theming/verification.md)）。

## 6. 减少动效与减少透明度

- 见 [减少动效](motion/reduced-motion.md)。每个动画都有 Reduced 与 Off 分支；状态反馈在 Reduced 下保留。
- `ReduceTransparency` 关闭半透明表面与背景模糊。

## 7. 验证

| 检查 | 方式 |
|---|---|
| Peer 类型与名称 | 控件测试中断言 `ControlAutomationPeer` 的类型、Name、HelpText |
| 焦点环只在键盘焦点出现 | Headless 测试：鼠标点击后不显示，Tab 聚焦后显示 |
| 键盘交互 | 按组件规格的键盘表逐项测试 |
| 屏幕阅读器冒烟 | macOS VoiceOver、Windows Narrator 手工验收（每个阶段结束时抽查） |
