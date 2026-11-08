---
title: 实时数据流
summary: 随读数到达而滚动的实时折线图，指针悬停时静止以便读取某个点，并标记被判定为异常的读数。
source-hash: 0178c9de6d7f
---

<Demo name="ServiceLatency" />

## 何时使用

`RealtimeStream` 展示正在被测量的数值。最新读数从右侧进入，折线向左滚动，读者随时可以让它停下来读取某个点。适合有人正盯着这个数字、想知道它走向何方的场景。

- **服务健康**：运维屏上的请求延迟、错误率或吞吐量，并在折线上标出告警系统标记的发布和重试高峰。
- **队列与工作进程**：后台控制台里任务队列当前的深度，配合固定坐标轴，让折线的高度全天含义不变。
- **传感器与设备**：工业或设施仪表盘里的温度、压力或土壤湿度，大约每秒一个读数。
- **进行中的会话**：交易、音频或直播工具里会话期间的码率、价格或观看人数。

## 何时不用

- **供人回看的历史数据**，例如上个月的收入：用 `LineChart`，它一次绘制整个范围。
- **需要缩放和范围选择的长序列**：用 `BrushChart`。
- **表格行或卡片里的小趋势**：用 `Sparkline`。数据流需要足够的宽度来滚动。
- **没有趋势的单个当前数字**：用 `Gauge` 或 `AnimatedCounter`。
- **发现异常**：图表只标记你标注的内容，自己不判断读数。请在自己的系统里做检测，并给它标注的读数设置 `Anomaly`。

## 用法

给图表设置 `Label`、`Unit` 和读数。`Append` 是最省资源的喂数方式：它接收时间、数值和可选的异常文字，自身不分配内存。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream x:Name="Latency"
                        Label="Checkout latency"
                        Unit=" ms" />
</UserControl>
```

```csharp
// 每个读数调用一次，在 UI 线程上。
Latency.Append(reading.Time, reading.Milliseconds);

// 告警系统标注了这个读数：文字成为它的标记。
Latency.Append(reading.Time, reading.Milliseconds, "Retry burst");
```

### 结构

- **页头**：`Label`、以滚动数字显示的最新值和 `Unit`。
- **状态按钮**：视图跟随数据时显示圆点和 `LiveLabel`，静止时显示暂停图标和 `PausedLabel`。状态由形状和文字表达，而不只靠颜色。按下时切换 `IsPaused`。
- **绘图区**：折线及其下方的浅色面积、水平网格线和数值标签。一个圆点跟随折线前端。
- **时间标签**：绘图区下方随折线滚动的整点时刻。绘图区较窄时数量会减少。
- **标记**：每个被标注的读数上有一个菱形，文字放在小标签里。菱形和文字让它不依赖颜色也能读懂。
- **提示框**：指针或键盘所在读数的时间和数值，以及它的异常文字。

### 从集合喂数

`Points` 接受任何 `RealtimeStreamPoint` 列表。`ObservableCollection` 会被监听：新增项会被追加，其他内容不会重新读取。数据源为控制自身内存而移除最旧的项不会影响图表，图表保留所需部分并自行裁剪其余部分。替换或清空集合会重新读取。

<Demo name="QueueDepth" />

### 时间窗口与间隔

`WindowDuration` 是绘图区显示的时长。`Interval` 是读数通常到达的频率，视图比最新读数落后这么多。这样折线总有前进的余地，会平滑地生长到下一个读数，而不是在它到达时跳变。请把 `Interval` 设得接近数据的实际节奏。如果它短得多，折线会停在最新读数处，等下一个读数到达。

### 数值轴

默认情况下，坐标轴跟随视图内的读数，留有少量余地并取整，然后缓动到每个新范围。设置 `Minimum` 和 `Maximum` 可固定坐标轴，也可以只设一个来固定那一端。固定坐标轴适合有已知上下限的数值，例如百分比或队列容量。

### 异常

给 `RealtimeStreamPoint` 设置 `Anomaly`，或把文字传给 `Append`。折线走到该读数时标记弹出。屏幕阅读器在每个被标注的读数到达时播报一次。初始 `Points` 中的读数属于历史：会被标记但不会播报。

### 暂停

当 `IsPaused` 为 true、指针停留在绘图区上（`IsPauseOnHover`）、触摸期间以及键盘正在读取点时，视图保持静止。期间到达的读数会被保留，页头保持最新。恢复时折线滑回实时位置。`IsHeld` 表示视图是否因任何原因静止，`PausedChanged` 报告每次变化及其 `Reason`。

当界面有自己的控件时，绑定 `IsPaused`。关闭 `IsPauseOnHover` 后图表在指针下也继续移动，适合大屏展示或光标常驻其上的图表。

<Demo name="ControlledPause" />

### 格式

`ValueFormatter` 在所有地方替换默认的数字格式，此时页头显示它的文字而不是滚动数字，因为只有纯数字才能滚动。`TimeFormatter` 收到的时间采用读数所带的时区偏移；默认格式是 `HH:mm:ss`。

```csharp
Latency.ValueFormatter = value => $"{value / 1000:0.00} s";
Latency.TimeFormatter = time => time.ToString("mm:ss");
```

### 绑定到视图模型

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream Label="Render queue"
                        Unit=" jobs"
                        Points="{Binding Readings}"
                        IsPaused="{Binding IsReviewing}"
                        Minimum="0"
                        Maximum="500" />
</UserControl>
```

```csharp
public ObservableCollection<RealtimeStreamPoint> Readings { get; } = new();

// 先切换到 UI 线程：图表在变化发生时读取它。
Dispatcher.UIThread.Post(() => Readings.Add(new RealtimeStreamPoint(time, depth)));
```

### 数据形状与性能

- 读数按时间升序到达。比最新读数更早的读数以及不是有限数的值会被忽略。
- 只绘制窗口内的读数，通过二分查找定位。更早的读数在超过两个窗口后被丢弃，因此无尽的数据流占用的内存保持稳定。视图静止期间，图表最多保留 200,000 个读数。
- 滚动的一帧只是移动上次读数到达时构建好的几何图形，自身不分配内存，也不重新解析颜色。图表移出屏幕或没有内容需要移动时，循环会停止。
- 传入时间和数值的 `Append` 不会分配点对象。数据很快时优先用它，而不是集合。

### 内容准则

- 在 `Label` 里写明测量什么、在哪里：写“Checkout API, p95 latency”，而不是“Latency”。
- 标记文字控制在两三个词，例如“Cache flush”。提示框和屏幕阅读器会重复它。
- 单位按它跟在数字后面应有的样子写。它直接接在数值后面，所以 `" jobs"` 带空格，`"%"` 不带。

### 最佳实践

- 只标注需要有人处理的情况。满是标记的折线就失去了意义。
- 高度本身有含义时固定坐标轴，形状更重要时让它跟随数据。
- 通过 `Points` 加载近期历史，让绘图区首次显示时不是空的。
- 需要仔细读数的场景保持 `IsPauseOnHover` 开启。移动的目标很难指中。

### 常见问题

**为什么折线停在最新读数之前？** 这是 `Interval` 的作用：视图落后于最新读数，以便持续移动到下一个读数。减弱动效时不滚动，所以会立即显示最新读数。

**悬停会设置 `IsPaused` 吗？** 不会。`IsPaused` 是你的请求，保持为 false。要了解视图状态，请读取 `IsHeld` 或 `PausedChanged`。

**能绘制多个序列吗？** 每个图表一条线。需要比较多个实时数值时，请堆叠多个图表。

## API 参考

<!-- api: RealtimeStream -->

<!-- api: RealtimeStreamPoint -->

## 无障碍

- 绘图区是一个可聚焦的分组，以 `Label` 命名，名称中包含按键说明。绘制内容本身对辅助技术隐藏，其数据通过帮助文本朗读。
- 页头的数值是真实文本：无论数字如何滚动，屏幕阅读器都能读出标签和当前值。
- 状态按钮的名称包含状态和按下后的效果，例如“Live. Pause updates”。
- 被标注的读数在到达时以礼貌方式播报一次。键盘移动时播报各点的读数。
- 状态从不只靠颜色表达：按钮上是圆点或暂停图标加文字，标记上是菱形加文字。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 把焦点移入或移出绘图区。移入时视图静止并读取最新可见点，移出时解除。没有读数时跳过绘图区 |
| `Left` / `Right` | 读取上一个或下一个点 |
| `PageUp` / `PageDown` | 向前或向后移动十个点 |
| `Home` / `End` | 读取最早或最新的可见点 |
| `Space` / `Enter` | 暂停或恢复 |
| `Escape` | 清除读取并恢复 |

## 动效

- 折线按屏幕刷新率滚动，前端在读数之间连续移动，而不是逐步跳变。
- 数值轴缓动到新范围。
- 折线走到标记时，标记带一次过冲弹出。
- 恢复时滑回实时位置，页头的数字滚动。
- 减弱动效时停止滚动，绘图区只在读数到达时重绘。关闭动效时相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新读数 | 折线继续向它滚动 | 立即绘制在右边缘 | 与减弱相同 |
| 坐标轴范围变化 | 缓动到新范围 | 直接跳变 | 与减弱相同 |
| 被标注的读数 | 标记带一次过冲弹出 | 标记直接出现 | 与减弱相同 |
| 恢复 | 滑回实时位置 | 直接跳到实时位置 | 与减弱相同 |

## 响应式

- `RealtimeStream` 填满宽度，调整大小时重绘。`PlotHeight` 保持你设置的值。
- 时间标签会减少以保持间距。30 秒的窗口在宽绘图区每 5 秒一个标签，在手机上每 10 秒或更久一个。
- 宽度小于 420 px 时，数值列变窄并显示更少的标签。
- 与较新标记重叠的标记文字会被省略，提示框仍会显示它。
- 触摸时按住并拖动可读取并保持静止，抬起手指后恢复。悬停只对鼠标或触控笔生效。

## 相关组件

- `LineChart`：供人回看的完整序列。
- `BrushChart`：需要缩放和范围选择的密集序列。
- `Sparkline`：行或卡片里的微型趋势。
- `Gauge`：当前值相对其上下限的情况。

| 组件 | 何时选择 |
|---|---|
| `LineChart` | 数据已完整，需要比较多个序列 |
| `BrushChart` | 一条长序列需要缩放和范围选择 |
| `Sparkline` | 趋势只是其他内容旁边的小细节 |
| `Gauge` | 只关心当前值相对上下限的情况 |
| `AnimatedCounter` | 只关心当前数字，且没有上下限 |
