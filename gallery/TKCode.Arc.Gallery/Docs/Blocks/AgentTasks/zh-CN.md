---
title: Agent tasks
summary: 交给 AI 智能体的任务看板：Queued、Running、Needs you 和 Done 四列卡片，显示负责的智能体、进度或提问，可按智能体筛选，按下卡片即可打开任务的运行。
source-hash: 4a36d0bbdc01
---

<Demo name="Preview" />

## 何时使用

`AgentTasks` 是用户交给 AI 智能体的工作总览。每个任务是四列之一中的一张卡片：等待开始、进行中、需要人处理（要回答的问题或要查看的错误）以及已完成。进行中的卡片显示进度和当前步骤，等待的卡片显示智能体的提问，每张卡片都显示负责的智能体和最后一次变化的时间。顶部的筛选按钮可以只看一个智能体。按下卡片会打开该任务的运行。

- **团队的智能体工作区**：多个智能体同时处理交办的任务。
- **智能体工作的收件箱**：不时需要有人处理。

## 何时不用

- **详细查看一次运行。** 请使用 `Agent run`。
- **批准智能体想执行的操作。** 请使用 `Agent approvals`。
- **由人规划和拖动的项目。** 请使用 `Kanban board` 或 `Project board`，它们支持拖动。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它显示示例任务，其中进行中的任务会自己推进。在应用中，给它你的任务（`ObservableCollection<AgentTaskItem>` 中任务的增减都会被跟随），并在按下卡片时打开运行：

```xml
<arc:AgentTasks Tasks="{Binding Tasks}" />
```

```csharp
board.TaskOpened += (_, e) => navigation.OpenRun(e.Task.Id);
task.Progress = 0.4;
task.Status = AgentTaskStatus.NeedsInput; // the card moves to Needs you
```

### 列与卡片

卡片所在的列由 `Status` 决定：`Queued`、`Running`、`NeedsInput` 和 `Failed`（都在 Needs you 列，Failed 带红色标签）以及 `Done`。进行中的卡片用进度条和百分比显示 `Progress`；`Note` 放当前步骤、提问或错误。`Updated` 显示为相对时间。`AgentFilter`（双向）让看板只显示一个智能体；筛选按钮列出看板上的每个智能体。

<Demo name="OpenRun" />

## API 参考

<!-- api: AgentTasks -->

<!-- api: AgentTaskItem -->

<!-- api: AgentTasksLabels -->

## 无障碍

- 看板是名为 "Delegated tasks" 的区域；每列以标题和数量命名。
- **Tab** 可到达筛选按钮和卡片。**Up** 和 **Down** 在一列中移动；**Left** 和 **Right** 移到相邻一列中最近的卡片；**Home** 和 **End** 到一列的两端。**Enter** 或 **Space** 打开运行。
- 卡片会读出标题、智能体、状态以及进度或备注，并提示 "Press Enter to open the run"。
- 一个礼貌式实时区域会在任务换列时播报。

## 动效

- 按下卡片时略微缩小，悬停时边框变色；已完成的卡片稍微变淡。
- 减少动效时颜色变化很短；关闭动效时立即生效。

## 响应式

- 看板填满容器。
- 宽度在 720 或以下时各列上下排列，Up 和 Down 会继续进入下一列。
- 标题最多两行；备注用省略号缩短。

## 相关组件

- `Agent run`，卡片打开的视图。
- `Agent approvals`，等待人处理的操作。
- `Agent routines`，按计划执行的智能体工作。
