---
title: Split bar
summary: 一根条形按各部分在总量中的占比分段，下方图例列出名称、数值和百分比；数据变化时，各段以弹簧动画移到新的占比。
source-hash: ff0ca4153563
---

<Demo name="Storage" />

## 何时使用

`SplitBar` 用一行回答“这个总量由什么组成”。它绘制一根水平条形，切分成若干段，每段的宽度等于它在总量中的占比，段与段之间留有小间隙；
下方的图例写出每一段的名称、数值和百分比。总量可以是各部分之和，也可以是固定的量，例如磁盘容量或预算，此时条形剩余的部分保持空白，
图例为它命名（"Free"、"Remaining"）。当某个数值变化、新增一段或移除一段时，每一段都会以弹簧动画移到新的宽度；动画进行中收到新数据时，
弹簧会保持当前速度，所以实时数据不会让条形跳动。

- **用量与配额**：按文件类型划分的存储空间、按套餐划分的席位、按模型划分的 token，并显示剩余空间。
- **预算与成本**：按类别划分的支出，与月度预算对照。
- **测试与任务结果**：通过、跳过和失败，按含义着色。
- **实时构成**：按来源划分的流量或按地区划分的负载，每隔几秒更新一次。

## 何时不用

- **在多个总量之间比较各部分**：`BarChart` 中的堆叠条形可以把多个总量并排显示。
- **很多细小的部分**：超过六七段后，细条和图例都会难以阅读。把末尾的小项合并为 "Other"。
- **需要精确读取每个数值**：当用户更关心数字而不是比例时，使用 `Table` 或列表。
- **朝一个目标的进度**：`Progress` 进度条比分段条更直接地说明“完成了多少”。

## 用法

添加带有 `Label` 和 `Value` 的 `SplitBarSegment` 项。没有设置 `Total` 时，整根条形就是各部分之和。

```xml
<arc:SplitBar Title="Storage" Total="256" ValueDecimals="1" ValueSuffix=" GB" RemainderLabel="Free">
    <arc:SplitBarSegment Label="Photos" Value="82.4" />
    <arc:SplitBarSegment Label="Apps" Value="46.1" />
    <arc:SplitBarSegment Label="Documents" Value="21.7" />
</arc:SplitBar>
```

标题栏显示 `Title` 和滚动变化的合计（"168.2 GB of 256 GB"）；`IsSumVisible="False"` 隐藏合计，`IsLegendVisible="False"` 隐藏图例。
数值按 `ValuePrefix`、`ValueDecimals` 和 `ValueSuffix` 格式化；`FormatValue(value)` 可为你自己的标签生成相同的文本。

### 实时数据

把 `ItemsSource` 绑定到一组分段，并直接修改它们的 `Value`：分段显示在屏幕上时，条形会监听每一段的变化。新增的分段从无到有地展开，
移除的分段在原位置收缩消失。

<Demo name="LiveShares" />

```csharp
foreach (var segment in segments)
{
    segment.Value = Math.Round(segment.Value * 1.1);
}
```

### 高亮与触发

指向某一段或它的图例项时，这一段会突出显示，其余段变暗；`HighlightedIndex` 报告当前高亮的段，也可以绑定。点击，或在条形获得焦点时按
Enter，会引发 `SegmentInvoked`，并带上 `Segment`、它的 `Index` 和 `Share`。

### 颜色

没有设置 `Brush` 的分段会依次使用图表系列颜色（系列 1 到 4，之后是它们更浅的几轮）。当颜色带有含义时，为每一段设置 `Brush`，
如下面的结果条所示。

<Demo name="Compact" />

## API 参考

<!-- api: SplitBar -->

<!-- api: SplitBarSegment -->

<!-- api: SplitBarSegmentEventArgs -->

## 无障碍

- 控件是一个以 `Title` 命名的分组（没有标题时为 "Split bar"）。条形本身是一个 Tab 停靠点，它的无障碍名称列出每一段
  （"Photos, 82.4 GB, 32 percent; ..."），有段高亮时只说高亮的那一段。
- 左右键（或上下键）移动高亮，Home 和 End 跳到第一段和最后一段，Enter 或空格触发高亮的段，Escape 清除高亮。键盘焦点环绘制在高亮的段周围。
- 图例用文字重复了每个名称和数值，因此颜色从来不是区分各段的唯一方式。

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新的占比 | 每一段以弹簧动画移到新宽度（约 0.45 s，略有回弹，被打断时保持速度） | 直接跳到 | 直接跳到 |
| 新增 / 移除一段 | 从无到有地展开 / 在原位置收缩消失 | 出现 / 消失 | 出现 / 消失 |
| 高亮 | 其余各段在 160 ms 内变暗 | 立即变暗 | 立即变暗 |
| 图例数值 | 数字滚动 | 立即改变 | 立即改变 |

第一次的数据立即显示；只有之后的变化才有动画。只有占比在变化时，帧循环才会运行。

## 响应式

- 条形填满可用宽度；放不下时，图例项换到更多行，过长的名称以省略号截断。
- 标题栏把合计放在标题右侧；给控件至少 240 px 的宽度，两者才能同时放下。
- 在触摸屏上没有悬停高亮；轻点会触发手指下方的那一段。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `BarChart` | 比较多个总量或随时间变化的系列 |
| `Progress` | 显示一项任务完成了多少 |
| `MetricTicker` | 显示多个实时数字，而不是一个总量的各部分 |
