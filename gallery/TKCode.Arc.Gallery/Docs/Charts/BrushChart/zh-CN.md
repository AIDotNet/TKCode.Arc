---
title: 刷选图表
summary: 带概览条的密集时间序列：拖动窗口即可缩放，通过手柄调整大小，并在原处查看事件。
source-hash: 09686434a9f1
---

<Demo name="OverviewAndDetail" />

## 何时使用

`BrushChart` 在一个绘图区中绘制较长的每日序列，并在其下方的概览条中保留完整历史。读者在概览条上拖动一个窗口来选择所关心的时段，绘图区随之变化。它适合放在分析或复盘页面的主体部分，此时序列是页面的主要内容，探索它本身就是任务。

- **SaaS 管理后台中的每日活跃用户**：产品团队查看一年的每日会话，然后把窗口缩小到某次定价调整前后的几周。概览条让全年其余部分保持可见，读者不会丢失当前位置。
- **事件复盘时间线**：可靠性负责人为一个季度绘制 p95 延迟，并用 `Annotations` 标出部署与故障。每个事件同时显示在曲线和概览条上，复盘时能同时读到因果关系。
- **财务仪表板中的营收或订单量**：财务人员打开一个包含数千个数据点的每日营收序列，点击会设置 `Range` 的“最近 30 天”等预设，并在由 `RangeChanged` 更新的说明文字中读取新窗口。
- **账单页中的计量用量**：客户打开当前计费周期内每日的 API 调用次数，平移到一个峰值处，读取其日期和数值，然后再决定是否提交支持工单。

## 何时不用

- **只有几周的数据，或需要比较多个序列**：使用 `LineChart`。它能以图例绘制多个序列，并显示整个范围，没有概览条。
- **表格单元格或 KPI 卡片中的小型趋势**：使用 `Sparkline`，它没有坐标轴，也没有交互。
- **包含负值，或数值轴不能从零开始**：使用 `LineChart`，它提供 `YMinimum` 和 `YMaximum`。刷选图表的数值轴始终从零开始，因此无法显示带符号的序列。
- **随时间变化的构成分层，例如各渠道的流量**：使用 `Streamgraph`，它会堆叠各层。刷选图表只绘制一个序列。
- **随计数增长而变化的单一总数**：使用 `AnimatedCounter`，它为数字本身添加动画。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BrushChart Label="每日活跃用户"
                    Unit="用户"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

`BrushChart` 派生自 Arc 图表家族的基类 `ChartBase`。该基类提供共享的外壳：`Label`、`Unit`、`PlotHeight`、`EmptyLabel`、`ValueFormat`，带有 `XBinding` 与 `YBinding` 的 `ItemsSource`，以及提示规则。刷选图表额外提供长历史所需的功能：概览条、可见窗口（`Range`）、其最小跨度以及事件标记。它采用自绘方式：绘图区和概览条都由代码根据数据绘制，而不是由子元素拼装而成，这使长序列依然保持流畅。

每个数据项提供一个日期和一个数值。日期可以是 `DateTimeOffset`、`DateTime` 或 `DateOnly`，控件会在内部将其转换为时间。数据变化时，控件会按日期对数据点排序，因此源集合的顺序无关紧要，不过按升序排列的数据源可以省去这部分工作。

### 结构

1. **绘图区**：视图内数据点的原始折线；当视图缩放得很远时，改为显示七点平均值及其由低到高的范围带。绘图区默认高 240 px（`PlotHeight`）。
2. **数值轴**：位于右侧，占用 44 px 的边距（令牌的 `GutterWidth`）。它从零开始，其顶部以弹簧动画调整到可见范围内的最高点，刻度标签采用紧凑格式，例如 1.2K，除非设置了 `TickFormatter`。
3. **网格线**：在数值刻度处绘制的水平线，遵循 `ShowGrid`，默认开启。
4. **日期轴行**：位于绘图区下方，高 28 px（令牌的 `AxisRowHeight`）。其刻度标签随窗口宽度变化，从天到周、月和年。
5. **事件标记**：一条细线加一个圆点，使用第二数据系列颜色，并从左到右依次标注。与其他标签冲突的标签会被跳过，因此即使文字放不下，标记依然保留。
6. **十字线与提示**：十字线位于当前数据点上。提示显示日期、带 `Unit` 的数值以及当天的任何事件；空间不足时它位于数据点上方，否则位于下方，并且始终不会离开绘图区。
7. **概览条**（模板中的概览部分）：绘制完整历史，每个宽度绘制一次，窗口之外的部分会被压暗。高 `OverviewHeight`（默认 52 px），起点位于日期轴下方 8 px，终点与数值边距对齐，因此两个图表共享同一条水平刻度。
8. **窗口与手柄**：概览条上的一个环，两端各有一个抓手，抓手的命中区域为 28 × 44 px。拖动环可平移，拖动抓手可调整大小，在空白轨道上拖动可绘制新窗口，单击可重新居中，双击可重置。

### 选择窗口与预设

`Range` 是以 `ChartTimeRange` 表示的可见窗口（起点与终点都包含在内）。为 null 时窗口即为整个序列。`Range` 是双向的：拖动或按键会把新窗口写回它，代码也可以通过赋新值来移动窗口。代码修改会在完整动效下以弹簧动画滑动到新窗口，在减弱或关闭动效下则直接跳转。

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"窗口：{e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

预设适用于读者最常询问的窗口：最近 30 天、最近一个季度、最近一年以及完整序列。预设只是普通的赋值，因此同样的代码也适用于视图模型。重置手势（在窗口上按 Escape 或 0）始终回到完整序列，无论预设如何设置。

### 标注

`Annotations` 在绘图区和概览条上标记事件。每个 `ChartAnnotation` 包含一个日期、一段简短的标签以及可选的描述。空间足够时，标签会打印在标记旁边；描述则显示在提示中，并由屏幕阅读器播报，因此它可以承载简短标签无法容纳的细节。

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 发布", "新的引导流程已面向所有用户开放。"),
    new ChartAnnotation(start.AddDays(410), "媒体提及", "被每周通讯刊登。"),
    new ChartAnnotation(start.AddDays(530), "定价调整")
];
```

列表中只保留能够解释序列走势的事件。事件是读者解读数据时的一部分，因此不在列表中的发布无法从图表中找到；应在发布时加入，而不是等有人询问时再补上。

### 最小跨度

`MinSpan` 防止窗口缩小到某个跨度以下。默认值为七天（`TimeSpan.FromDays(7)`）。该限制适用于拖动、手柄、键盘和代码：比最小值更窄的范围会扩展到最小值，起点或终点超出数据范围的范围会被拉回数据之内。跨度同时受序列长度限制，因此短于最小跨度的序列会显示整个序列。

```xml
<arc:BrushChart Label="请求数" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

选择读者应能检查的最小跨度。每周复盘需要 14 天，这样窗口总能显示两个完整的周周期，星期模式就不会隐藏在数据点之间。

### 格式化

`DateFormatter` 格式化提示和屏幕阅读器播报中的日期，`TickFormatter` 格式化数值轴。日期文字的默认格式为英文（en-US），例如 "Tue, Sep 15, 2026"。窗口文字则使用较短的形式，例如 "Jan 1, 2026 to Apr 10, 2026"，并且不会随 `DateFormatter` 改变。当页面使用其他文化或带单位的数字时，应替换默认值。

```xml
<arc:BrushChart Label="营收"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

`ValueFormat`（.NET 格式字符串，例如 `N0` 或 `C0`）作用于提示和播报中的数值，`ShowGrid` 可关闭网格线，使绘图区更清爽。

### 绑定到视图模型

视图模型拥有序列、窗口和事件。刷选图表只负责绘制它们，并在读者移动窗口时把窗口写回。使用 CommunityToolkit.Mvvm 时：

```csharp
public sealed record DailyUsage(DateTimeOffset Date, double Calls);

public partial class UsageViewModel : ObservableObject
{
    private readonly DateTimeOffset _lastDay = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    [ObservableProperty]
    private IReadOnlyList<DailyUsage> _usage = [];

    [ObservableProperty]
    private ChartTimeRange? _window;

    [ObservableProperty]
    private ObservableCollection<ChartAnnotation> _releases = [];

    [RelayCommand]
    private void ShowLast30Days() => Window = new ChartTimeRange(_lastDay.AddDays(-30), _lastDay);

    [RelayCommand]
    private void ShowAll() => Window = null;
}
```


- `Range` 是双向的，因此拖动或按键会把 `Window` 写回，预设则会移动绘图区。代码对 `Window` 的修改会以弹簧动画滑动窗口，因此应在用户操作时设置它，而不要放在计时器中。
- `Annotations` 接受任何 `IEnumerable<ChartAnnotation>`。赋值该属性时标记会重绘，因此列表变化后应重新赋值（新列表或同一集合）以触发重绘。
- `ItemsSource` 是整体替换而不是追加。序列重新加载时，应赋一个新的 `IReadOnlyList<DailyUsage>`。
- `XBinding` 与 `YBinding` 读取日期和数值。编译形式 `{CompiledBinding Date, DataType=...}` 是对 AOT 安全的路径；使用它时需在视图上添加 `x:DataType`。示例为简洁起见省略了它。

### 示例：账单页上的 API 用量

账单页向客户展示其在当前周期内发出的 API 调用。读者需要一眼看清周期的形态，跳到最近 30 天，并在导致峰值的发布旁边读取该峰值。两个预设按钮调用视图模型的命令，这些命令会写入 `Window`；图表读写的是同一个属性，因此预设与拖动始终保持一致。发布标记来自产品团队用于更新日志的同一份列表，因此标注都是真实事件，而不是装饰。此处不显示截图：只有视图模型提供数据点后，图表才会绘制数据。

```xml
<StackPanel Width="720" Spacing="12">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="最近 30 天" Variant="Secondary" Size="Small" Command="{Binding ShowLast30DaysCommand}" />
        <arc:Button Content="全部" Variant="Secondary" Size="Small" Command="{Binding ShowAllCommand}" />
    </StackPanel>
    <arc:BrushChart Label="每日 API 调用次数"
                    Unit="次"
                    ValueFormat="N0"
                    ItemsSource="{Binding Usage}"
                    XBinding="{Binding Date}"
                    YBinding="{Binding Calls}"
                    Range="{Binding Window, Mode=TwoWay}"
                    Annotations="{Binding Releases}" />
</StackPanel>
```

### 数据结构与性能

控件要求每个日期只有一个数值。它通过 `XBinding`（或 `XSelector`）读取每个数据项的日期，通过 `YBinding`（或 `YSelector`）读取其数值。日期无法转换的数据项会被跳过。

数据变化时，控件会对数据点排序，并对整个序列一次性计算七点平均值及其范围带，然后重建概览条。每次数据变化都是对序列的一次完整遍历，因此应通过一次赋值替换整个集合，而不是逐个添加数据点。绘图区只绘制窗口内的数据点，以及两侧各多出的一个。当超过两个数据点落在同一像素列中时，折线只保留该列的最低与最高值，因此峰值得以保留，路径也保持精简。预期范围是一年的每日数据或数千个数据点。

数据变化时，当前窗口会被保留，并被限制在新数据和最小跨度之内。绑定的 `Range` 优先于当前窗口，因此应把 `Range` 设为想要的时段，重新加载后会跟随它。数值轴会针对视图内的数据点重新计算，并以弹簧动画调整到新的顶部。

### 文案规范

以序列计量的内容及其频率为它命名，因为读者会先看到标签，然后才看到坐标轴。单位应保持简短，事件细节则放在标注描述中，而不要放在标签里。

| 不要写 | 改为 |
|---|---|
| Chart | Daily active users |
| Users | Active users per day |
| Release | v2 launch |
| Click the chart to zoom | Drag the window to zoom |
| Long description of the release in the label | Short label, with the detail in the description |
| 12000.0 calls | 12,000 calls (`ValueFormat="N0"`) |

窗口文字和说明文字保持句首大写，日期使用页面其余部分所用的格式，例如 "Oct 1, 2026"。不要把窗口读数放在标题中：图表下方的说明文字已经足够。

### 最佳实践

**推荐**

- 将 `Range` 设为页面打开时所显示的时段。超过每像素两个数据点后，折线只保留每个像素列的最低与最高值，因此需要查看单个数据点的读者应使用更短的窗口；完整序列只作为后备。
- 将窗口预设放在图表旁边，并把 `Range` 设为双向绑定，使预设与拖动保持同步。
- 将 `MinSpan` 设为复盘所需的最短时段。每周复盘需要 14 天，这样窗口就不会隐藏每周的模式。
- 添加能够解释序列走势的标注。每个标注都是读者无需阅读表格即可找到的标记。
- 为图表设置 `Label` 和 `Unit`，使屏幕阅读器用户能正确听到摘要和提示。

**避免**

- 负值，或数值轴需要从非零值开始。刷选图表始终从零开始，因此带符号的序列会看起来像贴近底部的一条平线。
- 两周或更短的序列。数据点过少会使概览条难以阅读，窗口也没有意义；应使用 `LineChart`。
- 同一天有许多重叠的标注。发生冲突的标签会被跳过，读者会错过本应看到的事件。
- 从计时器或每帧都在变化的值中写入 `Range`。每次写入都会让窗口滑动，因此绘图区永远无法稳定。
- 为一个很小的更新重新加载整个序列。每次数据变化都会重建平均值与概览条，因此应把变更合并为一次赋值。

### 常见问题

**设置 `Range` 时，为什么窗口会贴齐到七天？** `MinSpan` 默认为七天。比最小跨度更窄的窗口会扩展到该跨度，并移动终点以保持跨度。如需允许更短的跨度，请把 `MinSpan` 设为你希望允许的最小值。

**从代码设置 `Range` 会有动画吗？** 在完整动效下会以弹簧动画滑动；在减弱或关闭动效下则直接跳转。拖动和按键始终与指针一对一跟随，因此只有代码修改会使用弹簧。

**数值轴从哪里开始？** 始终从零开始。其顶部会以弹簧动画调整到可见范围内的最高点，因此缩放到一段平缓的区间时，绘图区会被充分填满。若要更改刻度标签而非刻度范围，请使用 `TickFormatter`。

**为什么只有一个数据点时图表显示“暂无数据”？** 刷选图表至少需要两个数据点才能绘制折线和概览条。`EmptyLabel` 可替换默认文字，该图表的默认文字为“暂无数据”。

**如何在自己的文字中显示窗口？** 处理 `RangeChanged` 并读取 `e.Range`，其中的 `Start` 与 `End` 都是 `DateTimeOffset` 值。像上面的示例那样，使用你自己的格式模式进行格式化。同一窗口也可通过 `Range` 获取。

**如何修改提示中的日期格式？** 将 `DateFormatter` 设为 `Func<DateTimeOffset, string>`。它会格式化提示和播报；窗口文字仍保持较短的形式。

## API 参考

<!-- api: BrushChart -->

### 配套类型

| 类型 | 用途 |
|---|---|
| `ChartAnnotation(DateTimeOffset Date, string Label, string? Description = null)` | 时间轴上的一个事件，以带标签和提示说明的标记显示 |
| `ChartTimeRange(DateTimeOffset Start, DateTimeOffset End)` | 可见窗口；`Span` 返回其长度 |
| `ChartTimeRangeChangedEventArgs` | `RangeChanged` 的载荷；其 `Range` 属性即为新窗口 |

## 无障碍

- `Label` 为图表命名。窗口及其两个手柄为滑块，其数值文字即为日期。
- 绘图区是一个可聚焦的分组。其自动化名称由标签、窗口读数以及一条提示组成，提示说明可使用左右方向键读取数值。
- 对某个数据点的读取会播报日期、带单位的数值以及当天的任何事件，例如 "Sun, Jan 11, 2026, 110 users. Launch"。
- 一张隐藏的数据表列出窗口内的每个数据点及其事件。事件以第二种数据系列颜色标记，并附带文字标签或提示，因此不会仅靠颜色来区分。
- 窗口及其手柄的焦点环仅在键盘焦点时绘制。

### 键盘

概览条共有四个键盘停靠点，顺序为：绘图区、窗口、起点手柄和终点手柄。Tab 与 Shift+Tab 在它们之间移动。

| 按键 | 操作 |
|---|---|
| 绘图区：`Left` / `Right` | 将十字线向前或向后移动一个数据点；第一次 `Right` 读取可见的最后一个数据点 |
| 绘图区：`PageUp` / `PageDown` | 将十字线移动可见数据点的八分之一 |
| 绘图区：`Home` / `End` | 将十字线移到第一个或最后一个可见数据点 |
| 绘图区：`Escape` | 清除十字线 |
| 窗口：`Left` / `Down`、`Right` / `Up` | 向前或向后平移窗口的十分之一 |
| 窗口：`Shift` + 方向键、`PageUp` / `PageDown` | 平移窗口的一半 |
| 窗口：`+` / `-` | 以中心为基准放大或缩小 |
| 窗口：`Home` / `End` | 将窗口移到序列的开头或结尾 |
| 起点或终点手柄：`Left` / `Right` | 将边缘移动一个数据点（在每日序列中为一天） |
| 起点或终点手柄：`Shift` + 方向键 | 将边缘移动十个数据点 |
| 起点手柄：`Home` / `End` | 将起点移到第一个数据点，或移到终点之前的最小跨度处 |
| 终点手柄：`Home` / `End` | 将终点移到起点之后的最小跨度处，或移到最后一个数据点 |
| 窗口或手柄：`Escape` / `0` | 将窗口重置为完整序列 |

## 动效

- 首次显示时，折线从左侧绘制出来一次，使用较长的无回弹弹簧。
- 拖动时窗口在各动效级别下都与指针一对一跟随。预设、重置以及 `Range` 的变化，在完整动效下以弹簧滑动窗口，在减弱或关闭动效下则直接跳转。
- 完整动效下，数值轴以弹簧调整到可见范围内的最高点。随着密度变化，原始折线与平滑趋势线交叉淡化。
- 完整动效下，十字线在数据点之间平滑滑动。
- 减弱动效时窗口立即到位，十字线保持原处。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 折线从左侧绘制一次，使用较长的弹簧 | 直接显示完成的折线 | 直接显示完成的折线 |
| 拖动窗口或手柄 | 与指针一对一跟随 | 与指针一对一跟随 | 与指针一对一跟随 |
| 预设、重置或新的 `Range` | 窗口以弹簧滑动 | 窗口立即移动 | 窗口立即移动 |
| 数值轴顶部 | 以弹簧调整到可见范围内的最高点 | 立即移动 | 立即移动 |
| 十字线 | 在数据点之间滑动 | 立即移动，保持原处 | 跳到数据点 |

## 相关组件

- `LineChart`：几周的数据、多个序列或带符号的数值轴。
- `Sparkline`：无交互的小型趋势。
- `Streamgraph`：多个图层随时间的构成。
- `AnimatedCounter`：变化中的单一总数。

| 组件 | 适用场景 |
|---|---|
| `LineChart` | 时段较短、需要比较多个序列，或数值可能为负 |
| `Sparkline` | 趋势位于表格单元格或 KPI 卡片中，且无需交互 |
| `Streamgraph` | 问题是整体各部分如何随时间变化 |
| `AnimatedCounter` | 一个总数在变化，数字本身就是重点 |
| `BarChart` | 按时段比较数值，而不是沿连续时间轴阅读 |
