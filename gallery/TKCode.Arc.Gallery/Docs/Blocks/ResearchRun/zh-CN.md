---
title: Research run
summary: 从问题到带引用报告的一次深度研究：阶段步骤条、计划、边读边填满的资料、从资料中记下的笔记，以及点击编号引用即可打开资料的报告。
source-hash: dafdca216e00
---

<Demo name="Preview" />

## 何时使用

`ResearchRun` 展示一个研究智能体处理需要大量资料的问题。一个小步骤条说明它进行到哪里（计划、阅读、撰写、报告）以及用了多长时间。计划列出智能体为自己定下的步骤并逐一打勾。它找到的每份资料在阅读时填满一条进度线，被舍弃的资料会划掉并写明原因。发现的内容进入笔记，并带上所依据资料的编号。报告完成后，整个过程收起成一行，报告占据主要位置，其中的编号引用可以打开对应资料。

- **深度研究和文献综述助手**：要花几分钟阅读几十个页面。
- **尽职调查、市场扫描和客服调查**：读者必须核对每个说法的来源。
- **展示保存下来的研究报告**，连同资料和笔记。

## 何时不用

- **只有几个来源的快速回答。** 请使用 `AI answer`，它在一次回复中直接标注来源。
- **动手做事而不是阅读的智能体**（修改文件、调用工具）。请使用 `Agent run`。
- **单纯的搜索结果列表。** 请使用 `Search results`。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它会播放一次内置的示例运行。在应用中，设置问题和计划，在智能体找到资料时加入 `ResearchRunSource` 并更新其状态和进度，记下笔记时加入笔记，撰写时设置报告：

```xml
<arc:ResearchRun Question="{Binding Question}"
                 Phase="{Binding Phase}"
                 Plan="{Binding Plan}"
                 CompletedPlanSteps="{Binding PlanDone}"
                 Sources="{Binding Sources}"
                 Notes="{Binding Notes}"
                 Report="{Binding Report}" />
```

```csharp
research.StopRequested += (_, _) => agent.Cancel();
research.RestartRequested += (_, _) => StartAgain();
research.SourceInvoked += (_, e) => { OpenInReader(e.ResearchSource); e.Handled = true; };
```

### 阶段与计时

`Phase` 从 `Planning` 依次到 `Reading`、`Writing` 和 `Done`（或 `Stopped`）。在计划、阅读或撰写时，计时从 `Elapsed` 开始自动计数，按钮显示 Stop；之后显示 Research again。阶段变为 `Done` 时，过程收起成一行摘要（"Read 6 sources, took 9 notes in 2m 31s"），并带有 Show process。

### 资料、笔记与引用

`ResearchRunSource` 是一个 `AiSource`（编号、标题、网站、地址、摘录），另有 `Status`（排队、阅读中、已读、已舍弃）和从 0 到 1 的 `Progress`。笔记是 `ResearchRunNote` 记录：一条发现以及其资料的编号。在报告中，在某个说法后写 `[3]` 即引用第 3 份资料；这个标记会变成一个带编号的小按钮，提示文字是资料的标题。按下引用或资料行会引发 `SourceInvoked`；如果没人处理，就打开资料的 http 或 https 地址。

<Demo name="Finished" />

## API 参考

<!-- api: ResearchRun -->

<!-- api: ResearchRunSource -->

<!-- api: ResearchRunNote -->

<!-- api: ResearchRunLabels -->

## 无障碍

- 运行是名为 "Research" 的区域；问题是标题，每个部分都有标题。
- **Tab** 可到达 Stop、资料行、引用、Show process 和 Copy。**Enter** 或 **Space** 打开资料。
- 资料行会读出编号、标题、网站和状态（"Source 2: Sizing guide, retrofit-guild.example, Read"）。
- 引用读作 "Source 3: Radiators that work at low flow temperatures"。
- 一个礼貌式实时区域会在每个阶段开始和报告完成时播报。
- 状态由文字和图标表示，从不只靠颜色。

## 动效

- 步骤条的圆点和连线在 240ms 内变色；阅读进度线平滑填满。
- 过程以弹簧收起和展开，并带短暂淡入淡出。
- 减少动效时只保留短暂淡入淡出；关闭动效时所有变化立即生效。

## 响应式

- 运行最宽 860，小于这个宽度时跟随容器。
- 宽度在 640 或以下时，计划和资料上下排列，内边距收紧。
- 过长的标题和网站用省略号缩短；报告自动换行。

## 相关组件

- `Agent run`，用于通过工具做事的智能体。
- `AI answer`，用于一次带引用的回复。
- `Agent tasks`，用于同时查看多个智能体。
