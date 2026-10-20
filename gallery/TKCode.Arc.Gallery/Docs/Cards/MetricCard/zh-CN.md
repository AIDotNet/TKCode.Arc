---
title: 指标卡
summary: 为需要上下文说明的数字提供紧凑的摘要。
source-hash: 10afd81f27bc
---

## 何时使用

- 单个数字，配有标签和解释它的上下文，例如可用率、订单量或延迟。
- 实时更新的数字，数字位会朝变化的方向滚动。
- 需要在上下文旁显示变化标签（例如“+12%”）的数字。

## 何时不用

- 趋势比最新数值更重要：用 `Sparkline`。
- 不需要卡片外框的数字，例如正文中的数据：用 `AnimatedCounter`。
- 带有媒体、所有者和操作的卡片：用 `Card`。

## 用法

`Label`、`Value` 与 `Context` 是常用部分。`Change` 为可选项；不设置时变化标签隐藏。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MetricCard Label="可用率"
                    Value="99.98"
                    Decimals="2"
                    Suffix="%"
                    Context="近 30 天"
                    Change="+0.02%"
                    Width="280" />
</UserControl>
```

### 变化标签

以 “+” 开头的 `Change` 显示为上升，以 “-” 或 “−” 开头的显示为下降。可通过 `ChangeDirection` 读回方向。设置 `IsChangeGlyphVisible` 可在标签内添加趋势图标。

```xml
<arc:MetricCard Label="p95 延迟"
                Value="182"
                Suffix=" ms"
                Context="对比昨天"
                Change="−8%"
                IsChangeGlyphVisible="True"
                Width="280" />
```

### 数据绑定

将数值与文字绑定到视图模型属性。每次更新时，数字都会滚动到新值。

```xml
<arc:MetricCard Label="订单量"
                Value="{Binding OrderCount}"
                Context="{Binding OrderContext}"
                Change="{Binding OrderChange}"
                Width="280" />
```

### 趋势线

`Sparkline` 接收一组数值，在数字旁绘制一条小趋势线。卡片首次出现时绘制一次，数据变化时重新绘制。

```csharp
orders.Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 };
```

### 窄窗口

当窗口宽度为 380px 或更窄时，卡片使用更紧凑的内边距，并将变化标签移到标签下方。此行为无需任何代码。

## API 参考

<!-- api: MetricCard -->

## 无障碍

- 卡片是以 `Label` 命名的分组。其帮助文本为变化与上下文。
- 数字由计数器朗读。变化标签为纯文本，并用 “+” 或 “−” 表明方向，因此方向不依赖颜色。
- 数值变化本身不会被播报。如需播报，请在卡片或其外层容器上设置 `AutomationProperties.LiveSetting`。

## 动效

- 数字在卡片首次滚入视图时从零开始滚动，之后逐步转向每个新值。
- 标签、上下文与变化标签的文字从数字移动的方向滚入。
- 变化标签的宽度通过弹簧适应新文字，标签以缩放与淡入淡出的方式出现或消失。
- 趋势线首次显示时在 0.9s 内绘制完成，数据变化时交叉淡化。
- 减弱动效时数字直接跳到新值，文字淡入；变化标签与趋势线的变化不带动画。关闭动效时所有变化立即生效。

## 相关组件

- `AnimatedCounter`：不需要卡片外框的单独数字。
- `Sparkline`：趋势比最新数值更重要时使用。
- `Card`：围绕数字的一组内容与操作。
