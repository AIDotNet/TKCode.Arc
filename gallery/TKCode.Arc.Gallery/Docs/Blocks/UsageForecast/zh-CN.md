---
title: Usage forecast
summary: 对照预算查看一个计费周期：已测得的用量和带可能范围的虚线预测，一条可拖动或用按键步进的预算线，标题、磁贴、红色超额部分和提醒标签都会随之变化，还有一个让所有路径变形的指标开关。
source-hash: 64c726ac820f
---

<Demo name="Preview" />

## 何时使用

`UsageForecast` 回答"本周期我们会不会控制在预算之内，什么时候会超出？"。它从左到右绘制一个计费周期的每一天：截至今天已测得的内容是实线折线，预测是虚线折线，预测周围有一个越往后看越宽的预测锥。一条水平线是预算。预测与它相交的位置，会有一个标记指出日期，而预算线之上的每一段路径都会变红。

- **账单和套餐页面。** 在一个视图中呈现用量、预测和预计超额，账户所有者可以移动预算，查看不同的上限意味着什么。
- **提醒和上限。** 50%、80% 和 100% 标签说明各个阈值已在何时达到或将在何时达到，并让使用者选择要接收哪些通知。
- **多个计量，一个故事。** 调用次数、存储和席位共用同一个区块：开关会把同一张图表变形为另一个计量，带有它自己的坐标轴、预算范围、单位和价格。

## 何时不用

- **没有预测的花费。** 只想查看单个数值的历史时，请使用折线图；本区块的价值在于预测和预算。
- **同时查看许多指标。** 本区块一次只显示一个计量。需要比较多个计量时，请使用 `Metrics dashboard` 或 `Small multiples`。
- **长达数百天的周期。** 每天绘制一个顶点，日期标签相隔一周；本区块为大约一个月的周期而设计。

## 用法

为区块提供周期（`PeriodStart`、`PeriodDays`），并为每个计量提供一个 `UsageMetric`。一个指标包含第 1 天到今天的 `Readings`，一个 `StartValue`（累计总量用 0；对于像正在使用的存储这类水平值则省略），固定的数值坐标轴，带范围和步长的预算，单位，以及超出预算部分每个单位的价格。除非 `TodayIndex` 另有指定，今天就是读数的个数。当你没有提供预测及其范围时，会根据最近两周计算得出。

```xml
<arc:UsageForecast Title="Usage outlook" Subtitle="Harborline Studio · Growth plan · Renews Jul 1" PeriodDays="30">
    <arc:UsageMetric Key="events" Label="Events" Readings="{Binding EventReadings}" StartValue="0"
                     AxisMaximum="40" BudgetMinimum="8" BudgetMaximum="40" BudgetStep="0.5" Budget="26"
                     Unit="M" UnitPrice="150" PriceNote="At $0.15 per 1k events" />
</arc:UsageForecast>
```

```csharp
forecast.PeriodStart = new DateOnly(2027, 6, 1);
forecast.Metrics.AddRange(metrics);
forecast.SelectedMetricKey = "database";
```

`MetricsSource` 接受由你自己持有的集合（会跟随 `ObservableCollection` 的变化）。只有一个指标时开关会隐藏，`UnitPrice` 为 0 时成本磁贴会隐藏。`Budget` 在每个指标上都是双向的，切换离开再切换回来时会保留其值。

### 结构

- **页眉**：`Title`、`Subtitle`，以及右侧的指标开关。
- **标题句**：一句话有四种形式（已在某天超出预算、预测在某天达到预算、在最后一天达到预算、保持在预算之下），其下一行显示剩余时间和可能范围。拖动时它们会被改写，只有变化的词会播放动画。
- **磁贴**：到目前为止的用量及其占预算的比例，最后一天的预测及其范围，以及超额成本（大于零时为红色，下方带有价格说明）。
- **图表**：固定坐标轴上的六个数值刻度，相隔一周的日期，一条 "Today" 线，预测锥，预测线，已测得的线，提醒刻度，带日期胶囊的交点标记，以及预算带。
- **页脚**：提醒标签和图例。

### 预算线

线周围整个 32px 的带状区域都响应指针。拖动是相对的（在偏离中心的位置按下不会让线跳动），数值会吸附到 `BudgetStep`，超出预算范围后线仍会跟随，但会被压缩，松开后弹回。每一步都会重新计算标题句、磁贴、标签和交点；`BudgetChanged` 在每一步都会触发，此时 `IsCommitted` 为 false，并在松开或按键之后再触发一次，此时 `IsCommitted` 为 true，这就是保存的时机：

```csharp
forecast.BudgetChanged += (_, e) => { if (e.IsCommitted) Save(e.Metric.Key, e.NewValue); };
```

`BudgetCommittedCommand` 通过绑定完成同样的事，新预算作为其参数。`SetBudget` 用同样的弹簧从代码中移动这条线。

<Demo name="Events" />

### 提醒

`AlertThresholds` 保存这些标签；当你没有添加任何阈值时，区块使用 50%（关闭）、80% 和 100%（开启）。阈值的 `IsEnabled` 是双向的，它只读的 `Status` 为 `AlreadyPast`、`Reached`、`Expected` 或 `NotExpected`，`Date` 是对应的日期。已启用的阈值会在线到达预算该比例的位置绘制一个刻度，旁边带有它的百分比。`AlertThresholdToggled` 报告每次切换；区块本身从不发送任何内容。

### 结果

<Demo name="StaysUnder" />

<Demo name="AlreadyPassed" />

只读属性把同样的事实提供给页面的其余部分：`Outcome`、`CrossingDay`、`CrossingEarly`、`CrossingLate`、`ProjectedTotal`、`ProjectedLow`、`ProjectedHigh` 和 `OverageCost`。

### 查看某一天

指向图表，或让它获得焦点并使用按键，即可得到十字准线和最近一天的卡片：它的日期、是测得的还是预测的、数值，以及占预算的比例或可能范围。卡片位于十字准线右侧，靠近边缘时翻转到左侧。`InspectedDay` 是双向的（null 表示没有）。

### 窄容器

区块读取自己的宽度。小于 560 时内边距收紧，开关移到标题下方，图表高 232，磁贴收紧；小于 440 时磁贴改为堆叠，数值在右侧，每个标签占满一行。标题句的字号随宽度在 18 到 22 之间变化。

<Demo name="Flat" />

### 文字、格式和日期

`IsFramed` 为 false 时去掉卡片边框。`DateFormat` 决定每个日期的写法。指标的 `ValueFormatter`、`Unit`、`UnitSeparator` 和 `CurrencyFormatter` 控制数值的显示方式；磁贴会根据 `ValueFormatter` 给出的小数位滚动数字，因此它应返回纯数字。每个句子、标签和朗读文字都是 `Labels` 的一个属性。

<Demo name="Words" />

## API 参考

<!-- api: UsageForecast -->

<!-- api: UsageMetric -->

<!-- api: UsageAlertThreshold -->

<!-- api: UsageForecastLabels -->

<!-- api: UsageBudgetChangedEventArgs -->

<!-- api: UsageOutcome -->

<!-- api: UsageAlertStatus -->

## 无障碍

- 图表是覆盖各天的滑块：它的名称是一个完整的句子（指标、到目前为止的用量、预测、预算和标题句），它的值读作 "Jun 21: 22.9M used, 88% of budget" 或 "Jun 27 projected: 29.4M, likely 28.2M to 31M"，它的帮助文字说明如何使用按键。
- 预算带是覆盖预算的滑块，数值以文字形式提供，提示为 "Drag the budget line or press the arrow keys."。
- 当预算确定、选择了指标或切换了提醒时，标题句及其下一行会通过礼貌的实时区域朗读；动画中的词本身对屏幕阅读器隐藏。
- 提醒标签是切换按钮，以其百分比和状态命名（"80% of budget, hit jun 19"）。
- 一个隐藏的表格列出每一天的读数和范围，预测的日期有标记。
- 超额部分会变红，同时还会写出 "Overage" 和金额；路径中变红的部分由标题句重复说明。

### 键盘

- **Tab** 依次访问指标开关（方向键更改选择）、图表、预算线和每个标签。
- 图表：**← →**（或 **↓ ↑**）移动一天，**Page Up / Page Down** 移动一周，**Home / End** 到第一天和最后一天，**Escape** 隐藏卡片。让图表获得焦点会显示今天。
- 预算线：**↑ →** 上调一步，**↓ ←** 下调一步，**Page Up / Page Down** 五步，**Home / End** 到最低和最高预算。
- 标签：**Enter** 或 **Space** 切换。

## 动效

- **预算线**：按键之后沿用灵敏弹簧移动，拖动时用较硬的弹簧跟随；红色裁剪区域随之移动。超出范围时线会被压缩并弹回。
- **交点标记、提醒刻度**：它们沿折线追随所显示的预算与所显示的线的交点；日期胶囊淡入淡出，经过时附近的日期标签淡出。
- **提醒刻度**自下而上绘制（700 ms），并以同样的方式擦除；它们的标签淡入淡出。
- **指标开关**：已测得的线、预测线和预测锥的每个顶点同时弹向新指标，预算线和刻度随之移动，数值标签交叉淡入淡出，磁贴数字滚动，标题句只替换变化的词（退出 160 ms，进入 240 ms，间隔 40 ms），标签状态逐字母变形。
- **标签**：勾选标记在快速弹簧上打开它的槽位，宽度随之变化。
- **查看卡片**：以较硬的弹簧跟随十字准线，淡入 160 ms。
- 减弱动效会让每个弹簧立即到位，只保留短暂的淡入淡出（词为 120 ms）。关闭则立即应用所有变化。

## 相关

- `Metrics dashboard`、`KPI drilldown`、`Cohort retention`，即其他 Analytics 区块。
- `SegmentedControl`，本区块使用的开关。
