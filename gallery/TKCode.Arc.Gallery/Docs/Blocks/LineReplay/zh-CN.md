---
title: Line replay
summary: 看一天的数据徐徐展开：时钟走动时折线自己画出来，你标记的事件在折线到达时出现在图上，还可以暂停、拖到任意时刻或调整速度。
source-hash: 46189eaf2cab
---

<Demo name="Preview" />

## 何时使用

`LineReplay` 适合顺序本身就是故事的数据。播放后，一个序列沿着它的一天被画出来；你标记的每个事件在画到那里时出现在线上，读者便能先后看到诱因和反应：先是发布，然后是攀升；先是故障，然后是下跌。

- **回顾一次发布或营销活动。** 看看是哪次发布、哪封邮件或哪次报道带动了数字。
- **故障之后。** 带团队回看在告警、修复和恢复之间，指标都经历了什么。
- **数据故事。** 一天、一次会话或一场比赛，事件发生的顺序正是重点。

## 何时不用

- **比较。** 当多个序列或期间需要并排阅读时，`LineChart`（或 `Small multiples`）无需等待回放就能呈现它们。
- **可编辑的注释。** 时刻来自你的数据；读者不能添加自己的时刻。
- **两个相关的指标。** 把一个对另一个作图，而不是回放一条线。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。`Points` 是位于同一时钟上的 `LineReplayPoint`，以距开始的分钟数计（示例中从午夜开始），每个点有一个值，并可带一个 `Baseline`；点不必等间距。`Moments` 是同一时钟上的 `LineReplayMoment`，带标题、可选的说明，以及决定图标的 `Kind`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LineReplay Title="Release day"
                    SeriesLabel="Release day"
                    BaselineLabel="Typical Wednesday"
                    ValueLabel="Active">
        <arc:LineReplayMoment Id="notes" Time="390" Title="Release notes published" Kind="Launch" />
        <arc:LineReplayMoment Id="queue" Time="705" Title="Sign-up queue backed up" Kind="Incident" />
    </arc:LineReplay>
</UserControl>
```

```csharp
replay.Points = samples.Select(s => new LineReplayPoint(s.Minute, s.Active, s.TypicalActive)).ToArray();
```

### 结构

- **头部**：`Title`、`Description`，以及折线（`SeriesLabel`）和虚线对照（`BaselineLabel`）的图例。
- **读数**：时间、数值（`ValueLabel`）、与对照的比值（"Versus typical Wednesday 2.6×"）以及截至目前的峰值和出现时间。播放时每个数值的宽度保持不变，所以各列不会移动。
- **图表**：绘图区上方放标注的车道、数值网格、完整画出的对照线、到播放头为止的面积与折线、每个已经过时刻的标记和竖桩、线尖上的数值，以及时间轴上的时钟。
- **传输栏**：播放按钮（Play、Pause，到末尾时为 Replay）、每个时刻对应一个刻度的拖动条，以及速度（0.5×、1×、2×、4×）。
- **关键时刻**：每个时刻一行，带时间和说明；当它的时间窗口播放完后，还会显示它的效果，即之后一小时的上升，或者对于故障，之后 20 分钟的下跌。宽度达到 1000 时，这些行在图表旁成一列；更窄时位于图表下方，分两列，小于 560 时为一列。

### 播放

`Duration` 是 1× 速度下完整回放所需的时间（默认 16 秒），`Speed` 设置节奏。`DefaultPosition` 决定首次播放前播放头停在哪里：`End`（默认）显示整条折线和所有时刻，Replay 会倒回并播放；`Start` 显示一张空图表，由首次播放画出。如果每个点都不带 `Baseline`，对照线、它的图例项和比值都会消失。

<Demo name="FromStart" />

### 从代码控制

`Play()`、`Pause()`、`Replay()` 和 `TogglePlay()` 的作用与按钮相同。`Seek(minutes)` 让播放头滑到某个时间，`SeekToMoment(moment)` 滑到某个时刻；如果正在播放，会从那里继续。`Position` 和 `IsPlaying` 报告状态。`MomentReached` 只在播放经过某个时刻时触发，拖动或跳转时不会触发，因此可以放心用于统计。`AutoPlay` 会在区块首次有 40% 进入视口时从头播放（减少动效时不会）。

<Demo name="Events" />

### 格式与文字

`TimeFormatter` 输出时间（默认 "9:40 am"），`TickFormatter` 输出坐标轴标签（"6 am"），`ValueFormatter` 输出数值（"2,316"）。按钮文字与名称（`PlayText`、`PauseText`、`ReplayText`、`PlayLabel`、`PauseLabel`、`ReplayLabel`）、读数标签（`TimeText`、`VersusFormat`、`PeakText`、`PeakFormat`）、效果（`RiseFormat`、`DipFormat`）、`MomentsTitle`、屏幕阅读器名称和各类播报都是带英文默认值的属性。

## API 参考

<!-- api: LineReplay -->

<!-- api: LineReplayMoment -->

<!-- api: LineReplayPoint -->

<!-- api: LineReplayMomentKind -->

<!-- api: LineReplayPosition -->

## 无障碍

- **Tab** 依次经过播放按钮、拖动条、速度控件和每个关键时刻。
- 在拖动条上，**Space**、**Enter** 或 **K** 播放或暂停（在末尾时从头重播）；**Left** 和 **Right** 前后移动一个点，按住 **Shift** 时移动一小时；**Page up** 和 **Page down** 移动一小时；**Home** 和 **End** 跳到开头或末尾。在速度控件中，方向键选择速度。
- 在关键时刻上按 **Enter** 或 **Space**，播放头会移到该时刻。
- 拖动条以分钟为单位报告自己是一个滑块。它朗读的数值（"9:40 am, 2,316 active"）只在播放停止时改变，让屏幕阅读器在回放期间保持安静。
- 折线显示的一切也以文字出现在读数中，因此图形不在无障碍树中。
- 每经过一个时刻、每次暂停或跳转、以及回放结束时，都会有一条礼貌播报（"Replay finished. Peak 4,894 at 10:40 am."）。
- 播放按钮的名称说明按下它会做什么：Play replay、Pause replay、Replay from the start。最近经过的时刻被标记为当前项；在那一段播放完之前，它的效果不会被读出。
- 故障除了颜色之外，还靠警告图标和自己的标题来区分。

## 动效

- **播放** 在所有动效级别下都会运行：它是观看者要求的，也可以随时停下。每一帧一个位置向前推进，折线、播放头、标签、拖动条和读数都依据它绘制。
- **倒回与跳转** 让播放头沿平滑弹簧移动（无回弹）；已画出的折线随之缩短或伸长，第一次跳转途中再跳一次会从当前位置继续。
- **到达时刻**：它的标记在淡入的同时从 40% 放大到完整大小，下方出现一条虚线竖桩，卡片上移 8px 落到绘图区上方；被替换的卡片在 120 毫秒内淡出。
- **小时标签** 在移动的时钟附近淡出（160 毫秒），而不是被压在它下面。时刻行在 160 毫秒内变色，在 240 毫秒内显示效果。
- 减少动效时回放照常播放，但倒回、跳转、标记和卡片都直接到位，`AutoPlay` 也不起作用。

## 相关组件

- `LineChart`：在一张图上绘制多个序列。
- `Small multiples`：Line charts 中的另一个区块，用于并排比较多个序列。
- `SegmentedControl` 和 `Button`：传输栏由它们组成。
