---
title: 河流图
summary: 在摆动基线上堆叠的流层，可在不同范围之间变形；可单独隔离某一层并逐周读取。
source-hash: d66fbacbb470
---

<Demo name="TopicsOverTime" />

## 何时使用

`Streamgraph` 展示总量随时间变化时，由哪些部分构成。每个部分是一层，各层沿共享的时间轴汇成一条带状的河流，读者一眼就能同时看到总量的大小与每个部分所占的份额。它适合回答“构成比例何时、如何变化？”的屏幕，而不适合回答“每个部分的精确数值是多少？”。

- **运营评审中按主题划分的支持工单**：每周的工单量拆分为账单、登录、性能、集成与移动端。发布之后，性能层膨胀，读者既能看到构成的变化，也能看到总量的上升。
- **营销看板中按获客渠道划分的流量**：26 周内每天来自搜索、社交、邮件、推荐与直接访问的访问量。河流能看出哪个渠道带动了某次活动，以及其他渠道如何收缩让出空间。
- **SaaS 财务评审中按套餐等级划分的营收**：来自入门版、成长版与企业版的月度经常性收入。读者能看到构成变化，例如一波升级潮，而无需比较五条独立的折线。
- **可靠性报告中一个季度内的事故类别**：每周每个类别的事故数。季节性与峰值以流动的层呈现，比独立的折线更清楚。
- **电商规划视图中按商品类别划分的季节性需求**：每周每个类别的售出件数，带状的形状显示各类别何时达到峰值。

## 何时不用

- **精确数值或层与层之间的比较**，例如“三月账单的增长是否快于登录？”：用 `LineChart`，它为每个系列提供独立的基线与坐标轴。
- **少量期间或离散类别**，例如四个季度或六个地区：用 `BarChart`，每个值是一根独立的柱子，读者可以比较它们的高度。
- **包含负值的数据**，例如净流量或各业务线的损益：各层从基线堆叠，河流便不再表示总量的组成部分。请用 `LineChart`。
- **某一时刻一个总量的组成部分**，例如今天各地区的营收占比：用 `DonutChart`。河流需要时间轴才有存在的价值。
- **许多都很小的层**：超过八层时，河流会变成一堆细条。请把最小的几层合并为“其他”系列，或在每个系列都重要时改用 `LineChart`。
- **比较多个组的分布，而非它们随时间的总量**，例如每个服务响应时间的离散情况：用 `Ridgeline`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Streamgraph Label="各主题的支持工单"
                     Unit="工单"
                     CategoryLabel="周"
                     ItemsSource="{Binding Weeks}">
        <arc:Streamgraph.Series>
            <arc:ChartSeries Key="billing" Name="账单" Values="{Binding Billing}" />
            <arc:ChartSeries Key="login" Name="登录" Values="{Binding Login}" />
            <arc:ChartSeries Key="performance" Name="性能" Values="{Binding Performance}" />
        </arc:Streamgraph.Series>
    </arc:Streamgraph>
</UserControl>
```

每个 `ChartSeries` 是一层。`ItemsSource` 保存共享的 x 位置，例如每周的起始日期，每一层的 `Values` 在每个位置上都有一个数字。悬停或聚焦某一层即可将其隔离。

`Streamgraph` 派生自 `ChartBase`，即 Arc 自绘图表的基类，与 `LineChart`、`BarChart` 共享数据管线、色板、首次显示动画、读数与无障碍摘要。河流在此之上增加堆叠、层隔离与直接标签。由于各层是堆叠的，每一层的高度就是它的数值，而河流在每一周的总高度就是可见各层的总和。

### 结构

1. **图例**（`ShowLegend`）：每层一个方形色块与名称，位于绘图区上方，与绘图区相隔 16 个单位。图例向两侧各延伸 8 个单位进入边距，使其悬停区域与绘图区边缘对齐。有多于一层时默认显示。
2. **绘图区**（`PlotHeight`，默认 260）：堆叠的各层，每层以其系列颜色填充。相邻的层之间以 2 个单位的表面颜色间隙分隔，因此无需描边也能看清边界。
3. **直接标签**（`ShowDirectLabels`，默认开启）：层名称显示在该层最粗的位置，并带有表面颜色的光晕，使文字在任何一层上都清晰可读。只有当该处的层厚度至少为 19 个单位，且文字能放入绘图区时才显示标签。
4. **十字准线**：位于当前周的 1 px 竖线，使用前景色，不透明度 50%。
5. **提示**：本周的标题，以及每个可见层一行，按堆叠顺序排列。被隔离层的那一行会被强调。
6. **时间轴**（`ShowXAxis`）：绘图区下方 26 个单位高的一行，显示分类标签。范围较长时，显示的标签较少。

### 层与系列

每一层是一个带有 `Key`、`Name` 与 `Values` 的 `ChartSeries`。`Key` 在 `HiddenSeries`、`ActiveSeriesKey` 以及 `HiddenSeriesChanged`、`ActiveIndexChanged` 事件中标识该层，因此在数据刷新时应保持稳定。`Name` 会出现在图例、直接标签、提示与播报中。

按读者应当看到的内容排列各层。堆叠时，第一层位于河流的中央，其余各层交替排在它的上方与下方，因此最大或最重要的层通常适合作为第一层。在共用图例的多个屏幕上保持相同的顺序，使同一主题每次都在相同的位置。

### 偏移

`Offset` 决定各层围绕基线的堆叠方式。默认值为 `Wiggle`。

| 值 | 堆叠方式 | 适用情境 |
|---|---|---|
| `Wiggle`（默认） | 使各层的斜率最小化，河流流动且保持水平 | 故事在于流动：各层一起起伏，读者应跟随这条带状 |
| `Silhouette` | 将整个堆叠围绕一条平直的中线置中 | 总量的起伏是重点，各层是围绕它的细节 |
| `Zero` | 从底部平直的基线按顺序堆叠各层 | 读者需要稳定的基线，以比较最底层与其上各层 |

```xml
<arc:Streamgraph Label="主题" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

改变 `Offset` 只会重新堆叠各层，不需要新数据。每个屏幕只使用一种偏移，使读者在两个图表之间切换时，同一层不会换位置。

### 范围切换

范围变化时，同时替换 `ItemsSource` 与各系列的数值。每一层都会从屏幕上的当前形状融合到新形状；不同长度的范围同样会变形过渡，因为在融合之前，每一层都会被重新取样到同一个网格上。

```csharp
void ShowRange(Streamgraph chart, IReadOnlyList<DateOnly> weeks, double[][] valuesByLayer)
{
    chart.ItemsSource = weeks;
    for (var i = 0; i < chart.Series.Count; i++)
    {
        chart.Series[i].Values = valuesByLayer[i];
    }
}
```

被隐藏的层会收缩至消失，其余层会围绕它合拢；读者再次显示该层时，它以相同的方式回来。

### 图例、直接标签与隐藏层

图例与直接标签用于命名各层。空间足够时，直接标签绘制在每一层最粗的部分，因此很细的层可能没有标签，但它的图例条目仍会标明它。图例已足够、而绘图区又很拥挤时，请关闭 `ShowDirectLabels`。

`HiddenSeries` 为双向属性：图例会切换它，视图模型也可以保存被隐藏的键。被隐藏的层会从堆叠、提示、播报与总量中移除，其余各层围绕它重新堆叠。

```xml
<arc:Streamgraph Label="主题"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### 读取某一周

`ActiveIndexChanged` 报告指标或键盘所在的那一周。它的 `Index` 与 `Label` 标识该周，`SeriesKey` 指明被隔离的层，`Values` 列出该周每一层的数值。`ActiveSeriesKey` 随时返回被隔离的层；未隔离任何层，或被隔离的层已被隐藏时，返回 `null`。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有各周、各层的数值与被隐藏的键。使用 CommunityToolkit.Mvvm 时，视图模型是 `ObservableObject`，每次范围变化对应一个命令。

```csharp
public sealed record TicketReport(
    IReadOnlyList<DateOnly> Weeks,
    double[] Billing,
    double[] Login,
    double[] Performance);

public interface ITicketApi
{
    Task<TicketReport> GetTicketsByTopicAsync(int weeks);
}

public partial class TicketTrendViewModel : ObservableObject
{
    private readonly ITicketApi _api;

    public TicketTrendViewModel(ITicketApi api) => _api = api;

    [ObservableProperty]
    private IReadOnlyList<DateOnly> _weeks = [];

    [ObservableProperty]
    private double[] _billing = [];

    [ObservableProperty]
    private double[] _login = [];

    [ObservableProperty]
    private double[] _performance = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [RelayCommand]
    private async Task LoadRangeAsync(int weeks)
    {
        // 各周的起始日期与各层的数值来自同一份报告，因此图表一次性接收它们的更新。
        var report = await _api.GetTicketsByTopicAsync(weeks);
        Weeks = report.Weeks;
        Billing = report.Billing;
        Login = report.Login;
        Performance = report.Performance;
    }
}
```

```xml
<arc:Streamgraph Label="按主题统计的支持工单"
                 Unit="工单"
                 CategoryLabel="周"
                 ItemsSource="{Binding Weeks}"
                 HiddenSeries="{Binding HiddenKeys, Mode=TwoWay}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="账单" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="登录" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="性能" Values="{Binding Performance}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

`HiddenSeries` 是双向的，因此视图模型能看到读者隐藏了哪些层，并能在刷新后保持它们隐藏。`ItemsSource` 与各层的数值是单向的。`HiddenSeriesChanged` 事件在其 `Hidden` 属性中报告新的隐藏列表，供偏好事件的代码使用。`ActiveSeriesKey` 为只读；若要响应读者的移动，请使用 `ActiveIndexChanged`。

### 数据结构与性能

数据变化时，每一层都会被重新取样到固定的 161 个点的网格上；之后每一帧都会根据这些点重新计算堆叠、边缘与标签。因此成本更多取决于层数，而不是周数。请将层数控制在三到八层，并保持数值非负。

- **周数**：每周是一个类别。无论周数多少，曲线都会被重新取样到固定网格上，时间轴会挑选不重叠的标签。
- **各层的数值**：每一层在每周都需要一个数值，顺序与 `ItemsSource` 相同。某周没有活动时传入 0，使各层在相同的周上对齐。
- **更新频率**：每天或每周刷新一次，读起来像是范围的变化。每隔几秒更新一次的河流会让各层持续移动，使隔离难以使用。
- **格式化**：数值由 `ValueFormat` 格式化（.NET 格式字符串，en-US），`Unit` 则出现在提示与播报中的数值之后，例如“4 tickets”。若产品所用的日期格式不同于默认的短日期，请用 `XAxisFormatter` 格式化坐标轴日期。

### 示例：发布后的工单构成

支持负责人回顾过去 26 周的工单。河流显示：发布之后的几周，性能层增长，而账单层保持平稳。图例保持可见，使每一层都可以被隐藏或隔离；直接标签为较粗的层命名，时间轴则标注各周。读者无需重新查询，就能隐藏移动端层，查看其余四层的构成。

```xml
<arc:Streamgraph Label="按主题统计的支持工单，26 周"
                 Unit="工单"
                 CategoryLabel="周"
                 PlotHeight="240"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 Offset="Wiggle"
                 ItemsSource="{Binding Weeks}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="账单" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="登录" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="性能" Values="{Binding Performance}" />
        <arc:ChartSeries Key="integrations" Name="集成" Values="{Binding Integrations}" />
        <arc:ChartSeries Key="mobile" Name="移动端" Values="{Binding Mobile}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

键是固定的字符串，因此数据刷新时，被隐藏的层会保持隐藏。`Name` 的值是读者在图例与提示中看到的文字，因此它是为人编写的，而不是为数据模型编写的。

### 文案规范

以层所计数的事物为各层命名，使用句子式大小写，并在 `Unit` 中写明单位。图例若写着“账单、登录、性能”，读者无需对照说明，就能扫读整条河流。

| 不要写 | 改为 |
|---|---|
| `Label="Chart 1"` | `Label="按主题统计的支持工单"` |
| `Name="billing_v2"` | `Name="账单"` |
| `Unit="count"` | `Unit="工单"` |
| `CategoryLabel="x"` | `CategoryLabel="周"` |
| `Name="Other (misc.)"` | `Name="其他"`，并把最小的类别并入其中 |
| `Name="ALL CAPS TOPIC"` | `Name="登录"` |

### 最佳实践

**推荐**

- 保持各层的键稳定。在 `HiddenSeries` 与事件中，层由键而非名称标识；键一旦改名，读者隐藏过的层就会被重置。
- 在传入数据之前，把最小的类别并入“其他”。太细、放不下直接标签的层很难隔离，此时图例与提示是它唯一的标识。
- 每个屏幕只使用一种偏移。改变偏移会重新堆叠各层，使读者难以在两个图表之间跟随同一层。
- 范围变化时，同时替换各周的起始日期与每一层的数值，使各层从屏幕上的形状变形为新形状。
- 在 `Unit` 中写明单位，使提示与播报读作“4 tickets”，而不是一个光秃秃的数字。

**避免**

- 用于包含负值的数据。基线以下的堆叠没有意义；请改用 `LineChart`。
- 超过八层。细层会变成读者无法隔离的细条；请合并它们。
- 用于层与层之间的精确比较。堆叠的层是靠其带状来阅读的，精确数值只在提示与播报中。
- 在小型绘图区中放入许多层的直接标签。细层上的标签会被隐藏，因此图例才是可靠的对照依据。
- 每隔几秒刷新一次数据。每次刷新都会移动所有层，河流对读者而言永远不会稳定。

### 常见问题

**如何从代码中隔离某一层？** 图表没有用于设置被隔离层的属性。指针、键盘与图例预览会设置它，而 `ActiveSeriesKey` 用于读回。若要驱动详情面板，请在 `ActiveIndexChanged` 中读取 `ActiveSeriesKey`，或在需要当前层时读取。

**为什么隐藏某一层后，一层消失而其他层会移动？** 被隐藏的层会从堆叠中移除，因此其余各层会填满它的空间并重新堆叠。它的图例条目仍然保留，键也保存在 `HiddenSeries` 中，直到读者再次显示它。

**播报会说什么？** 它会说出该周、被隔离层及其数值、可见层的数量，以及可见层的总量，例如“W5. Login 4 tickets, 3 layers, 30 tickets in total”。没有被隔离的层时，只说出该周与总量。请设置 `Unit` 与 `Label`，使播报有意义。

**为什么某一层没有直接标签？** 只有当该处的层厚度至少为 19 个单位，且文字能放入绘图区时，才会绘制直接标签。很细或很窄的层会保留它的图例条目与提示行，当层变厚后标签会重新出现。

**可以把 x 轴用于日期吗？** 可以。`ItemsSource` 接受 `DateTime`、`DateTimeOffset` 或 `DateOnly`，分类标签遵循默认的日期格式。将 `CategoryLabel` 设为表头应使用的词，例如“周”。

## API 参考

<!-- api: Streamgraph -->

## 无障碍

- `Label` 为图表命名。绘图区是一个以标签与键盘操作说明命名的可聚焦分组，例如“按主题统计的支持工单。使用左右方向键沿时间移动，上下方向键切换层。”
- 每次读取都会更新数值文字，并以礼貌播报（polite）的方式播报该周、被隔离层的数值与可见层的总量。使用屏幕阅读器的读者听到的数字，与提示所显示的一致。
- 图例条目是切换开关。聚焦某个条目会隔离其所属的层，按下 `Enter` 则显示或隐藏该层。
- 隐藏的数据表列出每一周的每一层。
- 各层直接在绘图区与图例中命名，因此不会只靠明暗来区分。相邻的层之间以 2 个单位的表面颜色间隙分隔。
- 使用指针时，移到某一层上即会隔离它，离开绘图区则清除读数。使用触控时，读数停留在被触碰的位置，直到接触结束。

### 键盘

绘图区可通过 `Tab` 取得焦点。焦点从键盘进入时，读数从最新一周开始。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在图例条目与绘图区之间移动焦点 |
| `Left` / `Right` | 移到上一周或下一周。静止时，`Right` 停在最新一周 |
| `Up` / `Down` | 按系列顺序隔离当前周中的下一个或上一个可见层。未隔离任何层时，`Up` 选中最后一个可见层，`Down` 选中第一个 |
| `PageUp` / `PageDown` | 向前或向后移动约六分之一的周 |
| `Home` / `End` | 移到第一周或最后一周 |
| `Escape` | 清除读数与被隔离的层 |
| `Enter`（在图例条目上） | 显示或隐藏该层 |

## 动效

- 首次显示时，河流从一根细线开始，沿中心线向外膨胀，使用不过冲的长弹簧。
- 新范围或新数据集会在共享网格上融合每一层，因此各层保持其顺序与速度。
- 被隐藏的层会收缩至消失，其余层围绕它重新堆叠。
- 十字准线与提示在弹簧上滑动；隔离某一层时，其余层会变暗。
- 减弱动效时立即显示最终状态，十字准线与提示则跳到新的一周，不使用弹簧。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 河流沿中心线以长弹簧膨胀，不过冲 | 直接显示最终形状 | 直接显示最终形状 |
| 新范围或新数据集 | 各层以平滑弹簧融合为新形状；新的层从无到有地长出 | 各层直接跳到新形状 | 立即 |
| 隐藏某一层 | 该层收缩至消失，其余层围绕它重新堆叠 | 直接跳到重新堆叠后的形状 | 立即 |
| 隔离某一层 | 其余层以短促的标准补间淡出 | 同样的短补间 | 立即 |
| 十字准线与提示 | 以弹簧滑动到新的一周 | 跳到新的一周，不使用弹簧 | 立即 |

## 相关组件

- `LineChart` 用于需要精确比较各系列数值的场合，`BarChart` 用于少量离散期间。
- `Ridgeline` 用于比较多个组的分布，而非它们随时间的总量。
- `DonutChart` 用于显示一个总量中各部分的占比。

| 组件 | 适用场景 |
|---|---|
| `LineChart` | 读者必须比较精确数值，或数据含有负值 |
| `BarChart` | 有少量离散期间或类别需要比较 |
| `Ridgeline` | 比较多个组的分布，而不是它们随时间的总量 |
| `DonutChart` | 在某一时刻显示一个总量中各部分的占比 |
| `Sparkline` | 在单一总量的标题数值旁显示它的趋势 |
