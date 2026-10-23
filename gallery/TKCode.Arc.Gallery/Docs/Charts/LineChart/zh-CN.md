---
title: 折线图
summary: 多系列折线图，带有平滑滑动的十字准线、图例开关，以及在不同范围之间变形的路径。
source-hash: e7da8d76673e
---

<Demo name="Ranges" />

## 何时使用

`LineChart` 展示一到三个随时间变化的指标，并让读者在任意时间点进行比较。它是分析页面的主要趋势图：位于一排 KPI 指标卡下方，并跟随范围选择器（7 天、30 天和 90 天）。每个系列都是一条稳定的线，因此切换范围时是改变同一组线条的形状，而不是换成一张新图。

- **分析概览中选定范围内的注册数或收入**。7 天、30 天和 90 天范围共享同一组系列，因此人们切换范围时，线条会从当前形状变形过渡。
- **账单或增长仪表板中的本期与上一期对比**。虚线的第二个系列显示本月高于还是低于上月，无需第二张图表。
- **故障页面中随时间变化的延迟或错误率**。使用 `Unit="ms"` 和 `Curve="Linear"` 的单一系列，能让尖峰保持真实高度，提示则显示带单位的精确数值。
- **金融科技运营控制台中每天的退款或支付失败数**。`EmptyLabel` 解释冷清的范围，`IsLoading` 则在获取下一个范围时保留屏幕上的上一次形状。
- **配额页面中的用量与目标对比**。虚线的目标线与实际用量并排显示，图例让读者可以隐藏其中任意一条。

## 何时不用

- **与单个数字并排的紧凑趋势**，例如 KPI 指标卡或表格单元格：用 `Sparkline`，它没有坐标轴、图例或提示。
- **星期、地区或套餐等离散类别**：用 `BarChart`。柱形用于比较彼此独立的数值；折线则暗示两个数据点之间的空间有意义。
- **部分与整体的关系**，例如按套餐划分的收入占比：用 `DonutChart`。当各部分是等值单位的计数时，用 `WaffleChart`。
- **超过四个左右的指标**：调色板只有四种系列颜色，其余指标只能使用彼此难以区分的中性色阶。当各层相加等于总量时，用 `Streamgraph`。
- **需要缩放的长时间历史**：用 `BrushChart`，它将详情视图与可框选的概览配对。
- **一年内的每日活动**，例如日历网格中每天的登录数：用 `ActivityHeatmap`。

## 用法

每个 `ChartSeries` 都有稳定的 `Key`，因此数据变化时线条仍保有其身份。设置 `Label` 可为辅助技术命名图表。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LineChart Label="注册数" CategoryLabel="日期" ItemsSource="{Binding Days}">
        <arc:LineChart.Series>
            <arc:ChartSeries Key="current" Name="本期" Values="{Binding Current}" />
            <arc:ChartSeries Key="previous" Name="上一期" Values="{Binding Previous}" IsDashed="True" />
        </arc:LineChart.Series>
    </arc:LineChart>
</UserControl>
```

`LineChart` 派生自 `ChartBase`，而 `ChartBase` 派生自 Avalonia 的 `TemplatedControl`。模板在绘图区上方放置图例，绘图区自行绘制每条线、面积、网格线、标签和提示。过渡动画无法作用于该绘图内部的坐标，因此图表的动效由代码驱动。

### 结构

1. **图例**（`ShowLegend`）：每个系列一个切换胶囊，带颜色色块，位于绘图区上方 16 px。它向边距延伸 8 px，使悬停胶囊与绘图区边缘对齐。
2. **绘图区**（`PlotHeight`）：默认高 220 px，宽度与容器相同。最上方的刻度位于顶部下方 12 px。
3. **网格线**（`ShowGrid`）：位于数值刻度处的水平线，使用边框颜色；底线为 1 px，使用较强的边框颜色。
4. **线条**：2 px、圆头描边，使用系列颜色。虚线系列细 0.25 px，使它在相邻的实线旁看起来不会更粗。
5. **面积填充**：线条下方 9% 的淡色。默认只有第一个系列有面积（`HasArea` 可改变此行为）。
6. **数值轴**（`ShowYAxis`）：右侧 48 px 的槽位中显示刻度标签，默认使用紧凑格式（1.2K、2.4K）。
7. **日期轴**（`ShowXAxis`）：绘图区下方一行 26 px。标签之间保持间距以免重叠；`XAxisFormatter` 可返回 null 来去掉某个标签。
8. **十字准线与数据点**：活动点处的竖线，以及每条可见线上 4 px 的圆点，外描边为表面颜色。
9. **提示**：日期，然后每条可见系列一行，显示其数值与单位。提示位于十字准线侧面 14 px 处，并会在即将超出绘图区前翻转。

### 范围切换

范围变化时，应同时替换数据行与系列数值，使每个系列在新范围中都有与数据行一一对应的值。随后每条线都会从屏幕上的形状变形到新形状，数值轴也会以弹簧效果过渡到新范围。

```csharp
void ShowRange(SignupDay[] rows)
{
    chart.ItemsSource = rows.Select(r => r.Date).ToArray();
    chart.Series[0].Values = rows.Select(r => (double)r.Current).ToArray();
    chart.Series[1].Values = rows.Select(r => (double)r.Previous).ToArray();
}
```

顶部的示例对 7、30 和 90 天展示同样的两个系列。坐标轴会按屏幕上的范围重新缩放，而线条保持其键不变。

### 单系列与单位

`ChartPoint` 项的列表无需额外的系列。`Unit` 显示在提示中，`Curve` 可将默认的平滑曲线切换为直线段。`Smooth` 永远不会超出数据范围；当读者可能把两天之间的曲线当作真实数值时，使用 `Linear`。

<Demo name="SingleSeries" />

```xml
<arc:LineChart Label="p95 延迟" Unit="ms" Curve="Linear" PlotHeight="180" ItemsSource="{Binding Latency}" />
```

### 图例与隐藏系列

存在多个系列时，图例会为每个系列显示一个切换开关。`ShowLegend` 可强制显示或隐藏图例，`HiddenSeries` 保存被隐藏的键。

```xml
<arc:LineChart Label="注册数"
               ItemsSource="{Binding Days}"
               ShowLegend="True"
               HiddenSeries="{Binding HiddenKeys}" />
```

隐藏某个系列时，它会淡出并压平到基线，数值轴则随剩余的系列重新缩放。`HiddenSeries` 是双向的：图例切换会把新列表写回并引发 `HiddenSeriesChanged`；代码中的变更则会显示或隐藏对应的线。所有系列都被隐藏时，绘图区会显示“选择要显示的系列”。

### 加载与空状态

`IsLoading` 会使线条变暗，并在没有数据点时显示骨架屏。`EmptyLabel` 用于解释空的范围。

<Demo name="Empty" />

```xml
<arc:LineChart Label="退款" EmptyLabel="此范围内没有退款" IsLoading="{Binding IsBusy}" />
```

默认的空状态文字是“此范围内没有数据”。请写明原因，让读者知道是图表有问题还是这段时间确实冷清。`IsLoading` 为 true 且没有数据点时，显示骨架屏；有数据点时，当前形状保持在屏幕上并轻微呼吸，表示新数据正在到来。

### 读取数值

十字准线移动时以及松开时，都会触发 `ActiveIndexChanged`。可用它在标题中显示数值。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
    ? "悬停或使用方向键读取数值。"
    : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
```

`ActiveIndex` 保存同一数据点，并且是双向的，因此代码可以把读数指向某一天，例如某次故障发生的那一天。`Values` 只列出可见的系列，因此隐藏的系列永远不会出现在读数中。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型负责数据行、范围与隐藏的系列。每次切换范围时，视图模型都会替换整个数组，图表则从之前的数据行变形到新的数据行。

```csharp
public sealed record SignupDay(string Date, int Current, int Previous);

public interface ISignupService
{
    Task<SignupDay[]> GetDailyAsync(int days);
}

public partial class SignupsViewModel : ObservableObject
{
    private readonly ISignupService _signups;

    public SignupsViewModel(ISignupService signups) => _signups = signups;

    [ObservableProperty]
    private SignupDay[] _days = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [ObservableProperty]
    private int? _activeIndex;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoadRangeAsync(int days)
    {
        IsBusy = true;
        try
        {
            // 一个新数组就是一次数据变更：图表接收一次数据行，并把线条变形到新数据。
            Days = await _signups.GetDailyAsync(days);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

```xml
<arc:LineChart Label="注册数"
               CategoryLabel="日期"
               Height="260"
               ItemsSource="{Binding Days}"
               XBinding="{Binding Date}"
               HiddenSeries="{Binding HiddenKeys, Mode=TwoWay}"
               ActiveIndex="{Binding ActiveIndex, Mode=TwoWay}"
               IsLoading="{Binding IsBusy}">
    <arc:LineChart.Series>
        <arc:ChartSeries Key="current" Name="本期" ValueBinding="{Binding Current}" />
        <arc:ChartSeries Key="previous"
                         Name="上一期"
                         IsDashed="True"
                         ValueBinding="{Binding Previous}" />
    </arc:LineChart.Series>
</arc:LineChart>
```

- `XBinding` 从每条记录中读取类别。没有它时，图表会读取项本身，这对 `string` 和 `DateTime` 项有效，但对记录不适用。
- `HiddenSeries` 与 `ActiveIndex` 是双向的，因为图例与十字准线会写入它们。`ItemsSource`、`IsLoading` 以及数值绑定都是单向的。
- 为简洁起见，示例使用普通的 `Binding`。在编译绑定的应用中，需要在视图上设置 `x:DataType`，并为每个绑定设置 `DataType`。

### 数据结构与性能

`ItemsSource` 每个数据点一项，最早的在前。每个系列都从同一索引读取数值，因此所有系列每行都需要一个值，并且顺序一致。缺失的值按零计算，因此没有数据的那一天会被画成下降到零。请为范围内的每一天提供真实数值。

用 `ValueFormat` 格式化数字，它是按 en-US 惯例读取的 .NET 格式字符串：计数用 `N0`，金额用 `C0`，百分比用 `0.0'%'`。默认会为千位分组，并最多显示一位小数，因此 2400 显示为“2,400”。如果货币取决于读者的区域设置，请设置 `ValueFormatter`。提示中的日期默认为“Tue, Sep 15”，坐标轴上为“Sep 15”；如需其他标签，请设置 `XLabelFormatter` 或 `XAxisFormatter`。

### 数值轴

数值轴从零开始；当数据跌至零以下时，则从数据的最小值开始，并结束于最大值之上的下一个刻度。当业务问题需要时，可以改变这一设置：

- `YMinimum` 与 `YMaximum` 固定任一端。对于百分比，两端都固定（0 到 100），使坐标轴不会在切换范围时跳动。
- `TickFormatter` 只格式化刻度标签，例如在代码中写 `chart.TickFormatter = v => $"{v:0}%";`。
- 当数值已在卡片其他位置显示（例如上方的 KPI 指标卡）时，`ShowYAxis="False"` 可去掉槽位。
- 距零较远的 `YMinimum` 会让微小的变化看起来很大。只在零不是有意义的基线时使用，例如分数，并在卡片中说明这一点。

```xml
<arc:LineChart Label="结账转化率"
               YMinimum="0"
               YMaximum="100"
               ValueFormat="0.0'%'" />
```

### 示例：带虚线对比的注册概览

Acme Analytics 的增长仪表板显示最近十天的注册数，并与上一个月同样的十天进行对比。本期为实线，对比为虚线，图例让读者可以隐藏对比线。标题说明周期、总数与变化幅度，因此即使不悬停提示，也能读懂要点。

![带实线本期、虚线上一期与图例的注册概览卡片](images/SignupsOverview.png)

```xml
<Border xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
        xmlns:collections="clr-namespace:Avalonia.Collections;assembly=Avalonia.Base"
        xmlns:sys="clr-namespace:System;assembly=System.Runtime"
        Width="680"
        Padding="24"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Signups" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Sep 1 to Sep 10, 2026. 1,411 new workspaces, up 28% on the previous period."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:LineChart Label="Signups"
                       CategoryLabel="Date"
                       PlotHeight="200">
            <arc:LineChart.ItemsSource>
                <collections:AvaloniaList x:TypeArguments="sys:String">
                    <sys:String>Sep 1</sys:String>
                    <sys:String>Sep 2</sys:String>
                    <sys:String>Sep 3</sys:String>
                    <sys:String>Sep 4</sys:String>
                    <sys:String>Sep 5</sys:String>
                    <sys:String>Sep 6</sys:String>
                    <sys:String>Sep 7</sys:String>
                    <sys:String>Sep 8</sys:String>
                    <sys:String>Sep 9</sys:String>
                    <sys:String>Sep 10</sys:String>
                </collections:AvaloniaList>
            </arc:LineChart.ItemsSource>
            <arc:LineChart.Series>
                <arc:ChartSeries Key="current" Name="This period">
                    <arc:ChartSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>120</sys:Double>
                            <sys:Double>138</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>152</sys:Double>
                            <sys:Double>149</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>104</sys:Double>
                            <sys:Double>160</sys:Double>
                            <sys:Double>171</sys:Double>
                            <sys:Double>168</sys:Double>
                        </collections:AvaloniaList>
                    </arc:ChartSeries.Values>
                </arc:ChartSeries>
                <arc:ChartSeries Key="previous" Name="Previous period" IsDashed="True">
                    <arc:ChartSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>96</sys:Double>
                            <sys:Double>104</sys:Double>
                            <sys:Double>112</sys:Double>
                            <sys:Double>109</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>92</sys:Double>
                            <sys:Double>88</sys:Double>
                            <sys:Double>124</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>127</sys:Double>
                        </collections:AvaloniaList>
                    </arc:ChartSeries.Values>
                </arc:ChartSeries>
            </arc:LineChart.Series>
        </arc:LineChart>
    </StackPanel>
</Border>
```

### 文案规范

- **标签**：度量指标；如果坐标轴上没有单位，则附上单位：“注册数”“p95 延迟（ms）”。标签会为辅助技术和数据表命名图表，因此必须能独立理解。
- **系列名称**：描述周期或对比关系的简短名词短语：“本期”“上一期”“目标”。
- **空状态文字**：说明范围为空的原因，并指明范围：“此范围内没有退款”。
- **卡片标题与说明**：用文字写出总数或变化，数字使用阿拉伯数字，例如“1,411 个新工作区”。

| 不要写 | 改为 |
|---|---|
| 数据 | 每日注册数 |
| 上一期 | 上一期 |
| 无数据 | 此范围内没有退款 |
| 系列 1 | 本期 |
| 收入图表 | 收入，2026 年 9 月 1 日至 9 月 10 日 |

### 最佳实践

**推荐**

- 每个指标使用一个系列，并以其所代表的周期或含义命名。图例与提示都会重复名称，因此它必须脱离卡片标题也能读懂。
- 将 `CategoryLabel` 设为坐标轴的单位（“日期”“周”）。数据表将其作为第一列，因此屏幕阅读器用户会先听到坐标轴的名称。
- 范围变化时，同时替换整个 `ItemsSource` 与系列数值，使线条在两个一致的状态之间变形。
- 延迟与计数使用 `Linear`；当形状比精确数据点更重要时，趋势图使用默认的 `Smooth`。

**避免**

- 对从零开始的指标（例如注册数或收入）设置非零的 `YMinimum`。被截断的坐标轴会让 5% 的变化看起来像崩塌。
- 超过三到四个系列。再多时颜色难以区分，提示也会变成一长串列表。
- 每秒刷新数据。线条、坐标轴与提示同时移动，会掩盖你想展示的变化。
- 用零作为无数据那一天的占位。缺失值按零计算，因此线条会下跌，并被读作真实的下降。
- 对按整数单位计数的数据（例如退款）使用 `Smooth`。两天之间的曲线暗示了从未发生过的数值。

### 常见问题

**为什么数值轴从零开始？** 默认基线为零；当数据跌至零以下时，则为数据的最小值。设置 `YMinimum` 可以改变它，请按数值轴一节所述有意识地使用。

**为什么我的图表没有图例？** 只有一个系列时，图例默认隐藏。设置 `ShowLegend="True"` 可显示它，或添加第二个系列，图例就会自动出现。

**为什么只有第一条线有填充？** `HasArea` 默认为 null，因此只有第一个系列会有面积。在另一个系列上设置 `HasArea="True"` 可为其也加上填充，设置 `HasArea="False"` 则可去掉填充。

## API 参考

<!-- api: LineChart -->

## 无障碍

- `Label` 为图表命名。绘图区会在隐藏的摘要中公开日期范围和每个系列的最新数值，隐藏的数据表则列出每个数据点。
- 绘图区是可聚焦的滑块。方向键每次移动一个数据点，PageUp 与 PageDown 大约移动范围的六分之一，Home 与 End 分别移到第一个与最后一个数据点，Escape 释放十字准线。
- 活动数据点的读数会说明日期以及每个可见系列的数值和单位，例如“周五，9 月 4 日：本期 240 位用户，上一期 160 位用户”。键盘聚焦时，读数从最新的数据点开始。
- 图例条目是切换按钮。每个系列既由名称标识，也由线型标识，而不仅依靠颜色。
- 加载状态会以“正在加载”礼貌播报。线条与提示对辅助技术隐藏，以避免重复朗读。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在图例开关与绘图区之间移动。用键盘进入绘图区时会选中最新的数据点 |
| `Left` / `Down` | 移到上一个数据点。处于静止状态时，第一次按下会落在最新的数据点上 |
| `Right` / `Up` | 移到下一个数据点。处于静止状态时，第一次按下会落在最新的数据点上 |
| `PageUp` / `PageDown` | 移动约六分之一的数据点；静止状态下按 PageUp 会落在最新的数据点上 |
| `Home` / `End` | 移到第一个或最后一个数据点 |
| `Escape` | 释放十字准线并隐藏提示 |
| `Enter` / `Space`（在图例开关上） | 显示或隐藏该系列 |

离开绘图区会释放十字准线。鼠标悬停在绘图区上时读取最近的数据点。触摸在按下并拖动时读取，手指抬起时释放。

## 动效

- 首次显示时，线条从左到右绘制，面积随线条一同显现。
- 新范围会在共享的 x 位置上重新采样线条，并从当前形状变形过渡。网格线随数值轴滑动。
- 十字准线在数据点之间滑动，提示由其自身的弹簧跟随，并在边缘处翻转。
- 加载中的线条会脉动，直到数据到达。图表离开视口时脉动暂停。
- 减弱动效时，跳过绘制、变形与滑动过程，并停止加载脉动。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 线条在约 840 ms 内从左侧绘出，面积随之显现 | 线条淡入，不做绘制过程 | 立即完成 |
| 新范围 | 线条从当前形状变形；数值轴以弹簧效果过渡，网格线滑动 | 立即完成 | 立即完成 |
| 十字准线移动 | 以弹簧效果在数据点之间滑动 | 直接跳到数据点 | 直接跳到数据点 |
| 加载中 | 线条呼吸式变暗 | 脉动停止 | 脉动停止 |

## 相关组件

- `Sparkline`：与数值并排的紧凑趋势。
- `BarChart`：在离散的日、周或月之间比较单一指标。
- `BrushChart`：需要缩放的高密度时间序列。
- `Streamgraph`：需要将多个系列当作堆叠层来阅读时。

| 组件 | 适用场景 |
|---|---|
| `Sparkline` | 趋势与单个数字并排，没有坐标轴或图例 |
| `BarChart` | 类别是离散的日、周或月，且每个数值相互独立 |
| `DonutChart` | 数据是一个整体的几个部分 |
| `BrushChart` | 历史较长，读者需要缩放到某一时段 |
| `Streamgraph` | 多个层应被当作堆叠总量的各部分来阅读 |
| `ActivityHeatmap` | 数据是按天的网格，例如全年的活动 |
