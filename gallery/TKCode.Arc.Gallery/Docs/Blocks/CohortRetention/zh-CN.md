---
title: Cohort retention
summary: 每个起始群体中有多少人留了下来，逐月呈现：热力图三角形可变形为留存曲线，头条数字对比最新与最早的队列，分段会重绘每个单元格，并有同时适用于指针和键盘的单元格光标。
source-hash: 97b2545d3dc0
---

<Demo name="Preview" />

## 何时使用

`CohortRetention` 回答一个问题：在某个周期开始的人（账户、工作区、安装）中，一个月、两个月、三个月后还有多少仍然活跃，并且情况是否在变好？每一行是一个队列，从最早的开始，每一列是自队列开始后的一个月。较新的队列被测量的月数较少，因此热力图是一个三角形；其下按规模加权的平均行显示一个典型队列的样子。

- **增长和客户成功仪表板。** 顶部的第 1 个月和第 6 个月留存，以及最新三个与最早三个队列之间的变化，一眼就能看出新手引导工作是否见效。
- **比较分段。** 把套餐、渠道或地区作为分段：切换时三角形以波浪形重绘，每个数字滚动，因此两组之间的差异在变化的过程中就能看到。
- **形状与水平兼顾。** 热力图显示每个单元格的水平；曲线显示各队列在哪里趋于平缓，以及哪些队列比平均值下降得更快。

## 何时不用

- **单个留存数字。** 没有可比较的队列时，请使用统计数字或折线图。
- **漏斗和转化步骤。** 人们按顺序经过的步骤属于漏斗或 `Journey flow` 区块，而不是自开始以来的月数。
- **数百个队列。** 该区块为大约 6 到 20 个队列及同样多的周期而设计；在显示更多之前请进一步汇总（用月代替周）。
- **需要精确查询的每日数据。** 有几十列的留存表在 `DataGrid` 中读起来更好。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。它的数据是一组 `RetentionSegment`，每个分段包含其 `RetentionCohort`，从最早开始。一个队列有 `Label`（"Jun 2026"）、供窄布局使用的可选 `ShortLabel`（"Jun ’26"）、`Size`（有多少人开始）以及 `Retained`，即第 1、2、3 个月……仍然活跃的人数。`Retained` 中值的个数就是目前已测量的月数。每个留存率、平均行和头条数字都由区块计算。

```xml
<arc:CohortRetention Title="Space retention"
                     Subtitle="Team spaces still editing each month after creation."
                     FirstMetricLabel="First-month retention"
                     SecondMetricLabel="Half-year retention"
                     TotalLabel="Spaces created">
    <arc:RetentionSegment Label="All" Description="all spaces">
        <arc:RetentionCohort Label="Jun 2026" ShortLabel="Jun ’26" Size="412" Retained="{Binding June}" />
        <arc:RetentionCohort Label="Jul 2026" ShortLabel="Jul ’26" Size="398" Retained="{Binding July}" />
    </arc:RetentionSegment>
</arc:CohortRetention>
```

```csharp
retention.Segments.Add(new RetentionSegment("Starter", starterCohorts, "Starter-plan spaces") { Key = "starter" });
retention.SelectSegment("starter");
```

`SegmentsSource` 则接受由你持有的集合（`ObservableCollection` 会被跟踪）。只有一个分段时，分段开关会被隐藏。修改所显示分段中的某个队列（例如它的 `Retained`）会就地更新区块。

### 结构

- **标题区**：`Title`、`Subtitle`，右侧是分段开关。
- **头条数字**：`FirstMetricMonth`（1）和 `SecondMetricMonth`（6）按规模加权的平均留存，各自带着已达到该月份的最新与最早 `CompareCohortCount`（3）个队列之间以百分点计的变化；以及开始的总数，附第一个和最后一个队列的名称。数值逐位滚动。
- **工具栏**：图例（六个热力等级及其阈值，或两种线条样式）和视图开关。
- **图表**：左侧是队列名称和规模，然后是单元格、平均行和月份坐标轴。

### 热力图和曲线

`View` 是 `Heatmap`（默认）或 `Curves`，双向。在热力图中，每个单元格显示四舍五入后的百分比，以及混入表面色的强调色等级：从最低到最高的等级依次是 8%、18%、32%、48%、66% 和 86%，各等级的起点是留存 20%、35%、50%、65% 和 80%。等级使用精确的留存率，因此 79.8% 读作 "80"，但仍属于第五级。用 `Thresholds` 更改阈值，用 `RampMix` 更改混合比例（比阈值多一个值）。混合比例达到 50% 或更高的等级用强调色的前景色绘制数字。

在曲线视图中，y 轴从 0 到 100%；每个队列是一条细的直线段折线，平均值是叠在最上面的粗强调色线，每个月一个点。

<Demo name="Curves" />

### 查看单元格

指向一个单元格（或曲线视图中的一个点）即可查看它：一个方框标出该单元格，图表中其所在行和列之外的部分淡出，该行的名称和月份编号变为加重，单元格旁边的一张卡片给出精确的留存率、开始的人数中仍然活跃的人数，以及与该月平均值的差异。在平均行上，卡片会说明平均值涵盖了多少个队列。在曲线视图中，最近的月份由 x 确定，该月最近的点由 y 确定，一条十字线延伸到 y 轴并在标签中给出精确值，该队列的线条变深，其他线条淡出。

`CursorCell` 保存被查看的 `CohortCell`（双向，无则为 null），`CursorChanged` 在每次变化时报告队列、留存率和人数。点击，或按 Enter 或 Space，会引发 `CellInvoked` 并运行 `CellCommand`：用它来打开该队列的人员或详情页。

<Demo name="Events" />

### 周、其他周期、其他阈值

区块中没有任何东西与月绑定：列数跟随最长的队列（或 `Months`），每一个提到周期的文本都是属性（`MonthFormat`、`MetricLabelFormat`、`AxisTitle`、`AxisTitleShort`、播报格式）。

```csharp
retention.Thresholds = [0.10, 0.25, 0.40, 0.55, 0.70];
retention.MonthFormat = "Week {0}";
retention.MetricLabelFormat = "Week {0} retention";
```

<Demo name="Weekly" />

### 窄容器

该区块读取自身的宽度，而不是窗口的宽度。小于 600 时图表切换为紧凑几何：队列名称缩短，没有规模列，行高 24px，坐标轴标题为 "Month"；当某一列窄于 32 时，数字会离开单元格。小于 560 时内边距收紧到 16，分段开关移到标题下方。小于 440 时头条数字上下堆叠，每行一个且数值靠右，视图开关移到图例上方。头条数值随区块宽度在 22 到 28 之间缩放。

<Demo name="Compact" />

### 格式和文字

`PercentFormatter`（0 到 1 的留存率）、`CountFormatter` 和 `DeltaFormatter`（百分点）替换默认的 "48.5%"、"5,199" 和 "+5.2 pts"。每一个可见的文字都是带有英文默认值的属性：头条标签、图例文本、卡片的行标签（`StillActiveText`、`VsAverageText`、`WeightedOverText`）、开关按钮及其名称、空文本和播报。`ShowSizeColumn` 和 `ShowAverageRow` 会隐藏规模列和平均行（以及平均线）。

## API 参考

<!-- api: CohortRetention -->

<!-- api: RetentionSegment -->

<!-- api: RetentionCohort -->

<!-- api: CohortCell -->

<!-- api: CohortCellEventArgs -->

<!-- api: CohortRetentionView -->

## 无障碍

- 图表是一个可聚焦的分组，其名称是一段摘要（视图、分段、队列的范围和两个头条数值），并以按键提示作为帮助文本。键盘焦点环绘制在它的周围。
- 每次光标移动都会以礼貌方式播报："Jun 2026, month 3: 56.2% still active, 236 of 420."。平均行会说明它涵盖了多少个队列。Escape 会清空播报。
- 视图变化会播报视图所显示的内容；分段变化会播报分段和两个头条数值。
- 一张隐藏的表格列出每个队列及其规模和每个月的留存率（或 "not measured yet"），平均行在最后，因此屏幕阅读器无需图表就能读出这些数字。
- 每个头条数字作为一个短语朗读，并用文字说出其变化（"up 5.2 pts newest 3 vs oldest 3 cohorts"），而不仅靠颜色。图例以其阈值命名。
- 颜色在两种主题下都来自强调色和表面色；在深色等级上，数字切换为强调色的前景色。

### 键盘

- **Tab** 依次移到分段开关、视图开关和图表，各占一个停靠点。Tab 进入图表时，光标落在最早队列的第一个月。
- **← →** 沿队列移动光标，并停在其第一个和最后一个已测量的月份。
- **↑ ↓** 在队列之间移动并保持月份，若该队列尚未到达该月，则取该队列最后一个已测量的月份。从最新队列按 **↓** 会到达平均行。
- **Home / End** 跳到队列的第一个和最后一个已测量的月份。
- **Escape** 清除光标；焦点保留在图表上。
- **Enter / Space** 为光标所在的单元格引发 `CellInvoked`。
- 在两个开关中，**← →** 立即更改选择并循环；**Home / End** 跳到两端。

## 动效

- **热力图到曲线**：一条主弹簧驱动变形；每个部分取其中一段，沿对角线从左上角开始依次更晚。数字淡出，单元格缩成点，点从它们的行移动到它们的留存率位置并拉开线条，行标签淡出，然后线条和 y 网格淡入，全部在约一秒内完成。返回时以相反顺序播放相同的片段：最后的单元格最先移动，每个单元格先移动再增大。
- **分段开关**：每个值都以弹簧变化到新的留存率，沿对角线每一步晚 9 ms 开始；每当一个值越过阈值，单元格就在 160 ms 内重绘，因此爬升三个等级的单元格会重绘三次，其数字在中途变为浅色或深色。头条数字滚动；变化文字向上滑出再滑入。
- **光标**：方框、环和十字线跳到新的单元格；其他单元格在 160 ms 内淡出；卡片在出现的位置淡入（120 ms），然后在一条快速弹簧上滑向每个新单元格，并在 160 ms 内淡出。
- **图例**：旧图例上升 6px 并淡出，新图例上升到位，240 ms。
- 减少动效会让变形、数值和颜色立即到位，只保留短暂的淡入淡出（卡片、图例、变化文字）。关闭会立即应用一切。

## 相关组件

- `Metrics dashboard`、`KPI drilldown` 和 `Journey flow`，即其他 Analytics 区块。
- `SegmentedControl`，该区块所用的开关。
- `Activity terrain`，另一个在两种视图之间变形的热力图。
