---
title: 迷你趋势图
summary: 在数值旁展示紧凑的趋势。
source-hash: 41c455f4761a
---

<Demo name="KpiCards" />

## 何时使用

`Sparkline` 是一条小型趋势线，带有主数值与附注，尺寸适合放在卡片、表格单元格或 KPI 区块中。它在数字所占的空间里回答“这个数字正在往哪里走？”，让读者无需坐标轴和图例，就能在当前数值旁看到指标的走向与形状。

- **分析概览中的收入与用量卡片**：卡片显示 `$48.2K` 和 `+4.1%`，下方是最近 14 天的收入。形状能看出数字是平稳上升，还是某一天突然飙升。
- **运维控制台中的服务行**：表格的每一行是一项服务，显示延迟或错误率趋势。同样大小的多行让表格易于扫视：一眼就能找到曲线走向不对的那项服务。
- **CRM 或支持工具中的流失、队列与积压卡片**：一个数值加上走向与色调，例如队列深度上升时用警告色显示。色调告诉读者这一趋势是好是坏。
- **账单与用量摘要**：每位客户的席位数或每日 API 调用量，旁边是当前总数。读者需要的是近期的规律，而不是精确的每个数据点。
- **在详情面板中回看近期历史**：读者沿线条移动指针或方向键，即可读取较早某个数据点的数值与日期，无需打开完整图表。

## 何时不用

- **需要并排比较的离散周期**，例如十二个月的收入或每个工作日的订单数：用 `BarChart`，它有数值轴，每个周期一根柱子。
- **只有当前数值及其变化、没有历史**：用 `MetricCard`。只有一个数据点的迷你趋势图会画成一条平线，无法说明走向。
- **需要坐标轴、多个系列或图例的趋势**，例如一个季度内各产品线的收入：用 `LineChart`。
- **读者必须缩放或平移的超长序列**，例如一年的分钟级指标：用 `BrushChart`，它在完整范围上保留一个窗口，让读者选择一段时间。
- **相对已知范围的数值**，例如 500 GB 配额中的磁盘使用量：用 `Gauge`，它显示数值在刻度上的位置，而不是它的变化路径。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Sparkline Label="收入"
                   Value="$48.2K"
                   Change="+4.1%"
                   Tone="Success"
                   Width="220"
                   ItemsSource="{Binding Revenue}" />
</UserControl>
```

`ItemsSource` 按从旧到新的顺序保存数值，每个值对应一个数据点。`Value` 与 `Change` 构成静止时显示的主数值与附注。`Label` 为指标命名。

`Sparkline` 派生自 `ChartBase`，即 Arc 自绘图表的基类。该基类提供数据管线（对象项使用 `ItemsSource` 配合 `YBinding` 或 `YSelector`，也可直接使用普通数字）、色板、首次显示动画、当前数据点读数（`ActiveIndex`）、数值格式化以及无障碍摘要。迷你趋势图在此之上增加主数值与附注、拖动查看手势，以及没有坐标轴的绘图区。由于它与 `LineChart`、`BarChart` 共用基类，同一屏幕中的数据与格式化规则完全一致。

### 结构

1. **标签**（`Label`）：指标名称，14 px，次要文字颜色，与主数值的基线对齐。它也是无障碍名称的开头。
2. **主数值**（`Value`）：18 px，中等字重，前景色，在其位置中右对齐。`Value` 为 null 时隐藏。
3. **附注**（`Change`）：12 px，中等字重。静止时以色调颜色显示 `Change`（`Accent` 使用强调色）。拖动时显示数据点（见下文“主数值、附注与拖动查看”）。
4. **标题间距**：标题行与绘图区之间 12 个单位的间距。
5. **绘图区**：线条、填充、1 px 的基线（边框色）以及拖动光标。线条宽 2.5 个单位，端点为圆角。
6. **面积**（`ShowArea`）：线条下方 9% 的色调文字颜色薄层。它绘制在线条之下，可以关闭。
7. **终点圆点**：一个位于最新数据点上的色调圆点，静止时直径 7 个单位，拖动某个数据点时为 10。首次显示动画结束前隐藏。
8. **焦点环**：绘图区获得键盘焦点时，Arc 以焦点色绘制焦点环。窗口默认的焦点装饰会被隐藏。

### 主数值、附注与拖动查看

附注区域有两种状态。静止时显示 `Value` 与 `Change`；拖动某个数据点时，改为显示该数据点，主数值与附注滚动为新的文字。

| 状态 | 主数值 | 附注 |
|---|---|---|
| 静止，已设置 `Value` 与 `Change` | `Value` | `Change`，使用色调颜色 |
| 拖动，已设置 `Value` 与 `PointLabels` | 拖动到的数值，按 `ValueFormat` 格式化 | 数据点标签，例如 `Sep 14` |
| 拖动，已设置 `Value`，未设置 `PointLabels` | 拖动到的数值 | 位置，例如 `4 of 14` |
| 拖动，`Value` 为 null | 隐藏 | 拖动到的数值与数据点标签，以圆点分隔 |
| 静止，`Value` 为 null | 隐藏 | `Change`；`Change` 为空时不显示 |

拖动时，附注改用次要文字颜色，而不是色调颜色。该数据点上并没有发生变化，日期或位置只是背景信息，因此保持中性。

### 色调

`Tone` 决定线条、填充、终点圆点，以及静止时的附注颜色。请根据指标的走向及该走向是否理想来选择，而不是为了装饰。

| `Tone` | 适用场景 | 附注颜色 |
|---|---|---|
| `Accent`（默认） | 中性趋势，例如访问量，或没有好坏之分的指标 | 强调色 |
| `Success` | 数值上升即为改善的指标：收入、转化率、可用性 | 成功色 |
| `Warning` | 需要关注但不属于错误的数值：正在增长的队列、上升的积压 | 警告色 |
| `Danger` | 正在变差的指标：流失率、错误率、超出目标的延迟 | 危险色 |

```xml
<arc:Sparkline Label="流失率" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="队列深度" Value="1,204" Change="上升" Tone="Warning" ItemsSource="{Binding Queue}" />
```

同时在 `Change` 中保留正负号。无法分辨颜色的读者仍能读到 `+0.3 pts`，黑白打印的读者也能读出走向。

### 数据点标签

`PointLabels` 为每个数据点提供一个标签，顺序与 `ItemsSource` 相同。拖动到某个数据点时，它的标签会替换 `Change` 显示在附注中，也是无障碍数值所朗读的文字。凡数据点是日期的迷你趋势图都应设置它。未设置时，附注显示位置（例如 `4 of 14`），对读者毫无意义。

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

标签的数量应与数值的数量一致。没有标签的数据点会退回为显示位置。

### 尺寸与比例

`DrawingWidth` 和 `DrawingHeight` 以 Arc 的绘图单位计，默认值为 160 和 52。它们只决定绘图区的宽高比。绘图区宽度随迷你趋势图的宽度变化，因此高度等于宽度乘以 `DrawingHeight` 再除以 `DrawingWidth`。在默认比例下，宽 320 像素的迷你趋势图高 104 像素。

| 位置 | 宽度 | `DrawingHeight` | 说明 |
|---|---|---|---|
| KPI 卡片 | 200 到 240 | 52（默认） | 主数值与附注位于线条上方的同一行 |
| 表格旁的图表面板 | 240 到 320 | 52（默认） | 为 `ShowArea` 与拖动查看留出空间 |
| 表格行 | 120 到 160 | 32 | 设置 `ShowArea="False"` 与 `IsInteractive="False"` |

在元素上设置 `Width` 以固定其尺寸，让高度按比例跟随。不要设置 `Height`：卡片缩放时，正是绘图比例防止线条被拉伸。

### 高密度表格

在行数很多的表格中关闭拖动查看。`IsInteractive="False"` 会移除每一行的 Tab 停留点与指针处理，使表格保持单一的焦点顺序。当二十行各自带有一层色块时，`ShowArea="False"` 能让线条保持安静。

<Demo name="DenseTable" />

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

行内的标签要简短。行本身已经标明了服务名，因此迷你趋势图的标签可以重复服务名，或只写指标名，例如 `Latency`。

### 没有主数值

未设置 `Value` 时，主数值隐藏，拖动到的数值及其数据点标签显示在附注中。在数值已显示于别处、而曲线仅供阅读的详情面板中，使用这种形式。

<Demo name="WithoutHeadline" />

```xml
<arc:Sparkline Label="访问量" Width="300" ItemsSource="{Binding Visits}" />
```

数据变化时，线条会从当前形状变形为新形状。同一个图表保持在屏幕上的位置，因此访问量面板的刷新会被理解为更新，而不是一个新图表。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有序列、主数值、变化量与标签，`Sparkline` 通过绑定读取它们。使用 CommunityToolkit.Mvvm 时，视图模型是带可观察属性的 `ObservableObject`，每次刷新都会赋一个新集合，而不是原地修改。

```csharp
public sealed record DailyRevenue(DateOnly Day, double Amount);

public interface IRevenueApi
{
    Task<IReadOnlyList<DailyRevenue>> GetDailyRevenueAsync(int days);
}

public partial class RevenueCardViewModel : ObservableObject
{
    private readonly IRevenueApi _api;

    public RevenueCardViewModel(IRevenueApi api) => _api = api;

    [ObservableProperty]
    private string? _headline = "$48.2K";

    [ObservableProperty]
    private string? _change = "+4.1%";

    [ObservableProperty]
    private IReadOnlyList<double> _amounts = [];

    [ObservableProperty]
    private IReadOnlyList<string> _pointLabels = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // 接口按从旧到新的顺序返回各天的数据。新列表是一次数据变化，
        // 因此每次刷新时线条只变形一次。
        var days = await _api.GetDailyRevenueAsync(14);
        Amounts = days.Select(d => d.Amount).ToArray();
        PointLabels = days.Select(d => d.Day.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
        Headline = days[^1].Amount.ToString("C1", CultureInfo.GetCultureInfo("en-US"));
    }
}
```

```xml
<arc:Sparkline Label="收入，14 天"
               Value="{Binding Headline}"
               Change="{Binding Change}"
               Tone="Success"
               Width="220"
               ItemsSource="{Binding Amounts}"
               PointLabels="{Binding PointLabels}" />
```

`Value`、`Change`、`ItemsSource` 与 `PointLabels` 为单向绑定：视图模型的值流入迷你趋势图。唯一的双向属性是 `ActiveIndex`，即读者正在查看的数据点。当屏幕需要知道读者停在哪个数据点时（例如同步更新图表旁的详情行），绑定它。

```xml
<arc:Sparkline Label="收入，14 天"
               ItemsSource="{Binding Amounts}"
               ActiveIndex="{Binding SelectedDayIndex, Mode=TwoWay}" />
```

`ActiveIndex` 静止时为 `null`，读者拖动时为所拖动数据点的索引。`ActiveIndexChanged` 事件报告同样的变化，供偏好事件的代码使用。读者松开时，它以 `null` 索引触发。

### 数据结构与性能

`ItemsSource` 接受数字或对象项。对于对象项，设置 `YBinding`（或 `YSelector`）读取数值，设置 `XBinding`（或 `XSelector`）读取日期，日期可以是 `DateTime`、`DateTimeOffset` 或 `DateOnly`。配合 `DataType` 使用 `CompiledBinding`，路径可在编译期检查，并兼容提前编译（AOT）。

- **长度**：使用卡片标题中所写的周期，例如“收入，14 天”对应 14 个每日数值。所绘制的曲线采样点数固定，因此更长的序列每个实例的绘制成本大致相同；卡片中的读者无法从长序列中获益，应先聚合。
- **平线或空序列**：一个数值画成平线。空序列在无障碍摘要中显示 `EmptyLabel`，且不绘制任何内容。当空卡片需要说明原因时（例如“暂无数据”），设置 `EmptyLabel`。
- **更新频率**：每次变化都会使线条变形，因此每分钟刷新一次读起来像更新。每秒都在变化的序列会使线条持续移动；应在数据进入控件之前先聚合。
- **大量实例**：每个迷你趋势图各自跟踪其尺寸与首次显示动画，因此数百行的表格会创建数百个绘图。应对长表格分页，并在密集行中关闭拖动查看。
- **数字格式**：数值由 `ValueFormat` 格式化，它是 .NET 格式字符串，例如 `"N0"` 或 `"0.0'%'"`，默认值为 `"#,##0.###"`。主数值 `Value` 请自行用同样的规则格式化，使主数值与拖动到的数值保持一致。需要依上下文决定文字时，`ValueFormatter` 会接收数值及其索引。

### 示例：运营概览

平台团队的概览在一行中展示三条趋势：收入、p95 延迟与支持队列。每张卡片静止时都保留主数值，因此当前数字始终可读；色调则依据对业务的好坏而定：收入上升为 `Success`，延迟上升为 `Danger`，队列增长为 `Warning`。由于只有三张卡片，它们保持可交互，读者可能需要查看某次峰值的日期。每张卡片都有各自的数据点标签，因此拖动到的数据点会读作日期。

视图模型为每张卡片提供主数值、变化量、数值与标签，XAML 只负责绑定。

```xml
<WrapPanel ItemSpacing="16" LineSpacing="16">
    <arc:Sparkline Label="收入，14 天"
                   Value="{Binding Revenue.Headline}"
                   Change="{Binding Revenue.Change}"
                   Tone="Success"
                   Width="240"
                   ItemsSource="{Binding Revenue.Amounts}"
                   PointLabels="{Binding Revenue.PointLabels}" />
    <arc:Sparkline Label="p95 延迟，14 天"
                   Value="{Binding Latency.Headline}"
                   Change="{Binding Latency.Change}"
                   Tone="Danger"
                   Width="240"
                   ItemsSource="{Binding Latency.Amounts}"
                   PointLabels="{Binding Latency.PointLabels}" />
    <arc:Sparkline Label="支持队列，14 天"
                   Value="{Binding Queue.Headline}"
                   Change="{Binding Queue.Change}"
                   Tone="Warning"
                   Width="240"
                   ItemsSource="{Binding Queue.Amounts}"
                   PointLabels="{Binding Queue.PointLabels}" />
</WrapPanel>
```

### 文案规范

`Label` 说明指标及其周期，使用句首大写的句子式写法。无障碍名称以它开头，因此“收入，14 天”比单纯的指标名更好。主数值与变化量应足够简短，使卡片能在一行内容纳。

| 不要写 | 改为 |
|---|---|
| `Label="LATENCY"` | `Label="延迟"` |
| `Label="最近两周的收入趋势"` | `Label="收入，14 天"` |
| `Value="48200"` | `Value="$48.2K"` |
| `Value="182 milliseconds"` | `Value="182 ms"` |
| `Value="1204"` | `Value="1,204"` |
| `Change="Up"` | `Change="+4.1%"` |
| `PointLabels` 使用 `2026-09-14` | `PointLabels` 使用 `Sep 14` |
| `Label=""` | 为指标命名，例如 `Churn` |

### 最佳实践

**推荐**

- 在 `Label` 中写明指标及其周期，例如“收入，14 天”。它是屏幕阅读器最先读到的内容，周期也告诉读者这条线涵盖的范围。
- 根据上升对业务是否有利来选择 `Tone`。同样的形状对收入和延迟意味着不同的事，色调告诉读者是哪一种。
- 凡数据点为日期的迷你趋势图都设置 `PointLabels`，使拖动到的数据点读作日期而不是位置。
- 在 `Change` 中保留正负号，并用与 `ValueFormat` 相同的规则格式化 `Value`，使主数值与拖动到的数值一致。
- 在表格行中关闭 `IsInteractive`，否则过多的 Tab 停留点会让表格难以遍历。

**避免**

- 为单个数值或只有一个数据点的序列使用迷你趋势图。它会画成平线，无法说明走向；这种情况应使用 `MetricCard`。
- 在长表格的每一行都开启 `ShowArea`。二十个色块会与旁边的文字争抢注意力。
- 用每秒触发一次的计时器替换 `ItemsSource`。每次变化都会使线条变形，图表永远无法稳定；应把数据聚合到卡片所显示的周期。
- 把 `Tone` 当作装饰。没有走向含义的颜色会让读者学会忽略颜色。
- 在元素上设置 `Height`。绘图比例决定高度，固定高度会在卡片缩放时拉伸线条。

### 常见问题

**拖动时为什么主数值消失了？** 只有 `Value` 为 null 时主数值才会隐藏，此时拖动到的数值及其数据点标签显示在附注中。设置了 `Value` 时，主数值显示拖动到的数值，附注显示数据点标签；读者离开绘图区或按下 Escape 后，两者都会回到 `Value` 与 `Change`。

**如何从代码中让读数停在某个数据点上？** 将 `ActiveIndex` 设为该数据点的索引，例如 `revenue.ActiveIndex = 6;`；设为 `null` 即可回到最新数值。该属性是双向的，绑定它的视图模型也会看到读者的移动。

**一个数值或没有数值时，迷你趋势图显示什么？** 一个数值会在绘图区中央画出一条平线。空序列不绘制任何内容，无障碍摘要中显示 `EmptyLabel`。当空卡片应当说明原因时（例如“暂无数据”），设置 `EmptyLabel`。

**为什么线条比我预期的短？** 高度来自绘图比例，而不是固定值。宽度为 240、使用默认 `DrawingWidth` 160 与 `DrawingHeight` 52 时，绘图区约高 78 像素。若要在相同宽度下让线条更高或更矮，请修改 `DrawingHeight`。

## API 参考

<!-- api: Sparkline -->

## 无障碍

- `Label` 为图表命名。无障碍名称合并了标签、最新数值与变化量，例如“Revenue: $48.2K. +4.1%”的形式，即“收入：$48.2K。+4.1%”。绘图本身对辅助技术隐藏，因此屏幕阅读器读取的是名称与文字。
- 主数值与变化量是纯文字，按原样朗读。
- 图表可交互时，绘图区是一个可聚焦的滑块，名称为“`Label`，逐点探索”。其数值文字会读出数据点标签与数值，例如“周四：22”；没有数据点标签时，则读出位置与数值，例如“第 2 个数据点，共 3 个：1,500”。
- 设置 `PointLabels`，使拖动到的数据点以日期朗读，而不是以它在列表中的位置朗读。
- Arc 以焦点色绘制绘图区的焦点环。即使不用窗口默认的焦点装饰，键盘焦点也清晰可见。
- 不可交互的迷你趋势图没有 Tab 停留点，也不响应指针。它的名称与文字仍可供屏幕阅读器读取。
- 色调从来不是唯一的信号。在 `Change` 中写明正负号（`+4.1%`、`-6 ms`），这样即使读者分辨不出颜色，也能读懂走向。

### 键盘

只有当 `IsInteractive` 为 `True` 时，迷你趋势图才会获得焦点。其模型与其他单序列的 Arc 图表相同：方向键逐个移动数据点，Escape 退出读数。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移入或移出绘图区 |
| `Left` 或 `Down` | 移到上一个数据点。静止时，第一次按下会落在最新数据点的前一个 |
| `Right` 或 `Up` | 移到下一个数据点。静止时，按下后仍停在最新数据点 |
| `PageUp` / `PageDown` | 向前或向后移动约六分之一的数据点（至少一个） |
| `Home` / `End` | 移到第一个或最后一个数据点 |
| `Escape` | 停止拖动，附注恢复为 `Value` 与 `Change` |

使用鼠标时，在绘图区上按下或移动，会拖动到最近的数据点；离开绘图区后回到最新数值。使用触控或触控笔时，读数停留在被触碰的数据点上，直到接触结束，然后回到最新数值。

## 动效

- 首次显示时，线条像用笔画一样绘制一次，终点圆点随线条落定而出现。绘制在迷你趋势图露出一半时开始，因此位于折叠线以下的卡片会在读者滚动到它时才绘制。
- 拖动时，光标以灵敏的弹簧在数据点之间移动，光标之后的线段变暗，终点圆点放大，以表明正在读取某个数据点。
- 主数值与附注滚动为新的文字，主数值的宽度缓动以适应新文字。
- 新数据会使线条从当前形状变形为新形状，而不是从头重绘。
- 减弱动效时整条线立即显示，光标移动时不使用弹簧。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 约 840 ms 内绘制完线条，终点圆点随线条落定而淡入，然后稳定 | 整条线立即出现；终点圆点出现时不使用弹簧 | 最终状态 |
| 拖动光标 | 光标以灵敏的弹簧移动 | 光标移动时不使用弹簧 | 立即 |
| 光标之后的线段 | 在短促的标准补间中变暗 | 同样的短补间中变暗 | 立即 |
| 拖动时的终点圆点 | 以灵敏的弹簧放大到 10 个单位 | 大小立即改变，不使用弹簧 | 立即 |
| 新数据 | 线条以平滑弹簧变形为新形状 | 直接跳到新形状 | 立即 |

## 相关组件

- `BarChart` 用于需要坐标轴的离散周期，`LineChart` 用于带有坐标轴、多个系列或图例的趋势。
- `MetricCard` 用于单个数值及其变化，没有趋势。
- `Gauge` 用于相对已知范围的数值，`BrushChart` 用于读者需要缩放的长序列。

| 组件 | 适用场景 |
|---|---|
| `BarChart` | 每个周期是独立的数值，且人们要比较各周期 |
| `LineChart` | 趋势需要数值轴、时间轴、多个系列或图例 |
| `MetricCard` | 只有一个当前数值，且其变化比路径更重要 |
| `Gauge` | 数值需要对照固定范围或阈值读取，例如配额 |
| `BrushChart` | 长序列需要在完整范围上可缩放的窗口 |
| `Treemap` | 问题是总量如何在各部分之间分配，而不是随时间如何变化 |
