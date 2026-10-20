---
title: 环形图
summary: 一个环形图，扇区在数据集之间平滑变形，当前数值滚动显示在中心。
source-hash: 29c9433344a7
---

## 何时使用

- 由两到六个部分组成一个整体，例如流量来源或支出类别。
- 中心显示总计，并随读者指向某个部分而变化。
- 切换数据集（例如本月与本季度）时，扇区应变形为新的大小。

## 何时不用

- 部分较多或需要精确比较：用 `BarChart`。
- 单个数值与已知上限对比：用 `Gauge`；配额场景用 `UsageMeter`。
- 展示随时间的变化：用 `LineChart`。

## 用法

每个 `ChartPoint` 对应一个扇区。为每个数据点设置稳定的 `Key`，这样数据变化时扇区才能保持各自的身份。值为零或负数的数据会被忽略。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DonutChart Label="按来源统计访问量"
                    Unit="次访问"
                    TotalLabel="总计"
                    OtherLabel="其他"
                    ItemsSource="{Binding Sources}" />
</UserControl>
```

### 数据集

替换 `ItemsSource` 即可切换数据集。扇区保持原有顺序，并从当前角度平滑移动到新的角度，因此扇区不会被重新创建。

```csharp
month.Click += (_, _) => chart.ItemsSource = s_month;
quarter.Click += (_, _) => chart.ItemsSource = s_quarter;
```

### 尺寸与分组

`Diameter` 和 `RingThickness` 决定环的尺寸。占比低于 `GroupBelow`（占总量的比例，默认 0.04）的部分，以及超出 `MaxSegments`（默认 6，包含“其他”）的部分，会合并到“其他”扇区中。

```xml
<arc:DonutChart Label="存储空间"
                Diameter="160"
                RingThickness="18"
                GroupBelow="0.05"
                MaxSegments="5"
                ItemsSource="{Binding Storage}" />
```

### 图例与隐藏扇区

默认情况下，图例可以隐藏和显示扇区。若只需要环形本身，设置 `ShowLegend="False"`。`HiddenKeys` 列出被隐藏的键，且为双向绑定，视图模型可以保存该状态。

```xml
<arc:DonutChart Label="按来源统计访问量"
                HiddenKeys="{Binding HiddenKeys}"
                ShowLegend="True"
                ItemsSource="{Binding Sources}" />
```

### 固定扇区

设置 `LegendAction="Select"` 后，点击会固定扇区而不是隐藏它。`ActiveKey` 保存被固定的键，同样为双向绑定。

```xml
<arc:DonutChart Label="按类别统计支出"
                LegendAction="Select"
                ValueFormat="$#,##0"
                ActiveKey="{Binding PinnedCategory}"
                ItemsSource="{Binding Spend}" />
```

```csharp
select.ActiveKeyChanged += (_, _) => selected.Text = select.ActiveKey is { } key
    ? $"已固定：{key}"
    : "点击一行或一个扇区即可固定。";
```

## API 参考

<!-- api: DonutChart -->

## 无障碍

- `Label` 是图表的名称。每一行图例都是一个切换按钮，其名称包含数值、占比；对于“其他”，还包含其所含的各个部分。
- 图例是键盘操作的路径。Down 与 Right 移到下一行，Up 与 Left 移到上一行，Home 与 End 分别移到第一行和最后一行。Enter 或 Space 显示或隐藏扇区；在 Select 模式下则为固定或取消固定。
- Escape 清除已固定的扇区。
- 可见扇区发生变化时，会以新的总计进行礼貌播报。
- 没有图例时，环形本身可以获得焦点：方向键朗读每个扇区，Enter 固定该扇区。
- 环形与中心读数对辅助技术隐藏。隐藏的数据表会列出每个部分。
- 焦点环仅在键盘焦点时显示。

## 动效

- 首次显示时，所有扇区同时从顶部扫出，每段的终点稍后跟随。
- 新数据集或隐藏某个扇区时，起始角与终止角会从扇区当前的位置开始变形，因此环保持其运动轨迹与顺序。
- 激活的扇区沿中线向外移动并略微加粗，其余扇区变暗。指针在扇区之间的间隙处仍保持悬停状态。
- 中心读数滚动到激活的扇区；总计与各占比数字滚动到新的数值。
- 减弱动效时，扇区、外移效果与文字会立即显示为最终状态。关闭动效时的表现与减弱动效相同。

## 相关组件

- 部分较多或需要精确比较时用 `BarChart`。
- `WaffleChart` 用于以格子数量（每格代表一个百分点）表示占比。
- 部分存在嵌套且需要逐级下钻时用 `Treemap`。
- `Gauge` 用于单个数值与已知范围的对比。
