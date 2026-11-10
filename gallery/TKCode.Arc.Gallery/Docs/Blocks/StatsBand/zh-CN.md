---
title: 数据带
summary: 进入视野时向上计数的醒目数字，每个都配有一幅佐证它的小图，悬停时显示一行背景说明，可平铺或排成细线网格。
source-hash: a76708197aa5
---

<Demo name="Preview" />

## 何时使用

几个具体的数字比一段话更能支撑你的说法。`StatsBand` 把三到四个数字并排放在首屏下方或两个区块之间，并为每个数字配上一幅佐证它的小图。

- **用数字支撑说法的营销页**：可用性、速度、使用产品的人数。
- **关于页或投资者页**：醒目的数字，各附一条简短说明。

## 何时不用

- **带趋势的产品实时指标**：请使用 `MetricCard` 或 `MetricsDashboard`。
- **超过四个数字**：分成两条数据带，或使用表格。
- **你无法担保的数字**：取整且没有来源的数字读起来像编造的。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:StatsBand Layout="Divided" Title="Quick and steady">
        <arc:StatsBand.Stats>
            <arc:StatsBandStat Value="38" Suffix="ms" Label="Median response" Detail="At the edge" />
            <arc:StatsBandStat Value="99.98" Decimals="2" Suffix="%" Label="Uptime" Detail="Over 90 days" />
            <arc:StatsBandStat Value="2340000" Notation="Compact" Decimals="1" Label="Deploys" Detail="This year" />
        </arc:StatsBand.Stats>
    </arc:StatsBand>
</UserControl>
```

`StatsBand` 派生自 `TemplatedControl`。`Stats` 为空时显示示例数字，上线前请替换。`Layout` 可以是 `Plain`（数字浮在留白上）或 `Divided`（上下有线的细线网格）。

### 单个数据

| 属性 | 作用 |
|---|---|
| `Value`、`Decimals` | 最终数字及其小数位数 |
| `Prefix`、`Suffix` | 数字前后的文字，绘制得更小更淡，单位放在这里：%、ms、+、/5 |
| `Notation` | `Compact` 写成 2.3M，并在该单位内计数，从 0.0M 到 2.3M |
| `Label`、`Detail` | 它衡量什么，以及一行简短的说明 |
| `Context` | 悬停或聚焦时替换 `Detail` 的一行文字，例如 “Up from 41%” |
| `Visual` | 一幅小图，见下文 |

`Locale` 决定数字分隔符（默认 `en-US`），紧凑单位始终是 K、M、B 和 T。

<Demo name="OwnNumbers" />

### 小图

每个 `Visual` 都是可选的；但只要有一个数据带图，所有数据都会保留这一行，让数字对齐。

| 类型 | 数据 | 呈现为 |
|---|---|---|
| `StatsBandTrend` | `Values`，最旧的在前 | 迷你折线图，最后一个值上有一个点 |
| `StatsBandUptime` | `Days`，单位是百分比 | 每天一根柱。有停机的日子明显更短并着色 |
| `StatsBandDistribution` | `Bins`、`Maximum`、`Marker` | 直方图，标记值以下的柱着色 |
| `StatsBandMap` | `Points`，经度和纬度 | 点阵世界地图，每个地点点亮一个点，并吸附到最近的陆地 |

悬停或聚焦某个数据时，它的小图采用强调色，其他小图保持中性。

<Demo name="Numbers" />

### 最佳实践

**建议**

- 使用具体的数字：99.98% 比“几乎一直在线”更有说服力。
- 选择能佐证数字的小图，以及一行用于比较的背景说明。
- 标签控制在几个词内，让各列对齐。

**避免**

- 没有理由地混用有无背景说明的数据。大家从第一个数据学会悬停。
- 一个页面放多条数据带。

## API 参考

<!-- api: StatsBand -->

<!-- api: StatsBandStat -->

<!-- api: StatsBandTrend -->

<!-- api: StatsBandUptime -->

<!-- api: StatsBandDistribution -->

<!-- api: StatsBandMap -->

## 无障碍

- 数据带是一个以 `Title` 命名的区域，没有标题时以 `AccessibleLabel`（“Key numbers”）命名。
- 每个数据以其最终值、标签、说明和背景说明命名。计数中的数字不会被播报。
- 带 `Context` 的数据可以获得焦点，因此键盘或点按也能显示这行文字。小图属于装饰。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在带背景说明的数据之间移动，焦点会显示这行文字 |

## 动效

- 数据带第一次有 40% 进入视野时，每个数字在 `CountDuration`（1.6 秒）内以强缓出从零开始计数，每个数据上升 8 px 并淡入。两者都间隔 90 ms。
- 数字等宽，最终值预留好空间，因此计数时不会有内容移动。
- 数字基本落定后，小图开始绘制：线条和地图在 1.1 秒内从左侧擦入，柱形以 420 ms 的扫动从基线长出，趋势线的末端圆点最后落定。
- 在 `Divided` 中，上下两条线在 900 ms 内画过去，数据之间的细线在 700 ms 内长出。
- 悬停时，说明行在 240 ms 内交叉淡化为背景说明行，并有 4 px 的位移。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 入场 | 计数、上升并绘制 | 立即显示最终值 | 立即显示最终值 |
| 悬停 | 带位移的交叉淡化，混入强调色 | 文字立即切换，240 ms 混入强调色 | 全部立即 |

调用 `Replay` 可重新播放入场动画。

## 响应式

- `StatsBand` 跟随自身宽度，而不是窗口宽度。数字在 40 到 52 px 之间缩放。
- 一行最多四个数据。宽度不超过 800 px（`:stacked`）时排成两列，三个数据时排成一列，因此不会有落单的数据。
- 宽度不超过 520 px（`:phone`）时每行一个数据，小图位于数字旁边。
- 细线跟随网格：相邻数据之间是竖线，行之间是横线。
- 触控：点按会聚焦数据，从而显示背景说明行。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `StatsBand` | 营销页上几个有佐证的数字 |
| `MetricCard` | 产品内一个带背景的数字 |
| `AnimatedCounter` | 一个会变化的总数 |
| `MetricsDashboard` | 带图表的实时指标 |
