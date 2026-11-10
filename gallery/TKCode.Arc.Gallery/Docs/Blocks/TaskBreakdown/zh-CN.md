---
title: Task breakdown
summary: 把一个大任务拆成根据描述建议的子任务，然后编辑每个标题、估时和负责人，通过拖动或按键调整顺序，并在创建前查看总时长和每个人的工作量。
source-hash: d98e03f2adb6
---

<Demo name="Preview" />

## 何时使用

`TaskBreakdown` 把一个大任务变成计划。先描述任务；Suggest 会根据描述建议子任务，每个都带有估时和按工作量选出的负责人；之后每个子任务都可以改名、重新估时、改派、拖到新的位置或删除，也可以再添加。底部合计时长，显示每个人的工作量，并创建子任务。

- **规划一个功能或一次上线。** 写下要做的事，得到一份可编辑的初始列表。
- **拆分过大的任务。** 把顺序和负责人放在同一个地方。
- **接入模型。** 把你自己的服务交给 `SuggestAction`；编辑交给本区块。

## 何时不用

- **持续跟踪任务。** 创建之后，请在 `Kanban board` 上跟踪子任务。
- **依赖关系和日期。** 子任务需要开始和结束日期时，请使用 `Gantt timeline`。
- **一句话就能说完的任务。** 对已经很小的任务，拆分没有意义。

## 用法

本区块派生自 `TemplatedControl`。当 `IsSampleDataEnabled` 为 true 且没有提供任何内容时，它显示一个虚构的任务、团队和建议。提供 `TaskTitle`、`Description`（都是双向）和 `People`；列表直接编辑 `Subtasks`。

```xml
<arc:TaskBreakdown TaskTitle="Move the blog" HoursPerDay="7" IsSampleDataEnabled="False" />
```

```csharp
breakdown.SuggestAction = async (request, cancellation) => await planner.SplitAsync(request.Title, request.Description, cancellation);
breakdown.Created += (_, e) => tracker.CreateSubtasks(e.Title, e.Subtasks);
```

### 建议

没有 `SuggestAction` 时，内置的建议功能会把描述拆成步骤（按行、列表项、句子和 "then"），根据每一步提到的工作类型估时，没有提到审查时加上一个审查步骤，并按工作量把任务分给各人。`SuggestFromText` 不需要区块就能运行它。建议运行期间 `IsSuggesting` 为 true，按钮显示加载指示；失败时显示一行简短说明，并保留现有列表。

### 编辑与创建

行是一个 `ReorderableList`：拖动手柄，或使用手柄的按键。`TotalHours` 合计估时；合计行还会按 `HoursPerDay` 给出工作天数。`Create` 引发 `Created`，带上有标题的子任务。文字都在 `Labels`（`TaskBreakdownLabels`）中。

<Demo name="OwnSuggester" />

## API 参考

<!-- api: TaskBreakdown -->

<!-- api: TaskBreakdownSubtask -->

<!-- api: TaskBreakdownRequest -->

## 无障碍

- 标题和描述输入框都有标签；每行的标题输入框名为 "Subtask N"，估时和负责人也有名称。
- 列表手柄遵循 `ReorderableList`：**↑/↓** 移动一行，**Space** 拿起和放下，**Escape** 取消；每次移动都会被播报。
- 建议和创建会以礼貌方式播报；建议失败会以强调方式播报。

## 动效

- 行像 `ReorderableList` 一样抬起、滑动和落下；建议按钮显示加载指示并淡入淡出地切换文字。
- 减少动效时仍可拖动，但没有弹簧。

## 响应式

- 在 `CompactWidth`（600 px）或更窄时，每行把估时和负责人放到标题下方；内边距收紧到 16。
- 底部的工作量标签会换行。

## 相关组件

- `ReorderableList`、`NumberField` 和 `Select`，组成本区块的控件。
- `Kanban board` 和 `Gantt timeline`，用于跟踪结果。
