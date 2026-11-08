---
title: 竞速条形图
summary: 随时间变化的排名：数值上涨时条形彼此超越，时间轴可以播放、暂停或拖动浏览各个时期。
source-hash: c411f827ec0e
---

<Demo name="TopRegions" />

## 何时使用

`RaceBarChart` 讲述的是"谁在什么时候领先"。每个时期是一份排名，图表从一份排名过渡到下一份，读者能看到一方超过另一方的那一刻，而静态的合计表格看不出这一点。它最适合用于展示：报告的开场、年度回顾，或让人按下播放后观看的页面。

- **销售或推广回顾中按地区的累计合计**：想知道哪个地区在什么时候拉开了差距。
- **媒体仪表盘上按节目或频道的受众**：每周数字起起落落，排名不断变化。
- **年度回顾中按产品的市场份额**：并高亮页面所讨论的那一款产品。
- **工程复盘中按团队的贡献**：由分组提供图例。
- **公开的排行榜**：访客可以拖动浏览各个时期。

## 何时不用

- **某一时刻的单份排名**：用 `BarChart`，无需等待就能比较数值。
- **需要查找的精确数值**：用 `Table` 或 `DataGrid`。条形在移动，只有停下时数值才便于阅读。
- **每个参与方的趋势**：用 `LineChart`，所有时期同时留在屏幕上。
- **整体的构成随时间变化**：用 `Streamgraph`，合计始终可见。
- **超过约二十个时期且没有明确的故事**：过长的竞速会变成屏保。请汇总时期，或改用 `LineChart`。

## 用法

给图表设置 `Items`（每个条形一项）和 `Frames`（每个时期一帧，按顺序）。帧中的数值按 id 匹配到项，帧里没有提到的 id 计为零。图表在每一帧中为各项排名，显示前 `BarCount` 名，并在每个条形上绘制名称和数值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RaceBarChart Label="各城镇的步道报名人数" IsAutoPlay="False">
        <arc:RaceBarChart.Items>
            <arc:RaceBarItem Id="elmford" Label="Elmford" />
            <arc:RaceBarItem Id="stonely" Label="Stonely" />
        </arc:RaceBarChart.Items>
        <arc:RaceBarChart.Frames>
            <arc:RaceBarFrame Period="春季">
                <arc:RaceBarValue Id="elmford" Value="120" />
                <arc:RaceBarValue Id="stonely" Value="90" />
            </arc:RaceBarFrame>
            <arc:RaceBarFrame Period="夏季">
                <arc:RaceBarValue Id="elmford" Value="180" />
                <arc:RaceBarValue Id="stonely" Value="240" />
            </arc:RaceBarFrame>
        </arc:RaceBarChart.Frames>
    </arc:RaceBarChart>
</UserControl>
```

在代码中，帧可以由字典构造，这也是数据来自查询时的常见形式。

```csharp
chart.Items = towns.Select(t => new RaceBarItem(t.Id, t.Name)).ToList();
chart.Frames = seasons.Select(s => new RaceBarFrame(s.Name, s.SignUpsByTown)).ToList();
```

`RaceBarChart` 派生自 `TemplatedControl`。一个自绘表面绘制条形、坐标轴和图例，大号时期标签、播放按钮和时间轴围绕在它周围。`Label` 为辅助技术命名图表。

### 结构

1. **图例**：设置 `Groups` 后出现在坐标轴上方。
2. **数值轴**：条形上方的网格线和标签。刻度数量随宽度变化，约每 84 px 一个刻度。
3. **条形**：高 28 px，间距 6 px，每个条形都带名称和数值。
4. **时期标签**：右下角的大号文字，位于条形之后。
5. **播放按钮**：36 px。显示播放、暂停，在最后一个时期显示重播。
6. **时间轴**：以时期为单位的滑块，触控目标 36 px，两侧是第一个和最后一个时期的名称。

### 起伏的数值

帧不必是累计值。使用原始值时每个时期彼此独立，因此参与方既能上升也能下降。`IsLooping` 在最后一个时期之后重新开始，`BarCount` 决定屏幕上有多少个条形：排名低于它的项滑出，达到它的项上升并淡入。

<Demo name="RisingAndFalling" />

### 控制时间轴

`FrameIndex` 是双向的，每次变化都会平滑经过中间的数值，因此你自己的按钮或滑块可以驱动图表。由页面而不是图表决定何时开始时，设置 `IsAutoPlay="False"`。`IsPlaying` 同样是双向的，`Play`、`Pause` 和 `Replay` 可在代码中完成同样的操作。`FrameChanged` 报告每个时期及其领先者，`PlayingChanged` 报告播放的开始和停止。

<Demo name="ControlledTimeline" />

### 分组与高亮

`Groups` 赋予条形颜色以含义。前四个分组使用 `Series1` 到 `Series4` 并出现在图例中。后面的分组和没有分组的项绘制为中性色条形。没有分组时所有条形都使用 `Series1`。`HighlightId` 强调页面所讨论的项的名称和数值，但不改变其颜色。

```xml
<arc:RaceBarChart Label="各地区的安装量" HighlightId="larkfield" BarCount="10" />
```

### 格式与时长

`ValueFormatter` 格式化条形和提示框。默认是紧凑数字，例如 1.2K。`TickFormatter` 格式化坐标轴，未设置时回退到 `ValueFormatter`。`FrameDuration` 设置播放时一个时期的时长。

```csharp
chart.ValueFormatter = value => value.ToString("N0");
chart.TickFormatter = value => ArcFormatting.Compact(value);
chart.FrameDuration = TimeSpan.FromSeconds(1);
```

### 读取条形

悬停在条形上、用手指按下，或使用上下方向键。其他条形退后，提示框显示名称、时期、数值和名次。读数随该项移动，如果该项跌出屏幕上的条形，读数会清除。

### 数据与性能

相等的数值保持 `Items` 中的顺序，因此并列不会闪烁，向回拖动也得到相同的顺序。非有限数值计为零，负值绘制空条形，id 重复的第二项会被丢弃。可观察的 `Items`、`Frames` 或 `Groups` 列表的变化会被跟踪；帧内部的变化不会，请改为赋值新的帧。

一个循环驱动全部内容，在图表暂停且静止时停止，离开屏幕时暂停。文字只布局一次，仅在显示的数字变化时重建，因此数百个项也能保持流畅；只绘制屏幕上的条形。

### 内容指南

- **Label**：度量和范围，例如"各地区的屋顶太阳能安装量（累计）"。
- **时期**：简短且长度一致，例如"3 月"或"2026 年第 2 季度"。它会以大号绘制。
- **项标签**：简短的名称。过长的名称以省略号截断。

| 不要写 | 应写 |
|---|---|
| 数据 | 各城市的月度乘客量 |
| 时期 3 | 3 月 |
| 分组 A | 北部海岸 |

### 最佳实践

**建议**

- 让 `BarCount` 接近人们能跟上的参与方数量，通常为 5 到 12。
- 页面围绕某一参与方时使用 `HighlightId`。
- 图表只是页面众多内容之一时，从 `IsAutoPlay="False"` 开始。

**避免**

- 把颜色用于 `Groups` 以外的用途。色相必须有含义。
- 过短的 `FrameDuration`。低于约 500 ms 时没有人能读出数值。
- 依靠动画来陈述结论。请在图表旁用文字说明。

### 常见问题

**为什么图表不会自己开始？** 它在第一次滚动进入视口时开始，并且只在完整动效下开始。减弱动效时它会等待播放按钮。

**为什么图表停在了两个时期之间？** 不会这样：暂停时会完成向下一时期的移动，单步和拖动都落在完整的时期上。

## API 参考

<!-- api: RaceBarChart -->

## 无障碍

- 绘图区是由 `Label` 命名的可聚焦分组。它的帮助文本是时期和领先者，或正在读取的条形。
- 时间轴是滑块，其值文本形如 "Mar, period 3 of 12"，播放按钮会说明自己的操作。
- 单步时播报时期和领先者。读取条形时播报名称、名次和数值。播放期间不播报，以免屏幕阅读器被刷屏；暂停时会再次播报当前状态。
- 每个条形都显示名称和数值，因此色相不是唯一的线索。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在绘图区、播放按钮和时间轴之间移动 |
| `Space` / `Enter` | 播放或暂停。在最后一个时期从头重播 |
| `Left` / `Right` | 平滑地前进或后退一个时期。单步会暂停播放 |
| `Up` / `Down` | 按名次在条形之间移动读数并显示提示框 |
| `Home` / `End` | 转到第一个或最后一个时期 |
| `Escape` | 清除读数 |

## 动效

- 数值在时期之间插值，每一行都在自己的弹簧上移动，因此超越看起来是一次连续的滑动。获得新目标的行会保留速度。
- 进入前列的条形从下方上升并淡入。离开的条形滑出并淡出。
- 时间轴的滑块沿临界阻尼弹簧跟随。坐标轴刻度随比例滑动，步长变化时交叉淡化。时期标签滚动切换并带有短暂的模糊。
- 离开屏幕时播放暂停，回到屏幕后继续。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 设置 `IsAutoPlay` 时开始播放 | 等待播放按钮 | 与减弱相同 |
| 时期变化 | 数值平滑过渡，行在弹簧上滑动 | 立即就位，没有插值 | 与减弱相同 |
| 播放中 | 连续 | 每个 `FrameDuration` 一步 | 与减弱相同 |
| 时期标签 | 滚动切换并带短暂模糊 | 短暂交叉淡化 | 立即替换 |

## 响应式行为

- `RaceBarChart` 按自身宽度判断。低于 400 px 时名称移入条形内部，条形放不下名称时移到条形末端之后；数值跟在名称后面。
- 低于 400 px 时时期标签缩小一档，时间轴两侧的第一个和最后一个时期名称隐藏。
- 刻度数量随绘图区宽度变化，从 2 到 8 个。
- 时间轴整条 36 px 的区域都响应手指，因此手机上也能拖动。按下条形可读取它。

## 相关组件

- `BarChart`：某一时刻的单份排名。
- `LineChart`：跟随每个参与方随时间的变化。
- `Streamgraph`：合计的各层随时间的变化。

| 组件 | 何时选择 |
|---|---|
| `BarChart` | 只有一个时期需要比较 |
| `LineChart` | 所有时期都应同时可见 |
| `Streamgraph` | 合计与其组成部分同样重要 |
| `DataGrid` | 需要查找精确数值 |
