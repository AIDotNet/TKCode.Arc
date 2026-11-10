---
title: 智能体编排
summary: 一个主智能体把工作分给多个子智能体，并实时展示：在一张图上，每个任务、问题和结果都化作一个圆点在智能体之间移动；另有消息记录，以及显示每个智能体的任务、当前步骤和结果的详情面板。
source-hash: bf1670f71367
---

<Demo name="Preview" />

## 何时使用

智能体编排在一次多智能体运行进行时把它展示出来。主智能体接到一个目标，把它拆成多个任务，再把每个任务交给一个子智能体。卡片把主智能体画在上方，下方每个子智能体一个节点，主智能体到每个子智能体之间各有一条线。两个智能体之间的每条消息都会作为一个圆点沿线移动：任务向下，结果向上返回，两个子智能体互相提问时则横向移动。每个节点显示智能体的名称、角色、状态（空闲、思考中、工作中、等待中、完成或失败）以及它现在正在做的事。图的下方是按顺序列出全部消息的记录，详情面板显示所选智能体的任务、当前步骤、消息数量和结果。

- **智能体产品中的运行视图**：人们可以看到谁在做什么，以及运行卡在了哪里。
- **可以回放的运行记录**：把记录下来的步骤作为 `Script` 传入，然后播放、暂停或重置。
- **实时运行**：用 `Post` 从你自己的事件中发送每条消息；智能体会按消息类型自动更新。
- **多智能体功能的演示**：没有数据时，卡片会播放一段虚构的示例运行。

## 何时不用

- **单个助手回答问题**：请使用 `AiChat` 或 `AiAnswer`；这里没有可以分出去的工作。
- **步骤和分支很多的大型流程**：这张图是为一个主智能体和最多约六个子智能体设计的。大型流程请使用流程编辑器或表格。
- **需要搜索或筛选的日志**：消息记录是一次运行的可读过程，不是日志查看器。请使用表格。
- **运行智能体本身**：卡片只展示运行。调用模型和工具由你的代码负责。

## 用法

`AgentOrchestrator` 派生自 Avalonia 的 `TemplatedControl`。智能体是 `OrchestratorAgent` 对象，包含 id、名称、角色和模型。除非 `LeadId` 指定了其他智能体，否则第一个智能体就是主智能体。

```xml
<arc:AgentOrchestrator Goal="Plan the team offsite" IsAutoStart="False">
    <arc:OrchestratorAgent Id="lead" Name="Atlas" Role="Planner" />
    <arc:OrchestratorAgent Id="venue" Name="Scout" Role="Venues" />
    <arc:OrchestratorAgent Id="budget" Name="Ledger" Role="Budget" />
</arc:AgentOrchestrator>
```

### 结构

- **头部**：标题和状态胶囊（Running、Paused、Complete、Failed）、目标、进度行（"2 of 5 agents done · 9 messages"），以及 Reset 和 Run 按钮。
- **图**：带 Lead 标签的主智能体、各个子智能体、它们之间的线和移动的圆点。
- **消息记录**：每条消息的发送方、接收方、类型、运行时间和文字，最新的在最后。
- **详情面板**：所选的智能体（在你选择其他智能体之前是主智能体），显示它的状态、模型、消息数量、任务、当前步骤和结果。

### 用你的代码驱动运行

没有 `Script` 时，由你的代码驱动运行。`Post` 添加一条消息并按它的类型更新智能体；`SetStatus` 直接改变一个智能体；`Complete` 和 `Fail` 结束运行。

<Demo name="Programmatic" />

```csharp
orchestrator.Start();
orchestrator.Post(new OrchestratorMessage
{
    From = "lead", To = "venue", Kind = OrchestratorMessageKind.Delegate, Text = "Find three venues near the lake"
});
orchestrator.SetStatus("lead", OrchestratorAgentStatus.Waiting, "Waiting on two agents");
```

| 类型 | 改变的内容 |
|---|---|
| `Delegate` | 接收方的任务变为这段文字，并开始工作 |
| `Progress` | 发送方的当前步骤变为这段文字 |
| `Question` | 发送方进入等待 |
| `Answer` | 接收方（提问的一方）重新开始工作 |
| `Result` | 发送方完成；它的结果就是这段文字 |
| `Error` | 发送方失败；它的结果就是这段文字 |

一条消息只能发送一次；要再次发送相同的文字，请发送 `message.Copy()`。智能体是 `AvaloniaObject`，因此直接设置它的 `Status`、`Task`、`Activity` 或 `Result` 也会更新卡片。

### 播放脚本

`Script` 是一个 `OrchestratorStep` 列表：每一步在等待 `Delay` 之后发送一条消息或设置一个智能体的状态。可以用 `OrchestratorStep.Send` 和 `OrchestratorStep.Set` 创建步骤。`Speed` 按比例缩放所有等待时间，`IsAutoStart` 让卡片显示时就开始播放脚本，Run 按钮用于暂停、继续和重新播放。暂停时正在等待的那一步，在继续后会重新开始等待。使用示例智能体且没有脚本时，卡片会播放 `AgentOrchestratorSamples.Script()`。

<Demo name="Recovery" />

### 选择智能体

点击一个节点，或让它获得焦点后按 Enter 或 Space。`SelectedAgentId` 是双向的；为 null 时显示主智能体。

### 较窄的容器

卡片按自身宽度变化。子智能体无法并排放下时，会在主智能体下方排成一列。

<Demo name="Narrow" />

### 文字

所有文字都来自 `Labels`（`AgentOrchestratorLabels`）。带值的文字是函数，因此可以改变词序。

```csharp
orchestrator.Labels = new AgentOrchestratorLabels
{
    Title = "Équipe d'agents",
    StatusName = status => status == OrchestratorAgentStatus.Done ? "Terminé" : status.ToString()
};
```

### 内容建议

- **按智能体做的事给它命名**，并配上简短的角色："Scout · Market research"。
- **每条消息一句话**，用智能体自己的口吻写："Checked two of three rival stores"。
- **任务要短**：节点最多显示两行智能体正在做的事。

## API 参考

<!-- api: AgentOrchestrator -->

<!-- api: OrchestratorAgent -->

### OrchestratorMessage

| 属性 | 说明 |
|---|---|
| `From`、`To` | 发送方和接收方的 id |
| `Kind` | `Delegate`、`Progress`、`Question`、`Answer`、`Result` 或 `Error` |
| `Text` | 一句话 |
| `Sequence` | 它在本次运行中的序号，从 1 开始（发送时设置） |
| `Elapsed` | 发送时的运行时间（发送时设置） |

### OrchestratorStep

| 属性 | 说明 |
|---|---|
| `Delay` | 在上一步之后等待的时间（速度为 1 时） |
| `Message` | 要发送的消息，或 null |
| `AgentId`、`Status`、`Activity`、`Result` | 要改变的智能体，或 null |

### 方法

| 方法 | 作用 |
|---|---|
| `Start()` | 开始运行（暂停的运行会继续，已结束的运行会重新开始） |
| `Pause()`、`Resume()`、`Toggle()` | 暂停、继续，或执行 Run 按钮的操作 |
| `Reset()` | 清空运行：所有智能体空闲，没有消息 |
| `Complete()`、`Fail()` | 结束运行 |
| `Post(message)` | 发送一条消息 |
| `SetStatus(id, status, activity)` | 改变一个智能体 |

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `MessagePosted` | 发送了一条消息 | `Message` |
| `AgentStatusChanged` | 某个智能体的状态改变 | `Agent`、`OldStatus`、`NewStatus` |
| `RunStateChanged` | 运行开始、暂停、结束或被重置 | `OldState`、`NewState` |

## 无障碍

- 图是一个名为 "Agents" 的列表；每个节点是一个列表项，名称包含智能体的名称、角色、状态和当前步骤（"Scout, Market research, Working: Checked two of three rival stores"），并随运行过程更新。
- 状态从不只靠颜色表达：每种状态都有自己的图标（转圈、沙漏、对勾、叉号）和文字。
- 一个礼貌型实时区域会读出每次状态变化（"Scout is done"）以及运行的开始、暂停和结束。
- 消息记录的每一行名为 "Atlas to Scout, Task: …"；Run 按钮的名称跟随它的文字，Reset 有名称和提示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | Reset、Run，然后是图（一个停靠点），然后是消息记录 |
| `ArrowLeft` / `ArrowRight` | 上一个或下一个节点（在排成一列时 `ArrowUp` / `ArrowDown` 也可以） |
| `ArrowUp` / `ArrowDown` | 从子智能体到主智能体，从主智能体到所选的子智能体 |
| `Home` / `End` | 主智能体、最后一个子智能体 |
| `Enter` 或 `Space` | 选择获得焦点的节点 |

## 动效

- 每条消息会让一个圆点在 900 ms 内以缓入缓出曲线沿线移动，在两端淡入淡出，并带有柔和的光晕。
- 通往工作中智能体的线变为强调色；已完成的线变为绿色，失败的线变为红色虚线。
- 工作中和思考中的节点显示一个转圈；节点的边框和背景在 160 到 240 ms 内变化。
- 运行时，状态胶囊会交叉淡化它的文字并转动它的转圈。
- 减少动效时不显示圆点，但保留所有状态变化；边框立即改变。关闭动效时一切立即生效。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 消息 | 圆点移动 900 ms | 没有圆点 | 没有圆点 |
| 节点状态 | 边框渐变；转圈转动 | 立即 | 立即 |
| 线的状态 | 立即 | 立即 | 立即 |

## 响应式

- 卡片最宽 960 px，按自身宽度而不是窗口宽度变化。
- 当每个子智能体都能以 156 px 以上的宽度并排放下时，它们在主智能体下方展开。否则它们在主智能体下方排成一列，左侧有一条直线，节点只显示一行文字。
- 宽度在 640 px 及以下（`:compact`）时，内边距变小，详情面板移到消息记录上方。
- 消息记录高 260 px，因此运行时卡片高度保持不变；记录在其中滚动，并跟随新消息，除非你向上滚动过。

## 相关组件

- `AiChat`：一个助手在对话中回答。
- `AgentMemory`：助手记住了什么。
- `ModelArena`：在同一个提示上比较两个模型。

| 组件 | 何时选择 |
|---|---|
| `AiChat` | 一个助手和一个人对话 |
| `ModelArena` | 两个模型回答同一个提示 |
| `Timeline` | 没有智能体、按时间排列的事件 |
