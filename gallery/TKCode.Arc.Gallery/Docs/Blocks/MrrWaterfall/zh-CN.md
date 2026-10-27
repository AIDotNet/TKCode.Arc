---
title: MRR waterfall
summary: 从周期开始到结束的经常性收入桥接：为新增业务、扩展、重新激活、收缩和流失绘制的悬浮柱，一个列出任意柱背后账户的侧面板，以及让每根柱、刻度和数字在同一条弹簧上移动的周期与单位开关。
source-hash: 48c0aedf240d
---

<Demo name="Preview" />

## 何时使用

`MrrWaterfall` 回答"这个周期里我们的经常性收入去了哪里？"第一根柱是期初收入，最后一根柱是期末收入，中间的柱悬浮着：每一根都从上一步停下的位置开始，赢得收入（新增业务、扩展、重新激活）时上升，失去收入（收缩、流失）时下降。图表上方，期末收入及其变化旁边是财务团队最先询问的三个比率：净收入留存率、快速比率和客户流失率。

- **董事会和财务报告。** 一张图展示一个月或一个季度，每一步背后的账户只需点击一下。
- **收入运营复盘。** 在各月与整个季度之间切换，看哪一类变动发生了变化，并用占期初百分比的单位比较规模不同的周期。
- **客户成功跟进。** 选中流失柱看谁离开了以及原因，再选中扩展柱看谁增长了。

## 何时不用

- **多个周期的趋势。** 桥接一次只展示一个周期；十二个月请使用折线图或面积图。
- **不是从已知水平起步的收入。** 每根柱都取决于期初收入；没有它时请使用柱状图。
- **每一步有数百个账户。** 面板列出金额最大的账户，并把其余账户合并为一行；它不是表格。请从该行链接到账户列表。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。它的数据是一组 `MrrPeriod`。一个周期包含期初收入（`StartingMrr`）、期初和期末的付费账户数、它的名称和日期（`Label`、`LongLabel`、`Caption`、`StartDateText`、`EndDateText`），以及按柱顺序排列的 `Movements`。每个 `MrrMovement` 有一个 `Kind`、一个正数 `Amount`（符号由类型决定）、一个 `AccountCount` 和其背后具名的 `Accounts`。期末收入、净变化和每个比率都由区块计算。

```xml
<arc:MrrWaterfall Title="Recurring revenue bridge" Scope="All tiers">
    <arc:MrrPeriod Label="Q2" LongLabel="Q2 2026" Caption="Apr 1 – Jun 30, 2026"
                   StartDateText="Apr 1" EndDateText="Jun 30"
                   StartingMrr="286400" StartingAccounts="892" EndingAccounts="948">
        <arc:MrrMovement Kind="New" Label="New business" ShortLabel="New" CompactLabel="New"
                         Amount="39800" AccountCount="116" CountNoun="new customers" />
    </arc:MrrPeriod>
</arc:MrrWaterfall>
```

```csharp
bridge.Periods.AddRange(periods);
bridge.SelectedPeriod = periods[^1];
bridge.SelectedIndex = 5; // the churn bar
```

`PeriodsSource` 则接受由你持有的集合（`ObservableCollection` 会被跟踪，修改所显示列表中的某个周期会就地更新区块）。只有一个周期时，周期开关会被隐藏。

### 结构

- **标题区**：`Title`、副标题（`Scope` 后接周期的 `Caption`），以及周期开关和单位开关。
- **摘要**：逐位滚动的期末收入、它相对期初的变化（箭头、金额和百分比，按方向着色），以及三个比率。
- **图表**：一条数值坐标轴、七根带标签的柱、把每一步的终点带到下一根柱的连接线，以及选中柱下方的标记。
- **面板**：选中柱（或被预览的柱）背后的账户。

### 坐标轴和柱

数值坐标轴不从零开始，否则各步在总额旁边将看不见。它的取值范围在最低水平之下延伸桥接跨度的 0.6，在最高水平之上延伸 0.05，其刻度步长取最接近该范围六分之一的、由 1、2、2.5 和 5 乘以十的幂得到的值。因此第一根和最后一根柱被截断：每根柱上的一道斜向断裂标记表明了这一点。柱最宽 56，或为其所占区间的 56%，且每条边都落在整像素上。

### 选中一根柱和预览一根柱

点击一根柱或按方向键，面板会列出其背后的账户：按金额排列最大的 `MaxNamedAccounts`（5）个，然后是代表其余部分的一行。每一行有一条表示其占该步份额的条。起点柱和终点柱则列出 `StartingBreakdown` 和 `EndingBreakdown`（套餐层级），没有"其余"行。`SelectedIndex` 是双向的，在周期和单位变化后依然保留，并引发 `SelectionChanged` 和 `SelectionCommand`。

使用鼠标时，指向一根柱会预览它的列表而不选中它：该柱出现底板，其他柱和标签淡出，提示文字变为说明点击会保留该列表。`HoveredIndex` 报告它。触摸只会选中。

### 周期和单位

`SelectedPeriod` 和 `ValueMode` 是双向的。更改其中任何一个都会让柱、连接线、坐标轴、标签和面板行一起移动：见动效。

<Demo name="Percent" />

在 `PercentOfStart` 中，每个值都是占该周期期初收入的份额，因此起点柱是 100%。头条数字仍然使用货币。刻度、标签和面板金额跟随该单位。

### 文字、符号和格式

所有文本都来自 `Labels`（`MrrWaterfallLabels`）：开关名称、头条标签、面板提示、读给屏幕阅读器的句子。`CurrencySymbol` 在所有位置更改符号；`CurrencyFormatter`、`CompactFormatter` 和 `PercentFormatter` 替换图表和朗读文本的数字格式。会滚动的数字使用该符号和内置格式。

<Demo name="SinglePeriod" />

### 窄容器

该区块读取自身的宽度。小于 800 时面板移到图表下方；小于 560 时头条和统计数字缩小，统计数字共用一行；当某个区间窄于 72 时，柱名称缩短为 `CompactLabel`，柱标签去掉货币符号；小于 48 时名称使用 10px 字号。

<Demo name="Narrow" />

### 事件

<Demo name="Events" />

## API 参考

<!-- api: MrrWaterfall -->

<!-- api: MrrPeriod -->

<!-- api: MrrMovement -->

<!-- api: MrrAccount -->

<!-- api: MrrWaterfallLabels -->

<!-- api: MrrSelectionChangedEventArgs -->

<!-- api: MrrPeriodChangedEventArgs -->

<!-- api: MrrValueModeChangedEventArgs -->

<!-- api: MrrMovementKind -->

<!-- api: MrrValueMode -->

## 无障碍

- 图表是一个可聚焦的分组，其名称是一句话，给出周期、期初收入、每一步和期末收入，并以按键提示作为帮助文本。键盘焦点环围绕选中柱所在的列。
- 每次选择都会以礼貌方式播报柱的名称、带符号的金额和账户数："Churn, −$18,460, 79 customers left"。周期变化会播报发生了什么变动。
- 一张隐藏的表格为每根柱列出变动、金额、占期初收入的份额和账户数。
- 面板以它所展示的柱命名（"Accounts behind churn"）。开关有名称，头条数字作为一个短语朗读，并用文字说出其变化，而不仅靠颜色。

### 键盘

- **Tab** 依次经过周期开关、单位开关和图表，各占一个停靠点。
- **← ↑** 和 **→ ↓** 把选择移到上一根或下一根柱，并从最后一根柱循环回第一根。移动即选中，因此面板随之变化。
- **Home / End** 选中第一根和最后一根柱。
- 在两个开关中，方向键更改选择并循环。

## 动效

- **周期或单位**：柱端、连接线、标签和坐标轴取值范围共用一条临界阻尼弹簧，约 600 ms 内落定。标签随之计数（"+$21.9k" 变到 "+$39.8k"）。保留的刻度随取值范围滑动；新刻度淡入（60 ms 后开始，240 ms），被去掉的刻度在滑动的同时于 160 ms 内淡出。单位变化会替换所有刻度。
- **面板行**：行按账户匹配。保留的行在同一条弹簧上滑到新位置，新行淡入并上升（240 ms，间隔 35 ms），被移除的行在原处于 120 ms 内淡出，份额条在弹簧上增长。行内的数字逐位滚动。
- **选择标记**：一条快速、不回弹的弹簧（横跨图表宽度约 400 ms）；柱名称在 160 ms 内变为加重。
- **悬停和焦点**：其他柱在 160 ms 内淡至 28%，标签淡至 32%；底板立即出现。
- **文字**：副标题、头条标签、变化文字、面板标题和行详情向上滑出再滑入（滑出 160 ms，滑入 240 ms）。
- 减少动效会让一切立即到位，唯独被去掉的刻度仍会淡出 160 ms。关闭会立即应用每一个变化。

## 相关组件

- `Metrics dashboard`、`Cohort retention` 和其他 Analytics 区块。
- `SegmentedControl`，该区块所用的开关。
