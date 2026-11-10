---
title: Metric ticker
summary: 一条实时指标带，可以像股票行情一样滚动，也可以分列静止显示；每个数值逐位滚动，并显示变化了多少、朝哪个方向。
source-hash: 712593f19cc6
---

<Demo name="LiveTicker" />

## 何时使用

`MetricTicker` 用很小的空间持续显示几个实时数字。每个指标有一个名称和一个数值；数值每次变化时，数字滚动到新值，旁边以向上或向下的箭头显示
与上一次读数相比的变化，单元格以好或坏的色调闪一下。上升是不是好消息由指标决定：每秒请求数上升是好事，延迟上升则不是。指标带要么在一行中
滚动经过，像两端淡出的股票行情条，要么静止排成等宽的列，在窄屏上换行。

- **状态栏与页面标题区**：运维页面上的服务健康、队列深度和错误率。
- **实时仪表板**：图表上方的一排关键数字，随着数据流入而变化。
- **活动与销售大屏**：发布期间不断上涨的订单、收入和注册数。

## 何时不用

- **只有一个重要数字**：使用 `AnimatedCounter` 或大号统计数字，而不是指标带。
- **历史与趋势**：指标带只显示最近一次变化；要显示数值随时间的变化，使用 `Sparkline` 或 `LineChart`。
- **需要仔细比较或复制的数字**：滚动的文字难以细读；使用 `Mode="Roll"` 或 `Table`。
- **警报**：指标带只是背景信息。需要处理的事情应该放在提示消息或横幅中。

## 用法

添加带有 `Label` 和初始 `Value` 的 `MetricTickerItem` 项，之后在新读数到达时设置 `Value`。第一个值是起点，之后的每个值都算作一次变化。

```xml
<arc:MetricTicker Label="Service metrics">
    <arc:MetricTickerItem Label="Requests / s" Value="1240" />
    <arc:MetricTickerItem Label="p95 latency" Value="182" Suffix=" ms" IsHigherBetter="False" />
    <arc:MetricTickerItem Label="Error rate" Value="0.42" Decimals="2" Suffix="%" ChangeFormat="Absolute" />
</arc:MetricTicker>
```

```csharp
ticker.Items[0].Value = 1310; // rolls to 1,310 and shows +5.6% in the success color
```

`Decimals`、`Prefix` 和 `Suffix` 用于格式化数值。`ChangeFormat` 决定变化的显示方式：相对上一个值的百分比（默认）、指标自身的单位（`Absolute`），
或不显示（`None`）。`IsHigherBetter="False"` 让上升变成坏消息。`Change` 和 `PreviousValue` 报告最近一次变化；`ItemsSource` 可以从视图模型获取指标，
代替 `Items`。

### 滚动或分列

在 `Mode="Scroll"`（默认）下，指标以每秒 `Speed` 像素的速度经过。指针停在上面或焦点在其中时，这一行会停住（`PausesOnHover`）；圆形暂停按钮会让它
一直停住，直到再次按下（`IsPaused`，双向）。所有指标都放得下时，这一行不会移动。`Mode="Roll"` 把指标排成每列至少 150 px 的列，放不下时换到更多行。

<Demo name="Columns" />

### 减弱动效

在减弱动效下，滚动的指标带保持静止，并以分列方式显示指标。数值仍会变化，但不再滚动，闪烁变为短暂的淡出。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: MetricTicker -->

<!-- api: MetricTickerItem -->

## 无障碍

- 指标带是一个以 `Label` 命名的列表（"Live metrics"）。每个指标是一个列表项，名称用文字说出名称、数值和最近一次变化
  （"p95 latency, 182 ms, down 3.1%"）。用来填满滚动行的副本对屏幕阅读器隐藏，因此每个指标只读一次。
- 数值变化时不会播报，繁忙的数据流不会打断阅读。
- 暂停按钮是真正的按钮，名称为 "Pause ticker" 或 "Play ticker"；Enter 和空格可以按下它。移动的内容随时可以停止，焦点进入指标带时也会让它停住。
- 变化除了颜色之外，还带有箭头和正负号。

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 滚动行 | 以 `Speed` px/s 滑动，悬停和获得焦点时停住 | 分列，不移动 | 分列，不移动 |
| 新数值 | 数字滚动 | 立即改变 | 立即改变 |
| 变化闪烁 | 单元格的色调在 1.2 s 内淡出 | 同样的淡出 | 无 |

只有当指标带在屏幕上并且有内容需要移动时，滚动循环才会运行。

## 响应式

- 滚动时，指标带填满可用宽度，两端淡出；没有最小宽度。
- 分列时，列数随宽度而定（每列至少 150 px），多出的指标换到新行；列较窄时，名称以省略号截断。
- 触摸不会让这一行停住（没有悬停）；暂停按钮可以。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `AnimatedCounter` | 单独一个数字滚动 |
| `Sparkline` | 显示数值随时间的变化 |
| `SplitBar` | 这些数字是一个总量的各部分 |
| `LiveVisitors` | 需要一张完整的实时访客卡片 |
