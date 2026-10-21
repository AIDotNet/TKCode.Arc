---
title: 数据流
summary: 事件从来源出发，经队列与批量转换后进入数据仓库和实时仪表板。
source-hash: bfddb2b7fcc9
---

<Demo name="EventPipeline" />

## 何时使用

- 在落地页或产品文档中说明事件如何从生产者流向存储与仪表板。
- 引导页和空状态需要展示一条管道、但没有真实数据时。
- 展示过滤步骤如何在存储前移除一部分流量。

## 何时不用

- 绘制真实数值随时间的变化：用 `LineChart` 或图表分类中的其他图表。
- 固定步骤序列且需要标示当前位置：用 `Stepper`。
- 页面上有大量动画插图：每个插图都会逐帧重绘画布，因此同一时间只保留少数几个在播放。

## 用法

插图加载后自动开始播放。图表下方的说明文字随故事推进而变化，播放按钮可暂停播放。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataFlow Label="事件管道" />
</UserControl>
```

### 吞吐量与过滤

`EventsPerSecond` 设置数据源发出事件的速度（1 到 60，默认 18）。`FilterRatio` 设置转换环节移除的事件占比（0 到 0.5，默认 0.14）。被过滤的事件会在转换节点处淡出。

```xml
<arc:DataFlow EventsPerSecond="8" FilterRatio="0.3" />
```

### 来源与标签

`Sources` 接受一到四个 `DataFlowSource` 值（一个标题和一行可选的详细信息）。`Labels` 接受一个 `DataFlowLabels` 对象，用于设置图表中所有可见的文字。

<Demo name="OwnSources" />

### 播放与速度

`IsPlaying` 用于播放和暂停故事（双向绑定），`Speed` 缩放场景时钟（0.25 到 4）。`Play()`、`Pause()` 和 `Restart()` 可在代码中实现相同的操作。

```xml
<arc:DataFlow Speed="2" IsPlaying="{Binding IsPipelineRunning}" />
```

```csharp
flow.Speed = 0.5;
flow.Restart();
```

### 跟随故事进度

`Step` 和 `StepCount` 描述循环中的位置（共三步）。`StepChanged` 提供新的步骤及其说明文字；`RowsStored` 和 `EventsFiltered` 分别统计已写入仓库的行数和被移除的事件数。

```csharp
flow.StepChanged += (_, e) => status.Text = $"第 {e.Step + 1} 步，共 {flow.StepCount} 步：{e.Caption}";
```

### 减弱动效

在减弱动效（或关闭动效）下，插图是其稳定状态的静态画面，说明文字概括整个故事。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: DataFlow -->

<!-- api: DataFlowLabels -->

## 无障碍

- `Label` 是无障碍名称，当前的说明文字作为帮助文本提供。
- 插图可获得焦点，焦点环宽 2px。
- 空格键或 Enter 播放与暂停故事，R 键从头开始播放。
- 说明文字的变化不会被播报，因为循环播放的故事会无休止地重复播报。

## 动效

- 事件以小数据包的形式沿连线传输，缓冲区在事件流中逐渐填满，读数同步递增。
- 每一步都会点亮当前活动的连线，其余连线淡回原色。
- 减弱动效和关闭动效时，显示稳定状态的静态画面，所有连线均高亮，说明文字概括整个故事。两种模式下播放按钮都会隐藏。

## 相关组件

- `PaymentFlow`：单笔请求被通过或被拒绝的场景。
- `MultiRegionFailover`：流量在区域之间切换的场景。
- `VectorSearch`：根据最相近的文档回答问题的场景。
