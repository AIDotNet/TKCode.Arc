---
title: AI insight card
summary: 一张仪表盘卡片，由助手说明某个指标为什么变化：变化幅度、标出相关时段的图表、可以选中的可能原因，以及一条建议的下一步。
source-hash: d3a4c23533f3
---

<Demo name="Preview" />

## 何时使用

`AiInsightCard` 放在仪表盘上，紧挨着它所说明的数字。当某个指标突然上升或下降时，人们会问：什么变了、为什么、现在该做什么。卡片给出指标的当前值和变化，给出说明所指的图表并用色带标出变化的时段，附上一段简短的文字总结，按各自能解释多少变化列出可能原因，最后给出一条建议的下一步和一个按钮。选中某个原因时，色带会移到这个原因对应的日期，文字和图表始终指向同一件事。

- **仪表盘上的异常。** 活跃用户下降、成本飙升、转化率下滑：卡片把告警变成可以据此行动的说明。
- **每周回顾。** 每个发生变化的指标一张卡片，由分析任务写好，在会上阅读。
- **有人把关的监控。** 建议的按钮会启动修复（回滚、建工单），由人来决定，由系统去执行。

## 何时不用

- **单纯的数字。** 没有需要说明的内容时，使用 `MetricCard` 或单独的图表。
- **长篇报告。** 卡片只容得下两三句话和几个原因。更多内容请链接到完整报告。
- **围绕数据的实时对话。** 需要提问和追问时，使用 `AI chat` 或 `AI side panel` 区块。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不设置任何数据时显示一份虚构的示例（`AiInsightCardSamples`）；设置属性即可显示你自己的数据。

```xml
<arc:AiInsightCard Title="Weekly active users"
                   Value="18,420"
                   Change="-12.4"
                   PeriodText="vs. the 14 days before"
                   Summary="Active users fell 12.4% over the last five days, almost all of it on Android."
                   NextStep="Roll Android back to 4.1.3 for magic-link sign-ins." />
```

```csharp
card.Points = values;               // oldest first
card.PointLabels = days;            // "Nov 4", one per point
card.ComparisonPoints = lastPeriod; // dashed line, or null
card.FocusRange = new AiInsightRange(9, 13);
card.Causes =
[
    new() { Title = "Android 4.2 release", Detail = "Sign-ins on Android fell 31%.", Share = 0.64, Range = new(9, 13) },
    new() { Title = "Regional holiday", Share = 0.09, Range = new(9, 10) },
];
card.ActionInvoked += (_, e) => rollouts.Rollback("android", e.NextStep);
```

### 结构

- **小标签与点赞**：一个小小的 "AI insight" 标签，以及赞和踩（`Feedback`，双向；再按一次同一个按钮会撤回回答）。
- **指标**：`Title`、`Value`、根据 `Change` 生成的变化标签和 `PeriodText`。`IsIncreaseGood` 决定色调：默认上升为绿色；用于成本和错误率时设为 false。
- **读数与图表**：`Points` 画成的折线和下方的浅色面积、虚线表示的 `ComparisonPoints`，以及覆盖 `FocusRange`（或选中原因的 `Range`）的色带，色带内的折线使用变化的颜色。上方的读数显示所选的点，没有选中时显示最后一个点。
- **总结、原因、下一步**：`Summary`；`Causes`，每个原因带占比条和百分比；`NextStep` 和按钮（`IsActionVisible`、`ActionCommand`、`ActionInvoked`）。
- **脚注**：`Footnote`，说明数据来源和说明写成的时间。

### 另一个指标

<Demo name="CostRise" />

### 正在生成时

在任务撰写说明期间设置 `IsAnalyzing`：文字会换成一行闪光的提示和若干占位行，图表保持不变。它再次变为 false 时，会朗读总结。

<Demo name="Analyzing" />

### 文字

所有文字都在 `Labels`（`AiInsightCardLabels`）中：各个标题、按钮、生成中的提示、读屏名称，以及变化、占比和读数的格式。`ValueFormatter` 用于格式化读数里的数值。

## API 参考

<!-- api: AiInsightCard -->

<!-- api: AiInsightCause -->

<!-- api: AiInsightRange -->

<!-- api: AiInsightCardLabels -->

## 无障碍

- 卡片是一个有名称的区域；指标名称是二级标题，各部分的标题是三级标题。
- 图表是一个 Tab 停靠点，作为以指标命名的图像读出，帮助文本是第一个和最后一个值。**Left** 和 **Right** 选择数据点，**Home** 和 **End** 跳到两端，**Escape** 回到最后一个点；用键盘选中的每个点都会读出（"Nov 4: 17,980, before 20,110"）。用指针悬停时不会朗读。
- 每个原因都是一个切换按钮，以标题和占比命名（"Android 4.2 release, 64% of the change"），并以证据作为说明。**Up** 和 **Down** 在原因之间移动，**Space** 或 **Enter** 选中。
- 赞和踩分别命名为 "Helpful" 和 "Not helpful"，并报告 "selected"；回答后会礼貌地确认。
- 生成中的提示和完成后的总结都会礼貌地朗读。

## 动效

- 卡片第一次出现时，折线从左向右画出（480 ms，标准缓动）。
- 选中原因时，色带以 snappy 弹簧滑到对应时段，色带内的颜色随之移动。
- 行和点赞按钮的颜色在 160 ms 内过渡；变化标签的色调交叉淡化。
- 减弱动效时整条折线立即显示，色带不经弹簧直接移动。关闭动效时所有变化立即生效。

## 响应式

- 卡片最宽 640，在更窄的容器中占满宽度。宽度不超过 420 时（`:compact`）内边距从 20 收紧到 16。
- 数值、变化标签和对比时段放不下时换到第二行。
- 图表保持高度并按新宽度重绘；占比条保持尺寸，标题以省略号截断。
- 没有任何功能依赖悬停：轻点原因即可选中，轻点图表即可选择数据点。

## 相关组件

- `MetricCard` 和 `LineChart`，用于不需要说明的数字和图表。
- `AI answer`，用于对一个问题给出更长、带引用的回答。
- `Metrics dashboard` 和 `KPI drilldown`，这张卡片所在的分析区块。
