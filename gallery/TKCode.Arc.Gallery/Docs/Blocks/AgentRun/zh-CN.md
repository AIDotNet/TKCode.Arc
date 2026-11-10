---
title: Agent run
summary: 实时展示一次 AI 智能体运行：目标，由思考、带输入与结果的工具调用、改动的文件和消息组成的时间线，计时、费用与 token，以及停止或重试。
source-hash: c189a387022d
---

<Demo name="Preview" />

## 何时使用

`AgentRun` 是你在 AI 智能体执行任务时观看的页面。目标放在最上方，让你始终知道智能体想做什么。下面的时间线一步一步增长：智能体想了什么，调用了哪个工具、输入是什么、返回了什么，新建或修改了哪些文件，以及最后对你说了什么。角落里显示计时、花费和用掉的 token，一条进度条显示完成了几步，一个按钮用来停止运行或重新开始。

- **编程、数据或运维智能体**：要花几分钟、调用很多次工具，用户想看到进度并随时介入。
- **任务的详情页**：从智能体工作的列表或看板打开。
- **事后查看一次已结束的运行**：同一个视图可以用保存的步骤显示已完成、已停止或失败的运行。

## 何时不用

- **只有几次工具调用的聊天回复。** 请使用 `AI chat` 或 `AI answer`，它们在对话中直接显示工具。
- **同时查看很多次运行。** 请用 `Agent tasks` 看总览，再从那里打开一次运行。
- **成千上万行的长日志。** 原始日志请放在日志查看器中；这里的一步是一个有意义的操作。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它会播放一次内置的示例运行，方便先试用。在应用中，给它目标、作为 `ObservableCollection<AgentRunStep>` 的步骤、状态、费用和 token，并处理停止与重试：

```xml
<arc:AgentRun Goal="{Binding Goal}"
              Steps="{Binding Steps}"
              State="{Binding State}"
              Cost="{Binding Cost}"
              Tokens="{Binding Tokens}" />
```

```csharp
run.StopRequested += (_, _) => agent.Cancel();
run.RetryRequested += (_, _) => StartNewRun();

// As your agent works: add a step when it starts, update it when it ends.
var step = new AgentRunStep { Kind = AgentRunStepKind.ToolCall, Title = "Find the route", ToolName = "search_code",
                              Input = query, Status = AgentRunStepStatus.Running };
steps.Add(step);
step.Output = result;
step.Duration = TimeSpan.FromSeconds(1.3);
step.Status = AgentRunStepStatus.Done;
```

### 步骤

每个 `AgentRunStep` 有类型、标题和状态。思考步骤把推理放在 `Text` 中，工具调用有 `ToolName`、`Input` 和 `Output`，文件改动有 `Files`（路径、新建、修改或删除、增加和删除的行数），消息则放在 `Text` 中。按下步骤的标题行会展开详情；消息始终展开。运行显示在屏幕上时新到的步骤会升起就位，在你向上滚动之前，时间线会一直跟随最新的步骤。

### 状态、计时与费用

`State` 是双向的。处于 `Running` 时，计时从 `Elapsed` 开始自动计数，按钮显示 Stop。按下 Stop 会引发 `StopRequested`，然后把运行和正在进行的步骤标为已停止。已完成的运行提供 Run again，已停止或失败的运行提供 Retry；两者都会引发 `RetryRequested`。失败的运行在底部显示 `FailureMessage`。

<Demo name="Failed" />

### 已结束的运行

设置保存的步骤、状态和合计，即可显示一次已经结束的运行。

<Demo name="Completed" />

## API 参考

<!-- api: AgentRun -->

<!-- api: AgentRunStep -->

<!-- api: AgentRunFile -->

<!-- api: AgentRunLabels -->

## 无障碍

- 运行是名为 "Agent run" 的区域，目标是标题。
- **Tab** 可到达每个步骤的标题行以及 Stop 或 Retry 按钮。**Up** 和 **Down** 在步骤标题行之间移动，**Home** 和 **End** 跳到第一个和最后一个；**Enter** 或 **Space** 展开或收起步骤。
- 步骤标题行会读出类型、标题、状态和时间（"Tool call: Find the route, done, 1.3s"），并报告自身展开或收起的状态。
- 一个礼貌式实时区域会在步骤开始和运行结束时播报（"Completed in 1m 12s"）。
- 状态由图标和文字表示，从不只靠颜色。

## 动效

- 新步骤上升 6px，同时淡入并展开高度。
- 步骤详情以弹簧展开并带短暂淡入；箭头以灵敏的弹簧转动。
- 状态圆点和步骤标记在 240ms 内变色。
- 减少动效时只保留短暂淡入淡出，没有位移；关闭动效时所有变化立即生效。

## 响应式

- 运行最宽 720，小于这个宽度时跟随容器。
- 宽度在 480 或以下时，计时、费用和 token 移到状态行下方，两侧内边距收紧。
- 时间线超过 420 高时滚动；标题和路径用省略号缩短。

## 相关组件

- `Agent tasks`，一个由多次运行组成的看板，每次运行都能打开这个视图。
- `Research run`，用于阅读资料并撰写报告的研究智能体。
- `AI chat`，用于调用工具的对话。
