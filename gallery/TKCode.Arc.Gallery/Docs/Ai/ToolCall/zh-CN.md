---
title: Tool call
summary: 智能体对话记录中的一行工具调用：工具、它做了什么以及结果如何，点开即可看到调用的输入与输出，并在行内完成审批与重试。
source-hash: 034f5a63d7bf
---

<Demo name="Preview" />

## 何时使用

`ToolCall` 是智能体对话记录中的一行：工具的标记、用平实文字描述这次调用做了什么、"4 results · 1.2s" 这类低调的元信息，以及一个状态图标。`ToolCallGroup` 把某一轮助手回复中的每次调用收集到一个标题下，标题写着 "Used 4 tools"。点击一次调用即可展开其输入与输出，使对话记录保持易读，细节仍只需一次点击。

- **展示智能体做了什么**：在聊天或智能体日志中，参数与结果都只需一次点击。
- **人在回路中**：在运行前必须允许或拒绝的调用，例如发送消息或写入数据库，行内提供拒绝与允许按钮。
- **可恢复的失败**：超时的查询会在出错处显示原因和一个重试按钮。
- **调试与审计视图**：人们需要复制调用的精确输入与输出。

## 何时不用

- **带步骤与进度的计划**：使用 `AgentPlan`；工具调用是一个动作，不是清单。
- **整次运行的单一状态**：使用 `AgentStatus`；分组标题只汇总它所包含的调用。
- **跨多次运行的审批队列**：自行构建列表；一行只替一次调用做决定。
- **智能体可以使用的工具**：那是配置，不是历史记录。

## 用法

提供一个 `ToolCallItem` 给行。工具是数据，而非品牌：供屏幕阅读器使用的名称、一个 24 × 24 的描边标记（Lucide 图标）以及每种状态对应的短语。若不提供项，该行显示一次示例网络搜索。

```xml
<arc:ToolCall>
    <arc:ToolCallItem Id="tickets" ToolName="Tracker"
                      ToolMark="{x:Static arc:ArcIcons.Inbox}" Status="Success"
                      Title="Searched tickets in finance/close" Summary="4 results" Duration="0:0:1.18" />
</arc:ToolCall>
```

单独使用时，行位于一个 1px 的 Surface 边框内（圆角 16，内边距 4）；在分组中则与边框齐平，没有边框。

对于一轮智能体回复，在智能体开始调用时把它们加入 `ToolCallGroup`，并原地修改这些项：

```csharp
var group = new ToolCallGroup();
group.Items.Add(call);                  // a new row grows in from zero height
call.Status = ToolCallStatus.Success;   // the row keeps its place and its open state
group.IsActive = agentIsWorking;        // keeps "Using" between calls
```

### 结构

1. **行**：高 40px。标记（16px）、标题（单行，超出则截断）、元信息、状态图标和一个箭头。
2. **原因行**：位于失败调用的标题之下（其原因），或位于等待审批调用的标题之下（允许后会发生什么），12px。带有原因行的行高 58px。
3. **按钮**：失败调用上的重试，等待审批的调用上的拒绝与允许，高 28px。
4. **输入与输出**：等宽的键值列表；结果则以列表、表格、着色 JSON 或文本呈现。每个都有一个复制按钮，点击后以对勾就地确认 1.9 秒。
5. **分组标题**：每个工具一个重叠的标记，然后是 "Using 3 tools"、"Used 4 tools" 或 "Waiting for approval"，右侧是 "1 failed" 或总时长。

### 状态

<Demo name="Statuses" />

每种状态都有各自的图标形状，因此颜色从来不是唯一的提示：旋转指示器（运行中）、只绘制一次的对勾（成功）、警告（错误）、带问号的盾牌（审批）和禁止符号（拒绝）。运行中的标题会闪烁。审批行带有强调色轮廓，底色是淡淡的强调色。已拒绝的行会弱化显示，其元信息读作 "Denied"。

`ToolCallItem` 为每种状态提供一个短语：`Title` 是已完成的过去时（"Searched …"），`RunningTitle` 是现在时，`ApprovalTitle` 是一个请求（"Post a status note to …"），`ErrorTitle` 是失败描述（"Could not read …"）。缺少的短语会退回到 `Title`。

### 输出

<Demo name="Outputs" />

| 输出 | 外观 | 折叠 | 复制得到 |
|---|---|---|---|
| `ToolCallListOutput` | 等宽数字的前导、标题与标签；32px 行 | 超过 5 行后 | 每个条目一行 |
| `ToolCallTableOutput` | 表头与等宽单元格；30px 行；在其容器内可横向滚动 | 超过 5 行后 | 带表头的制表符分隔行 |
| `ToolCallJsonOutput` | 缩进的 JSON，键、字符串、数字与字面量着色；20px 行 | 超过 10 行后 | JSON 文本 |
| `ToolCallTextOutput` | 一段保留换行的文字 | 从不 | 文本 |

JSON 基于 `System.Text.Json.Nodes` 构建，由我们自己的一个小型词法分析器着色，因此没有任何部分使用反射。折叠的内容藏在 "Show 25 more lines" 之后，它会变成 "Show less"。缺少摘要时，会根据输出推导："N results"、"N rows"、"N fields"。`OutputTemplate` 可用你自己的视图替换内置视图，用于显示成功调用的结果。

### 由你自己的数据驱动的分组

<Demo name="OwnData" />

分组会引发 `ApproveRequested`、`DenyRequested` 和 `RetryRequested`（它们从行冒泡上来，并携带该项）。由你自己更新调用，通常先变为运行中，再变为成功，并设置 `Handled`。控件从不改变你驱动的状态。

如果无人处理某个操作，控件会保留本地行为，使没有接线的页面也能工作：**拒绝**将调用标记为已拒绝；**允许**与**重试**只模拟一次运行，且仅限内置示例数据（`ToolCallSamples`、`ToolCallSimulation`）。对于其他数据，什么都不会改变。模拟运行会通过该行的实时区域说明这一点（"This is sample data, so the call is only simulated."）。

### 展开与折叠

展开分组标题时，各行以间隔 20ms 交错滑入，每行向下落 6px 后定位。分组展开期间追加的调用则会从零高度生长出来。智能体工作时分组保持展开；在每次调用结束 450ms 后（`AutoCollapse`、`AutoCollapseDelay`），它会自动收起，使答案紧跟在 "Used 4 tools" 之下。一旦人已经展开或收起过分组或某一行，它就永远不会自行折叠。需要决定的调用会展开一个折叠的分组。`ExpandedChanged` 会说明这次变化是否为自动。

### 模拟

`ToolCallSimulation` 按时钟把一段预设的回合播放进一个可观察列表：调用约每 225ms 追加一次，运行其时长后结束；失败的调用等待 `Retry`；需要审批的调用等待 `Approve` 或 `Deny`。将分组的 `ItemsSource` 绑定到 `Items`，并将 `IsActive` 绑定到 `IsWorking`。默认脚本是 `ToolCallSamples.Script()`；传入 `TimeProvider` 即可在测试中逐步推进。预览中分组周围的对话记录（问题、句子、答案以及 "Run again" 按钮）是演示内容，不属于控件的一部分。

### 窄容器

<Demo name="Narrow" />

当行自身宽度低于 440px 时，元信息只保留时长，原因文字换行，按钮移到其下方，输入与输出则取消缩进。低于约 370px 时元信息隐藏。过宽的 JSON 与表格在其容器内滚动。

### 减弱动效

在减弱与关闭级别下，交错滑入、旋转指示器、闪烁的移动以及所有弹簧都会停止：面板、行与状态立即改变。标签、播报、焦点与所有内容都保留。

### 内容准则

- 将标题写成动词短语：已完成时用过去时（"Read the export job log"），运行中用现在时。
- 按工具的用途为其命名（"Tracker"、"Warehouse"）。它先于标题被读到。
- 原因行用一句话说明出了什么问题，或允许后会发生什么。
- 选择读起来最清楚的输出：搜索结果用列表，行数据用表格，结构化数据用 JSON，已发送的消息或简短日志用文本。

### 最佳实践

- 保持 `Id` 稳定：行在状态变化时保留其展开状态。
- 原地修改项；新的调用加到末尾。
- 智能体工作时设置 `IsActive`，使标题不会在 "Using" 与 "Used" 之间闪烁。
- 每个工具标记使用一个 Lucide 图标；除非你设置了 `ToolMarkBrush`，否则标记跟随文字颜色。

### 常见问题

**我可以把品牌标志用作标记吗？** 标记可以是任意 24 × 24 的描边几何图形。品牌标志需要你自行取得授权；示例只使用 Lucide 图标。

**允许会运行工具吗？** 不会。它会引发 `ApproveRequested`；由你的代码运行工具并更新状态。

**为什么运行中调用的输出为空？** 还没有结果；该区域会说明这一点。

## API 参考

<!-- api: ToolCall -->

<!-- api: ToolCallGroup -->

<!-- api: ToolCallItem -->

<!-- api: ToolCallArgument -->

<!-- api: ToolCallOutput -->

<!-- api: ToolCallListOutput -->

<!-- api: ToolCallListEntry -->

<!-- api: ToolCallTableOutput -->

<!-- api: ToolCallJsonOutput -->

<!-- api: ToolCallTextOutput -->

<!-- api: ToolCallLabels -->

<!-- api: ToolCallSimulation -->

<!-- api: ToolCallSimulationStep -->

<!-- api: ToolCallActionEventArgs -->

<!-- api: ToolCallExpandedChangedEventArgs -->

<!-- api: ToolCallSimulationEventArgs -->

枚举如下：

| 类型 | 取值 |
|---|---|
| `ToolCallStatus` | `Running`, `Success`, `Error`, `Approval`, `Denied` |
| `ToolCallSimulationOutcome` | `Succeeds`, `FailsUntilRetried`, `NeedsApproval` |
| `ToolCallSimulationState` | `Idle`, `Working`, `WaitingForRetry`, `WaitingForApproval`, `Finished` |
| `ToolCallSimulationPhase` | `Started`, `CallAppended`, `CallSucceeded`, `CallFailed`, `WaitingForApproval`, `Retried`, `Allowed`, `Denied`, `Finished` |

`ToolCallSamples` 保存虚构的示例调用与默认脚本。

## 无障碍

- 分组标题与每一行都是按钮，并报告其是否展开。一行的名称会读出工具、短语与状态，例如："Tracker: Searched tickets in finance/close Done, 4 results, 1.2s"。
- 状态从不只依赖颜色：每种状态都有各自的图标，失败与等待中的调用会显示原因行，名称中也把状态拼写出来。
- 每一行都通过礼貌级别的实时区域播报状态变化，包括示例调用只是模拟这一点。
- 重试、允许与拒绝都以其所作用的调用命名："Allow: Post a status note to #finance-ops"。当按钮消失时，焦点会移到其所在的行，而不是落回页面。
- 输入与输出是有名称的分组；复制按钮通过实时区域确认，且不改变宽度。
- 指针焦点不显示焦点环；键盘焦点在行内显示 2px 的焦点环，因此不会被裁切。
- 每个文字都位于 `ToolCallLabels` 中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次移动到分组标题、每一行、行上的重试、拒绝或允许按钮，以及展开的行中的复制按钮与折叠按钮 |
| Enter, Space | 展开或收起分组或某一行；在按钮上则按下该按钮 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 展开一行或分组 | 高度以弹簧变化，内容在 160ms 内淡入 | 立即 | 立即 |
| 分组展开时的各行 | 间隔 20ms 交错，下落 6px | 无 | 无 |
| 追加到已展开分组的一行 | 从零高度生长 | 立即 | 立即 |
| 状态图标 | 缩放与淡入淡出切换；对勾在 330ms 内绘制 | 立即 | 立即 |
| 运行中的标题与 "Using 3 tools" | 闪烁，每次 1.8s 扫过一遍 | 静止 | 静止 |
| 标题或标题文字变化 | 从柔和的模糊中升起，耗时 240ms | 在 130ms 内淡入 | 立即 |
| 标题中新增一个工具标记 | 以弹簧展开变宽 | 立即 | 立即 |
| 箭头 | 以弹簧转动 | 立即 | 立即 |
| 审批轮廓 | 在 240ms 内淡入淡出 | 立即 | 立即 |

## 相关组件

- `AgentStatus`：整次运行的实时状态。
- `AgentPlan`：智能体行动前编写的计划。
- `AgentTimeline`：在同一时间轴上回放多智能体运行。
