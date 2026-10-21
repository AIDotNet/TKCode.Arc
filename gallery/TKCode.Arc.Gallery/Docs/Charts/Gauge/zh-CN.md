---
title: 仪表盘
summary: 在已知范围内展示一个数值。
source-hash: 9936da3b7dd8
---

<Demo name="Thresholds" />

## 何时使用

- 对照已知范围的单个数值，例如配额中的磁盘用量或健康评分。
- 带有状态的数值，由阈值区间命名状态，例如健康、即将占满和严重。
- 适合放入仪表盘卡片、需要一个百分比和一行简短说明的读数。

## 何时不用

- 正在运行或即将完成的任务进度：用 `Progress`。
- 用户正在消耗、有上限的计划或配额：用 `UsageMeter`。
- 随时间变化的数值：用 `LineChart` 或 `Sparkline`。

## 用法

`Value` 为读数。`Minimum` 与 `Maximum` 设定范围，默认为 0 到 100。`Detail` 会在读数下方增加一行说明，例如以绝对单位表示的数量。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Gauge Label="磁盘用量"
               Value="72"
               Detail="360 / 500 GB">
        <arc:Gauge.Thresholds>
            <arc:GaugeThreshold From="0" Tone="Success" Label="健康" />
            <arc:GaugeThreshold From="60" Tone="Warning" Label="即将占满" />
            <arc:GaugeThreshold From="85" Tone="Danger" Label="严重" />
        </arc:Gauge.Thresholds>
    </arc:Gauge>
</UserControl>
```

### 阈值

阈值从 `From` 开始，一直持续到下一个阈值。屏幕上的数值所达到的最高区间决定颜色，并显示该区间的 `Label`。区间由屏幕上的数值选定，因此数值跨越区间时，标签随之变化。

```csharp
disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "健康"));
disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "即将占满"));
disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "严重"));
```

### 更新读数

同时设置 `Value` 与 `Detail`，使数字与其说明保持一致。

```csharp
disk.Value = Math.Round(used / 5.0);
disk.Detail = $"已用 {used} / 500 GB";
```

### 自定义范围

`Minimum` 与 `Maximum` 可将任意范围映射到圆环上。数字按该范围的百分比显示。

<Demo name="CustomRange" />

### 没有阈值时的色调

没有阈值时，圆环使用 `Tone`：`Accent`、`Success`、`Warning` 或 `Danger`。

<Demo name="Tones" />

## API 参考

<!-- api: Gauge -->

<!-- api: GaugeThreshold -->

## 无障碍

- `Label` 为仪表盘命名。数值以只读范围的形式公开，并包含最小值与最大值。
- 数值文字包含当前区间的标签，因此状态会与数字一同被读出。
- 状态由阈值标签以文字说明，颜色从来不是唯一的状态提示。
- 仪表盘为只读，不定义键盘交互。
- 圆环与带动画的读数对辅助技术隐藏。

## 动效

- 仪表盘进入视图一半时，圆环以无过冲的平滑弹簧一次性填充到其数值。
- 新数值会将圆环移动到新的长度，同时百分比数字计数到新的数字。
- 数值跨越阈值时，颜色与区间标签在该时刻切换，跟随计数变化。
- 减弱动效时，圆环与数字立即到达目标值；关闭动效的表现与减弱动效相同。

## 相关组件

- `Progress` 表示任务的完成进度。
- `UsageMeter` 表示用户在一段时间内消耗的配额。
- `DonutChart` 表示整体的各个部分，而非对照范围的单个数值。
- `MetricCard` 表示带有变化与趋势的单个数值。
