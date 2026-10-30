---
title: AI workflow
summary: 一张自动化卡片：触发器、AI 步骤、分支和动作排在同一条轨道上，测试运行沿轨道逐步滑下并展开每一步的输出，步骤从紧凑的命令菜单中添加。
source-hash: 31dbad0538c5
---

<Demo name="Preview" />

## 何时使用

`AiWorkflow` 展示一个简短的自动化流程：应用触发器、模型步骤和应用动作混在一起，并且可以就地测试。步骤是排在同一条细线轨道上的
32px 紧凑行。测试运行一次只动一件事：一个小小的强调色圆点滑向下一步，该步骤运行时闪烁，然后显示用时和它产出内容的预览。分支会
选中其中一条路径，并把另一条标为 Skipped。步骤从一个小命令菜单中添加，用所在行上的按钮删除。

- **自动化与集成页面**：人们在这里组合一个触发器、一个 AI 步骤和几个动作。
- **新手引导与产品导览**：展示 AI 步骤在团队已在使用的应用之间处于什么位置。
- **复查工作流**：它做了什么，以及一次示例运行在每一步产出了什么。

## 何时不用

- **大型图**：有很多分支、循环或汇合时，使用 Workflow 包中的 `WorkflowCanvas`。
- **单个工具调用，或智能体的实时步骤**：使用 `ToolCall` 或 `AgentStatus`。
- **智能体写下的计划**：使用 `AgentPlan`。
- **开启自动播放的正式产品**：设置 `AutoPlay="False"`；重复播放的运行只用于展示。

## 用法

把你的流程放进 `Steps`。卡片自己没有步骤时，会播放它的 `Simulation`，也就是由你提供的流程。两者都没有时显示空状态：只有外框，
测试运行不可用，并显示 "No steps yet"；如果 `Catalog` 里有触发器，则列出可作为起点的触发器。只要没有接入运行器，标题栏就会显示
"Simulated"。

```xml
<arc:AiWorkflow Title="Escalate urgent support email"
                Description="Runs on every new email to support@example.com"
                AutoPlay="False" />
```

### 步骤

`Steps` 就是流程，按顺序排列。每个 `AiWorkflowStep` 有 `Id`、`Kind`（`Trigger`、`Ai`、`Action` 或 `Branch`）、`App`（内置的
`AiWorkflowApp` 之一，或你自己的名称与标记）、`Title`，以及可选的 `Detail`、运行时显示的 `Activity`，还有模拟运行使用的
`Output`、`Duration` 和 `Error`。分支带有 `Paths`（每条是一个 `AiWorkflowBranchPath`，包含 id、标签页文字和它自己的步骤），并
且必须是最后一个顶层步骤。

```csharp
flow.Steps =
[
    new AiWorkflowStep { Id = "mail", Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Detail = "support@example.com" },
    new AiWorkflowStep { Id = "label", Kind = AiWorkflowStepKind.Ai, App = AiWorkflowApp.Ai, Title = "Classify with Claude Sonnet 5.5", Activity = "Reading the email" },
    new AiWorkflowStep { Id = "issue", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Detail = "Support team" },
];
```

卡片把步骤当作值：要修改步骤，就给 `Steps` 一个新列表。空列表显示空状态，其中列出目录里可作为起点的触发器。

内置应用都是通用的，使用 Lucide 图标：`Mail`、`Ai`（模型步骤，以强调色绘制）、`Chat`、`Tracker`、`Docs`、`Sheets`、`Crm`、
`Helpdesk`、`Repo`、`Payments`、`Schedule`、`Webhook` 和 `Branch`。真实产品请传入 `new AiWorkflowApp("id", "Name", geometry)`，
并使用你自己的 24 × 24 描边图形；模型步骤的应用以模型厂商命名（`isAccent: true` 让它的标记以强调色绘制）。

### 模拟

`AiWorkflowSimulation` 保存一个在 `Steps` 为 null 时播放的流程：`Title`、`Description`、`Steps` 以及添加步骤用的 `Catalog`。
卡片显示这个流程；开启 `AutoPlay` 时，只要卡片在屏幕上就会测试运行它，短暂停顿后重复，直到有人在卡片内按下鼠标或按键。每一步都按
它的 `Output`、`Duration` 和 `Error` 模拟，不会发送任何内容。你自己设置的 `Steps`、`Title`、`Description` 和 `Catalog` 优先于
它。添加或删除步骤会把编辑后的流程放进 `Steps`；把 `Simulation` 设为 null 会停止运行并显示空状态。

```csharp
flow.Simulation = new AiWorkflowSimulation
{
    Title = "Escalate urgent support email",
    Description = "Runs on every new email to support@example.com",
    Steps = SupportFlow.Steps(),     // List<AiWorkflowStep> with Output, Duration and Error
    Catalog = SupportFlow.Catalog(), // List<AiWorkflowCatalogItem>
};
```

本页的演示在 `AiWorkflowDemoData` 中构建流程；模型步骤使用厂商官方页面上的真实模型（核对于 2026-10-29），所有人物、公司和数字
都是虚构的。

### 真实运行步骤

<Demo name="OwnRunner" />

设置 `RunStep`，即可让每一步调用你的测试端点。它会收到步骤和一个 `AiWorkflowRunStepContext`，其中有 `Cancellation` 令牌（在
Stop、开始新一轮运行或卡片离开窗口时取消）、按步骤 id 索引的 `Previous` 输出，以及 `Attempt` 次数。返回其中一种输出即可；抛出异常
则以异常消息让该步骤失败。分支返回 `AiWorkflowRouteOutput` 来选择路径。

```csharp
flow.RunStep = async (step, context) =>
{
    using var response = await http.PostAsync($"/api/workflows/test/{step.Id}", null, context.Cancellation);
    response.EnsureSuccessStatusCode();
    return new AiWorkflowTextOutput { Text = await response.Content.ReadAsStringAsync(context.Cancellation) };
};
```

没有 `RunStep` 时，每一步都按它的 `Output` 和 `Duration` 模拟；带 `Error` 的步骤在第一次尝试时失败，以便展示 Retry。
`RunCompleted` 报告通过的运行，或停在失败步骤上的运行，并带上用时、所有输出和失败步骤的 id。`StartRun`、`StopRun` 和 `Retry`
可以从代码驱动运行。

### 输出

每种输出都以小号的弱化文字内嵌在其步骤下方，与标题对齐：

| 输出 | 显示 |
|---|---|
| `AiWorkflowEmailOutput` | 一行发件人和主题，然后是两行正文 |
| `AiWorkflowLabelOutput` | 一个带圆点的细线标签、等宽字体的置信度，以及流式出现的理由 |
| `AiWorkflowRouteOutput` | 分支走了哪条路径，可附一条说明 |
| `AiWorkflowMessageOutput` | 频道和作者，然后是消息内容 |
| `AiWorkflowRecordOutput` | 等宽字体的记录编号、它的标题和一条补充信息 |
| `AiWorkflowTextOutput` | 自由文本，例如起草的回复，以流式出现 |

### 添加与删除步骤

Add step 会打开一个紧凑的命令菜单：一个按标题、应用或详情筛选的搜索框，以及带应用标记和 1 到 9 快捷键的行。新步骤放在流程末尾；
流程以分支结尾时，放在当前可见路径的末尾。除触发器外，每个步骤都有删除按钮。两者都会给 `Steps`（双向属性）一个新列表，并触发
`StepsChanged`，参数中有新列表、变化类型和对应的步骤。

`Catalog` 存放菜单的条目（`AiWorkflowCatalogItem`，即没有 id 的步骤）。触发器只出现在空状态中。只有当流程中还没有分支时才提供分
支。目录为空时隐藏菜单。

### 只读

<Demo name="ReadOnly" />

`IsEditable="False"` 会隐藏 Add step 和删除按钮。Test run 仍然可用。

### 加载中

`IsLoading` 在流程加载时显示三个骨架步骤。在它变为 false 之前，Test run 和菜单都不可用。

### 窄布局

<Demo name="Narrow" />

卡片填满容器，最宽 640px。宽度小于 480px 时，每行增高到 44px，详情移到标题下方，状态槽从 72px 收窄到 64px，标题栏折成两行，空状
态中的触发器不再显示详情。

### 自定义文字

`Labels` 包含卡片显示或朗读的每一个词，包括屏幕阅读器名称和播报。替换整个对象即可本地化或改写：

```csharp
flow.Labels = new AiWorkflowLabels
{
    TestRun = "Probar",
    AddStep = "Añadir paso",
    Remove = title => "Quitar " + title,
    Passed = duration => "Correcto en " + duration.TotalSeconds.ToString("0.0") + " s",
};
```

### 内容准则

- 步骤标题以动词开头（"Create issue"、"Post message"）；详情说明位置（"#support"、"Support team"）。
- AI 步骤在标题中写出模型名（"Classify with Claude Sonnet 5.5"）。
- 保持流程简短：一个触发器，一到两个 AI 步骤，末尾一个分支，每条路径几个动作。

### 最佳实践

- 把 `RunStep` 接到测试端点，返回输出以便渲染预览；抛出异常即可展示带 Retry 的失败。
- 替换列表时保持步骤 id 不变，这样行和运行结果都会留在原位。
- 在展示页和营销页之外关闭 `AutoPlay`。

### 常见问题

**自动播放为什么停了？** 在卡片内第一次按下鼠标或按键后，它会永久停止，以免在别人操作时运行。卡片不在屏幕上时它也会等待。

**一个流程可以有两个分支吗？** 不可以。每个流程只有一个分支，并且是最后一个顶层步骤。更大的图请使用 `WorkflowCanvas`。

## API 参考

<!-- api: AiWorkflow -->

<!-- api: AiWorkflowStep -->

<!-- api: AiWorkflowBranchPath -->

<!-- api: AiWorkflowCatalogItem -->

<!-- api: AiWorkflowSimulation -->

<!-- api: AiWorkflowApp -->

<!-- api: AiWorkflowOutput -->

<!-- api: AiWorkflowEmailOutput -->

<!-- api: AiWorkflowLabelOutput -->

<!-- api: AiWorkflowRouteOutput -->

<!-- api: AiWorkflowMessageOutput -->

<!-- api: AiWorkflowRecordOutput -->

<!-- api: AiWorkflowTextOutput -->

<!-- api: AiWorkflowRunStepContext -->

<!-- api: AiWorkflowStepsChangedEventArgs -->

<!-- api: AiWorkflowRunCompletedEventArgs -->

<!-- api: AiWorkflowLabels -->

## 无障碍

- 卡片是一个以标题命名的分组（加载时为 "Loading workflow"）；步骤列表和每条分支路径都是有名称的列表。
- 每个步骤的名称都包含标题、应用和状态（"Create issue, Tracker, Done in 700ms"）。状态以图标加简短文字呈现，从不只靠颜色。
- 一个礼貌的实时区域会播报运行开始、每一步的结果、失败及其消息和最终结果；没有接入 `RunStep` 时会补充
  "Simulated, nothing was sent."。添加和删除步骤也会播报。
- 添加步骤菜单的搜索框始终保持焦点；它的帮助文字说明当前高亮的条目。
- 分支路径是一个使用游走焦点的单选组：只有选中的标签页是 Tab 停靠点。
- 删除按钮按步骤命名（"Remove Create issue"）。使用鼠标时，它在所在行被悬停或自身获得键盘焦点时显示；在触摸屏上始终显示。
- 所有文字都在 `AiWorkflowLabels` 中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过 Test run（或 Stop）、每个步骤的删除按钮、分支路径标签页、失败步骤上的 Retry，以及 Add step |
| Enter、Space | 开始或停止运行、删除步骤、重试失败的步骤，或打开添加步骤菜单 |
| 方向键 | 在分支标签页之间切换路径（Home 和 End 跳到两端）；菜单打开时，Up 和 Down 移动高亮 |
| 输入文字 | 按标题、应用或详情筛选添加步骤菜单 |
| Enter（菜单） | 添加高亮的步骤并关闭菜单 |
| 1 到 9（菜单） | 搜索框为空时，添加对应快捷键的步骤 |
| Escape | 关闭菜单，并把焦点还给 Add step |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 运行进入下一步 | 一个 5px 的强调色圆点在 240ms 内沿连接线滑下，身后的线随之填满 | 连接线立即填满，没有圆点 | 立即 |
| 步骤开始或结束 | 状态在固定的 72px 槽中交叉淡化（淡出 100ms，淡入 160ms）；详情交叉淡化为闪烁的活动行 | 短暂淡化，不闪烁 | 立即 |
| 输出、错误、添加或删除的步骤 | 以 0.22s 无回弹弹簧按测得的高度展开或收起，内容淡入淡出 | 立即 | 立即 |
| 理由或草稿 | 每 34ms 流式出现五个词，每块淡入；最终排版从第一帧起就预留好 | 分块出现，不淡入 | 一次全部显示 |
| 切换路径 | 列表交叉淡化（淡出 100ms，淡入 160ms），高度随之变化；标签页高亮以同一弹簧滑动 | 120ms 交叉淡化，高度立即变化 | 立即 |
| 添加步骤菜单 | 在 160ms 内淡入并就位，无回弹，120ms 内离开；筛选时列表动画到新的高度 | 淡入淡出 | 立即 |
| 自动播放 | 卡片不在屏幕上时等待，在卡片内第一次按下或按键后永久停止 | 相同 | 相同 |

在所有状态下，步骤行保持高度，状态槽保持宽度，因此运行播放时没有任何东西移位。

## 相关组件

- `AgentPlan`：智能体行动前写下的计划。
- `ToolCall`：智能体对话记录中的单个工具调用。
- `AgentStatus`：整个智能体运行的实时状态。
- `WorkflowCanvas`：大型自由形式的工作流图。
