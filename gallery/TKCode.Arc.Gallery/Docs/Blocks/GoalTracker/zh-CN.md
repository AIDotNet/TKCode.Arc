---
title: Goal tracker
summary: 一个周期内的团队目标和关键结果：标出到今天应达到位置的进度条、负责人、信心、带数值和备注的原地进度更新，以及对照目标直线的时间轴。
source-hash: 5e55b5cb34c0
---

<Demo name="Preview" />

## 何时使用

`GoalTracker` 跟踪一个团队在某个周期（例如一个季度）里的目标及其关键结果。每个关键结果都有起始值、目标值和当前值，所以进度条显示它走了多远，条上的标记显示如果进展均匀，到今天应该在哪里。负责人的信心（正常、有风险、偏离）决定进度条的颜色。进度更新在原地记录新值、信心和备注；时间轴视图把所有更新画在通向目标的直线旁边。

- **每周更新进度。** 负责人不用离开页面就能更新关键结果。
- **管理层回顾。** 按信心统计的数量和总体进度条概括整个周期。
- **及早发现偏差。** 进度条落后于今天的标记，或时间轴低于直线，都需要关注。

## 何时不用

- **任务列表。** 工作项请使用 `Kanban board` 或 `Project board`。
- **是或否的里程碑。** 关键结果需要一个数字；清单请使用 `Release readiness`。
- **整个公司的目标树。** 本区块只显示一层目标及其关键结果。

## 用法

本区块派生自 `TemplatedControl`。没有目标时，它显示本季度的虚构目标。提供 `Goals`、`PeriodStart` 和 `PeriodEnd`；负责人是 `ProjectPerson`。

```xml
<arc:GoalTracker Title="Support goals" View="Timeline" CurrentUser="Mia" />
```

```csharp
var reply = new GoalTrackerKeyResult("First reply under 2 hours", 35, 90, 62, "%") { Owner = mia };
tracker.Goals.Add(new GoalTrackerGoal("Answer every customer faster", "Support", mia) { KeyResults = [reply] });
tracker.CheckedIn += (_, e) => okrs.Save(e.KeyResult, e.CheckIn);
```

### 进度更新

"Check in" 在关键结果下方打开一个编辑区：数值、信心和备注。保存会添加一条日期为 `Today`、署名为 `CurrentUser` 的 `GoalTrackerCheckIn`，更新 `Current` 和 `Confidence`，并引发 `CheckedIn`。`CheckIn` 在代码中做同样的事；`OpenCheckIn` 打开编辑区。按 Escape 关闭它。

### 团队与视图

`TeamFilter`（双向）把目标缩小到一个团队；团队选择列出目标中出现的团队。`View` 在列表和时间轴之间切换。文字都在 `Labels`（`GoalTrackerLabels`）中。

<Demo name="Timeline" />

## API 参考

<!-- api: GoalTracker -->

<!-- api: GoalTrackerGoal -->

<!-- api: GoalTrackerKeyResult -->

<!-- api: GoalTrackerCheckIn -->

<!-- api: GoalTrackerConfidence -->

<!-- api: GoalTrackerView -->

## 无障碍

- 目标标题是三级标题；区块标题是二级标题。
- 每个进度条都是 0 到 100 的只读进度条，以关键结果及其数值命名。
- 进度更新编辑区会把焦点移到数值输入框；Escape 关闭它；结果会被播报。
- 视图切换和团队选择分别名为 "View" 和 "Team"。

## 动效

- 进度更新后，进度条以弹簧移到新值（Smooth，无过冲）。
- 减少动效和关闭动效时，新值立即到位。

## 响应式

- 在 `CompactWidth`（640 px）或更窄时，关键结果标题会换行，进度更新编辑区的输入框上下排列；内边距收紧到 16。
- 时间轴的标签栏最多占宽度的 40%。

## 相关组件

- `Progress` 和 `SegmentedControl`。
- `Release readiness` 和 `Gantt timeline`，其他项目类区块。
