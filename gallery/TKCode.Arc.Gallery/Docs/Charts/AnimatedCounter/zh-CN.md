---
title: 动画计数器
summary: 让变化的总数具有清晰的动感。
source-hash: aaf33cd863f2
---

## 何时使用

- 单独出现、会变化的数字，例如总额、价格或实时计数。
- 首屏统计数字，滚动进入视野时应从零开始计数。
- 仪表板中的总数，需要动效表明它已变化，但不需要卡片包裹。

## 何时不用

- 需要卡片、标签和变化情况的数字：使用 `MetricCard`，它内部即使用本计数器。
- 变化的是文字而非数字：使用 `TextMorph`。
- 作为图表读数的数字：图表的主要数值本身已会滚动显示。

## 用法

`Value` 接收原始数字，而不是格式化后的字符串。`Label` 为数字上方的名称，`Prefix` 与 `Suffix` 则在数字前后添加文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnimatedCounter Label="收入"
                         Prefix="$"
                         Value="{Binding Revenue}" />
</UserControl>
```

### 小数与后缀

`Decimals` 固定小数位数，使数字在变化时保持相同的宽度。

```xml
<arc:AnimatedCounter Label="转化率" Value="{Binding Conversion}" Decimals="2" Suffix="%" />
```

### 更新数值

将 `Value` 设为新的总数。只有发生变化的数位列会滚动，滚动方向与数字的变动方向一致。

```csharp
revenue.Value += random.Next(100, 4000);
rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
```

### 进入视野时显示

`AnimateOnView` 会在计数器首次有 60% 可见时，让每一位数字从零开始向上滚动。

```xml
<arc:AnimatedCounter Label="首次查看时滚动显示" Value="{Binding Visitors}" AnimateOnView="True" />
```

### 区域性

`CultureName` 设置千位分隔符所用的区域性。默认值为 `en-US`，因此服务端与客户端的输出保持一致。

```xml
<arc:AnimatedCounter Label="用户" Value="{Binding Users}" CultureName="de-DE" />
```

## API 参考

<!-- api: AnimatedCounter -->

## 无障碍

- 计数器不可获得焦点，也不定义键盘交互。
- 完整的格式化文字（包括前缀、分隔符、小数与后缀）即为计数器的名称，因此读屏软件能一次读出整个数字。
- 滚动的数位对辅助技术隐藏。计数器本身不会播报变化。若要播报，请在外层元素上设置 `AutomationProperties.LiveSetting="Polite"`。

## 动效

- 每一位数字列都会沿整个数字的变动方向转动，并在 9 与 0 之间循环。
- 位数变化时，新增的数位列与分隔符会滑入或滑出，宽度平滑过渡以适应。相邻内容会随之轻微移动。
- 使用 `AnimateOnView` 时，计数器有 60% 可见后开始滚动，各位数字以轻微的错落依次开始。
- 减弱动效时直接替换为新值，不做滚动。关闭动效时直接跳到最终状态。

## 相关组件

- `MetricCard`：卡片内的数字，带有标签与变化情况。
- `Sparkline`：带趋势的数字。
- `BarChart` 与 `LineChart` 会更新各自的主要数值，因此无需使用本计数器。
- `TextMorph`：用于变化的文字而非数字。
