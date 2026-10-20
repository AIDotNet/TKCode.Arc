---
title: 河流图
summary: 在摆动基线上堆叠的流层，可在不同范围之间变形；可单独隔离某一层并逐周读取。
source-hash: 82df7bb364a1
---

## 何时使用

- 总量随时间的构成，例如每周各主题的支持工单。
- 以流动的层来呈现比以独立折线更容易看清的季节性或峰值。
- 三到八层。把较小的层合并为“其他”系列后再传入。

## 何时不用

- 需要精确数值或比较各系列：用 `LineChart`。
- 少量周期或离散类别：用 `BarChart`。
- 包含负值的数据：各层从基线堆叠，河流形状无法正确读取。请用 `LineChart`。

## 用法

每个 `ChartSeries` 为一层。`ItemsSource` 保存共享的 x 位置，例如每周的起始日期。悬停或聚焦某一层即可将其单独隔离。

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

### 范围切换

同时替换 `ItemsSource` 与各系列的数值。每一层都会从屏幕上的当前形状融合到新形状，长度不同的范围同样会变形过渡。

```csharp
void Range(int weeks)
{
    var start = ChartsADemoData.Today.AddDays(-7 * (weeks - 1));
    chart.ItemsSource = Enumerable.Range(0, weeks).Select(w => start.AddDays(7 * w)).ToArray();
    for (var i = 0; i < series.Length; i++)
    {
        series[i].Values = ChartsADemoData.Daily(weeks, 60 - i * 9, 40, weeks * 7 + i, i % 2 == 0 ? 0.8 : -0.3);
    }
}
```

### 偏移

`Offset` 决定各层的堆叠方式：`Wiggle`（默认）使各层的斜率最小化，形成流动的河流；`Silhouette` 将堆叠围绕一条平直的中线居中；`Zero` 则从平直的基线按顺序堆叠各层。

```xml
<arc:Streamgraph Label="主题" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

### 图例、直接标签与隐藏层

图例与直接标签用于命名各层。空间足够时，直接标签会绘制在某一层最粗的位置。`HiddenSeries` 为双向绑定，因此视图模型可以保存被隐藏的键。

```xml
<arc:Streamgraph Label="主题"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### 读取某一周

`ActiveIndexChanged` 报告指针或键盘所在的周，`ActiveSeriesKey` 指明被隔离的层。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

## API 参考

<!-- api: Streamgraph -->

## 无障碍

- `Label` 为图表命名。绘图区是可聚焦的分组：获得焦点时读取最新一周，每条读数都会播报该周、被隔离层的数值与总量。
- 左、右方向键沿时间轴移动，上、下方向键切换被隔离的层。PageUp 与 PageDown 大约移动范围的六分之一，Home 与 End 分别移到第一周与最后一周，Escape 清除读数。
- 图例条目为切换开关。悬停或聚焦某个条目即可隔离其所属的层。
- 隐藏的数据表列出每一周的每一层。
- 各层直接在绘图区上标注名称，因此不会只靠明暗来区分。相邻的层之间以 2 px 的表面颜色间隙分隔。

## 动效

- 首次显示时，河流从中线处的一根细线沿中心向外膨胀，使用不过冲的长弹簧。
- 新范围或新数据集会在共享网格上融合每一层，因此各层保持其顺序与速度。
- 被隐藏的层会收缩至消失，其余层围绕它重新堆叠。
- 十字准线与提示在弹簧上滑动，隔离某一层时其余层变暗。
- 减弱动效时立即显示最终状态，并保留十字准线与提示但不移动它们。关闭动效时直接到达最终状态。

## 相关组件

- `LineChart`：需要精确比较各系列数值时。
- `BarChart`：少量离散周期。
- `Ridgeline`：比较多个组的分布，而非其随时间的总量。
- `DonutChart`：一个总量中各部分所占的比例。
