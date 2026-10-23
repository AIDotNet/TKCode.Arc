---
title: 活动地形
summary: 一周每小时的活动呈现为三维地形：可以旋转和缩放，悬停某个小时查看其数值，飞到某根柱子，把它压平成热力图，并在旁边读取总量、高峰时段和最繁忙的一天。
source-hash: d106cd4feb20
---

<Demo name="Preview" />

## 何时使用

`ActivityTerrain` 把一周的逐小时数据呈现为立在底面上的 168 根柱子：七行代表周一到周日，二十四列代表小时，从周一 00:00 到周日 23:59。柱子越高、颜色越深，表示那个小时越繁忙，因此一周的节奏一眼即可看出：早晨的上升、午饭时的低谷、安静的周末。悬停一根柱子会将它抬起，并显示它的数值、一周前同一小时的数值以及当天的走势；点击它则把相机飞到那里。图表旁边的摘要给出一周的总量及其变化、最繁忙的时段和最繁忙的一天。

- **产品分析。** 每小时的访问、注册或搜索次数，用来看出用户何时真正使用产品，以及发布或维护窗口放在哪里伤害最小。
- **工程活动。** 每小时的构建、部署或合并的变更，用来展示团队何时交付，以及周五下午是否依然安静。
- **运维与值班。** 每小时的告警或事件，使用危险色调：一个糟糕的下午会表现为一根醒目的红色尖峰，相比上周的上升也会明确地显示为坏消息。
- **支持与销售台。** 每小时的聊天、工单或来电，围绕摘要列出的高峰安排人手。
- **仪表板首页上的展示区**，其中的动效（入场、切换指标时的波浪、飞入）会邀请人们探索一周的数据，而他们原本可能只会在表格里一扫而过。

## 何时不用

- **不是七天乘二十四小时的数据。** 形状是固定的：一年的日数据请使用 `ActivityHeatmap`，其他周期请使用折线图或柱状图。
- **需要精确比较，而不只是整体形状时。** 透视会让远处的柱子显得更矮；热力图视图有所帮助，但表格或柱状图比较数值更精确。
- **页面上有许多图表时。** 地形很大且带有动画，每页一个就足够。小型多图请使用迷你图或 `MetricCard`。
- **不是按小时计的计数或数量的数据**（比率、可能为负的平均值）：柱子的高度从零开始，随数值增长。

## 用法

### 结构

从上到下依次是：可选的标题和副标题；工具栏，包含指标切换器（有两个或更多指标时显示）和视图切换器（地形或热力图）；绘图区，包括提示卡，以及在相机移动后出现的“重置视图”按钮；从少到多的图例，附带手势提示；以及摘要的三个部分：一周的总量及其变化、高峰时段和最繁忙的一天。绘图区的高度为区块宽度的 44%，范围在 280 到 460 之间。

### 指标

每个 `ActivityTerrainMetric` 是一项度量：`Values` 中保存 168 个数值，从周一 00:00 开始，先按小时、再按天排列（`day × 24 + hour`）。`PreviousValues` 保存上一周，顺序相同；若省略它，变化和“上周”一行都会被隐藏。`Label` 为切换器上的指标命名，`Unit` 跟在卡片和摘要的数值之后，`Key` 则让你通过 `SelectedMetricKey` 选中它。

```csharp
var visits = new ActivityTerrainMetric
{
    Key = "visits",
    Label = "Visits",
    Unit = "visits",
    Values = thisWeek,            // 168 numbers: Monday 00:00, Monday 01:00, … Sunday 23:00
    PreviousValues = lastWeek     // optional
};
terrain.Metrics.Add(visits);
```

按 7 × 24 网格保存的数据可以用 `ActivityTerrainMetric.Flatten(grid)` 转换（行是从周一开始的天，列是小时）。缺失的条目按零计算。其余一切由地形自行计算：总量、相对上一周的变化、高峰时段（从大到小排列，并列时按天和小时排序）、每天的合计、最繁忙的一天，以及落在工作时间内的部分占一周的比例。

### 色调与升降方向

`Tone` 决定柱子、图例以及提示卡中当前小时的颜色：`Accent`（默认）、`Neutral`、`Success`、`Warning` 或 `Danger`。`HigherIsBetter` 决定上升显示为绿色还是红色；对于错误、事件或等待时间，请将其设为 false。下面是一个危险色调的指标，其中有一个糟糕的下午：

<Demo name="Incidents" />

```xml
<arc:ActivityTerrain PeakCount="5"
                     Title="Incident load"
                     Subtitle="Alerts that paged someone, per hour.">
    <arc:ActivityTerrainMetric Key="incidents" Label="Incidents" Unit="incidents" Tone="Danger" HigherIsBetter="False" />
</arc:ActivityTerrain>
```

### 地形与热力图

`ViewMode` 在地形（`Perspective`）与从上方俯视的平面热力图（`Top`）之间切换。在热力图中，柱子被压平成方块，每一天都有标签，拖动不再转动视图；悬停、点击和缩放依然可用。工具栏中的视图切换器设置的是同一个属性，而 `ViewModeChanged` 会报告这一变化。

<Demo name="HeatMap" />

### 悬停与飞入

悬停一根柱子，或用方向键移动到它，都会高亮它所在的小时：柱子抬起，一条带沿着它所在的行和列延伸到坐标轴标签，一周中的其余部分淡去，提示卡显示这一天、这个小时、数值、上一周的数值、变化，以及该小时占当天的比例，并配有当天 24 个小时的小柱子。`HighlightedCell` 保存这个小时，`CellHovered` 会报告它。

点击一根柱子会把相机飞到它那里（缩放至少为 1.5），并使它保持抬起；点击空白的地面或按下 Escape 则回到整周。`FocusedCell` 保存这个小时，`CellFocused` 会报告它，而 `FlyTo`、`ShowWeek` 和 `FlyToCommand` 可以在代码中做同样的事。摘要中的高峰时段按钮会飞到对应的小时，再飞回来。

```csharp
terrain.CellFocused += (_, e) =>
{
    if (e.Cell is { } cell)
    {
        ShowDetails(cell.Day, cell.Hour, e.Value);
    }
};
terrain.FlyTo(new ActivityTerrainCell(Day: 2, Hour: 14));   // Wednesday 14:00
```

### 旋转与缩放

拖动即可转动视图（偏航角不受限制，俯仰角保持在 10° 到 84° 之间，过度拖动后会弹回），松开后视图会继续滑行。按住 Ctrl 或 ⌘ 再使用滚轮，或做捏合手势，即可在 0.7 到 2.8 之间缩放；单纯的滚轮则滚动页面。`Yaw`、`Pitch` 和 `Zoom` 报告相机的状态，相机一旦移动，`IsViewDefault` 就变为 false；“重置视图”按钮（或 `ResetView`、`ResetViewCommand`）会使其回到默认位置，并引发 `ViewReset`。

### 窄屏布局

布局跟随区块自身的宽度。宽度低于 560 时，切换器竖向堆叠，绘图区高度为 280，视角再多转一些（50°），小时标签缩短为“09”，摘要分为两列。宽度达到 880 时，摘要移到绘图区旁边的一列中。

<Demo name="Narrow" />

### 你自己的数据与实时更新

每当有新数据到达时，替换 `Values`（以及 `PreviousValues`）即可：柱子会以切换指标时的同一波浪升到新高度，摘要中的数字也会滚动更新。星期的名称来自 `DayNames` 和 `DayAbbreviations`，`WorkingHours` 则定义哪些小时计入工作时间。

<Demo name="LiveData" />

```xml
<arc:ActivityTerrain Title="Support desk"
                     SelectedMetricKey="tickets">
    <arc:ActivityTerrain.WorkingHours>
        <arc:ActivityTerrainWorkingHours FirstDay="0" LastDay="4" FirstHour="8" LastHour="18" />
    </arc:ActivityTerrain.WorkingHours>
    <arc:ActivityTerrainMetric Key="chats" Label="Chats" Unit="chats" />
    <arc:ActivityTerrainMetric Key="tickets" Label="Tickets" Unit="tickets" Tone="Neutral" />
</arc:ActivityTerrain>
```

### 文案

每一段可见或会被朗读的文字都是一个属性，并带有英文默认值：图例（`LessText`、`MoreText`）、切换器（`PerspectiveText`、`TopViewText`）、`ResetViewText`、提示（`HintText`、`TouchHintText`、`KeyboardHintText`）、提示卡（`PreviousWeekText`、`ChangeText`、`ShareFormat`、`HourRangeFormat`）、摘要（`SummaryTitleFormat`、`ChangeSuffixText`、`PeakHoursTitle`、`BusiestDayTitle`、`BusiestDayFormat`），以及各种播报。格式字符串使用带编号的占位符；某个指标自己的 `SummaryTitle` 会替代该指标的 `SummaryTitleFormat`，而 `ValueFormat`（如 `"N1"` 这样的 .NET 数字格式）则改变其数值的显示方式。

## API 参考

<!-- api: ActivityTerrain -->

<!-- api: ActivityTerrainMetric -->

## 无障碍

- 绘图区是一个可获得焦点的元素。屏幕阅读器会朗读一句关于这一周的摘要（指标、总量、变化、最繁忙的小时和天），并把键盘帮助作为其描述。
- 这一周也以表格的形式提供给屏幕阅读器：每天一行，以星期命名，每个小时一个单元格（“14:00：1,000 visits”），并以指标作为标题。
- 礼貌式实时区域会播报高亮的小时及其数值和变化，以及飞入和返回整周、视图切换、指标切换和重置。
- 指标切换器和视图切换器是分段控件，各自只有一个 Tab 停靠点。摘要中的高峰时段行是切换按钮，名称包含日期、小时和数值；当相机对准其小时时，按钮处于按下状态。“重置视图”只在显示时才位于 Tab 顺序中。
- 键盘焦点在绘图区外侧显示 2px 宽的焦点环，外偏移 2px；指针聚焦时不显示。
- 颜色从来不是唯一的信息：数值显示在提示卡、表格和播报中，变化带有正负号和箭头。

### 键盘

| 按键 | 操作 |
|---|---|
| ← → ↑ ↓ | 第一次按下会高亮最繁忙的小时；之后按小时（← →）或按天（↑ ↓）移动，到达边缘即停止 |
| Home / End | 高亮那一天的第一个 / 最后一个小时 |
| Enter / Space | 飞到高亮的小时 |
| Escape | 回到整周；再按一次则清除高亮 |
| Shift + ← → | 将视图转动 15° |
| Shift + ↑ ↓ | 将视图俯仰 8°（范围 10° 到 84°） |
| + / − | 以 1.25 倍放大 / 缩小（范围 0.7 到 2.8） |

## 动效

- **入场。** 绘图区第一次出现在屏幕上时，相机从远处上方俯冲而下（约 3 秒），同时柱子从底面升起，先是后排和左侧的列。
- **切换指标。** 每根柱子的高度和颜色都以弹簧过渡到新数值，从周一 00:00 向周日 23:00 逐个稍晚开始，形成约三分之一秒的斜向波浪；摘要中的数字逐位滚动，文字淡入淡出交叉切换；图例颜色在 480 毫秒内变化。
- **相机。** 飞入、返回、重置和视图切换都是 1 到 1.4 秒的弹簧，被打断时保持速度；缩放更快；松开拖动后视图会滑行，过度拖动则在俯仰限制内弹回。俯仰变化时，地形与绘图区的适配也会沿自己的弹簧跟随。
- **悬停。** 柱子在约四分之一秒内抬起，一周中的其余部分淡去；提示卡在 120 毫秒内淡入，随后在相机移动时以弹簧跟随柱子。
- **减少动效。** 没有入场，也没有波浪；相机移动、视图切换和缩放立即到位；拖动跟随指针，但不滑行；提示卡只淡入淡出。关闭动效时，没有任何东西会动。
- 静止的绘图区不绘制任何内容；只有在有东西移动且绘图区位于屏幕上时才会运行帧。

## 相关组件

- `ActivityHeatmap`：一年的日数据，以日历热力图呈现。
- `BarChart`：按类别呈现数值，便于精确比较。
- `MetricCard`：一个数字及其变化，用于指标很多的仪表板。
