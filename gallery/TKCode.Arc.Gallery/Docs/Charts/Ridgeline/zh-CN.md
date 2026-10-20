---
title: 山脊图
summary: 重叠的分布，每组一条山脊：悬停可抬起山脊并读取其四分位数，切换数据集时每条曲线都会变形。
source-hash: 99493fcb54f7
---

## 何时使用

- 并排比较几组分布的形状，例如每月的每日最高气温或各地区的延迟。
- 单个汇总数值会掩盖的离散程度、偏斜与第二个峰。
- 最多约二十组，每组都有足够的观测值（至少 20 个）以绘制平滑曲线。

## 何时不用

- 每组只有一个汇总数值：用 `BarChart`。
- 每组只有少量数据点或需要精确数值：用 `BarChart` 或表格。
- 单一数值随时间的变化：用 `LineChart`。

## 用法

每个 `RidgelineSeries` 对应一组数据，包含 `Id`、`Label` 与原始的 `Values`。图表会为每组计算密度，并按系列顺序自上而下绘制山脊。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Ridgeline Label="苏黎世每日最高气温"
                   Unit="°C"
                   DomainMinimum="-5"
                   DomainMaximum="35"
                   ItemsSource="{Binding Months}" />
</UserControl>
```

### 数据集

替换 `ItemsSource` 即可切换数据集。每条曲线都会从当前形状变形到新形状，而山脊的顺序保持不变。

```csharp
zurich.Click += (_, _) => chart.ItemsSource = Zurich;
lisbon.Click += (_, _) => chart.ItemsSource = Lisbon;
```

### 项目与数值格式

在代码中创建的分组可通过 `Items` 添加。`ValueFormatter` 用于格式化坐标轴与读数上的数值。

```csharp
var latency = new Ridgeline
{
    Label = "各地区响应时间",
    ValueFormatter = v => $"{v:0} ms",
    IsTinted = false,
    RowHeight = 36,
    Overlap = 1.8
};
latency.Items.Add(new RidgelineSeries("eu", "欧洲", europeValues));
latency.Items.Add(new RidgelineSeries("us", "美洲", americasValues));
```

### 重叠与平滑度

`Overlap` 设定山脊向上一行延伸的距离，以行高的倍数表示。`RowHeight` 设定每行的高度。`Bandwidth` 以数值的单位设定平滑程度。默认情况下，每组使用 Silverman 规则。

```xml
<arc:Ridgeline Label="延迟" Overlap="2.4" RowHeight="30" Bandwidth="4" ItemsSource="{Binding Regions}" />
```

### 色调与定义域

`IsTinted` 会按每条山脊的中位数着色，颜色从浅色渐变到第一个系列的颜色，并在坐标轴下方显示色阶。`DomainMinimum` 与 `DomainMaximum` 固定数值轴，使其不会随数据集变化而移动。

```xml
<arc:Ridgeline Label="延迟" IsTinted="False" DomainMinimum="0" DomainMaximum="300" ItemsSource="{Binding Regions}" />
```

### 读取山脊

`ActiveId` 为被抬起的山脊，且为双向绑定。每次变化都会触发 `ActiveIdChanged`。

```csharp
chart.ActiveIdChanged += (_, e) => readout.Text = e.Series is null
    ? string.Empty
    : $"{e.Series.Label}: 中位数 {Stats(e.Series).Median:0.0}";
```

## API 参考

<!-- api: Ridgeline -->

<!-- api: RidgelineSeries -->

## 无障碍

- `Label` 为图表命名。摘要会列出每一组。绘图区是可聚焦的分组。
- 上、下方向键抬起前一条或后一条山脊。左、右方向键以一个坐标轴步长移动读数光标。Home 与 End 分别抬起第一条与最后一条山脊，Escape 放下山脊并清除读数。
- 触摸按下会抬起手指所在的山脊，并读取该处的数值。
- 礼貌的实时区域会播报被抬起山脊的中位数、中间一半的范围、取值区间与数据个数。
- 隐藏的数据表列出每组的中位数、四分位数、极值与数量。每一行都有文字标签，因此并非只能通过色调读取中位数。

## 动效

- 数据集或定义域变化时，每条曲线都会在共享网格上重新采样，并对每个点进行插值。山脊以相同的平滑弹簧膨胀、滑动并分裂。
- 首次显示时，山脊从顶部开始逐行滚入。
- 被抬起的山脊会以副本的形式绘制在最上层。它上升 6 px，中间一半会加上阴影，并标出中位数。提示跟随指针移动，并保持在图表之内。
- 减弱动效时直接绘制最终状态。关闭动效的表现与减弱动效相同。

## 相关组件

- `BarChart`：每组只有一个汇总数值时使用。
- `LineChart`：单一数值随时间的变化。
- `Streamgraph`：多个层的总量随时间的变化。
- `BrushChart`：单一的高密度时间序列。
