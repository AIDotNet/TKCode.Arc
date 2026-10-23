---
title: Agent plan
summary: 将智能体的计划呈现为可编辑的清单：草稿阶段可以重排、改名和添加步骤，批准后则逐步跟随运行过程。
source-hash: 0e89f7dff7a5
---

<Demo name="Preview" />

## 何时使用

`AgentPlan` 是智能体在"这是我将要做的事"与"已完成"之间展示的卡片。计划仍为草稿时，人会阅读其中的步骤并加以修改：原地重命名标题、将步骤拖到另一个位置、删除一个步骤、添加一个步骤，或请智能体再补充一个步骤。批准后计划即被锁定，同一张卡片会变成实时清单，逐步跟随智能体的执行，显示每一步所用的时间、活动行的位置，以及某一步失败时需要做出的决定。

- **编码或运维智能体的计划模式**：智能体提出步骤，人编辑并批准，卡片随后跟踪工作进展。批准之前不会运行任何内容。
- **跟踪多步任务的侧边面板或聊天消息**：显示当前的一个步骤，已完成的步骤回落为安静的文字；底部的页脚以总耗时和一行摘要收尾。
- **可能中途失败的工作**：失败的步骤会停止运行，并显示失败原因以及 Retry 和 Skip，而不是把失败隐藏在日志里。
- **在页面或演示中体验完整流程**：在没有数据且没有状态时，卡片会在内置的模拟中播放一份虚构的计划，因此无需智能体即可看到每一种状态。

## 何时不用

- **记录智能体做过的全部事情**：使用 `ChatThread` 或日志视图。计划是一份简短的有序意图清单，而不是工具调用及其输出的记录。
- **逐个批准有风险的单独操作**：计划只需在开始前批准一次。当人需要逐案决定时，请为每个操作单独提出请求。
- **多个智能体的独立任务**：这里是一份有序的计划。队列请使用任务列表。
- **入职检查清单**：这里的步骤属于智能体，人无法勾选，而且它们只会随着智能体的工作向前推进。
- **汇报单个任务的状态**：使用 `Alert`、`Badge` 或表格。只有一个步骤的计划说明不了什么。

## 用法

在没有 `Steps` 也没有 `Status` 时，卡片会显示一份虚构的草稿（为帮助中心添加全文搜索），并在批准后运行内置的模拟。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentPlan MaxWidth="560" />
</UserControl>
```

`AgentPlan` 派生自 Avalonia 的 `TemplatedControl`，位于 Special 包中；请在 `UseArc()` 旁调用 `UseArcSpecial()` 以注册它。卡片的宽度与其容器相同，最宽为 560 px，在约 320 px 时仍然易读，并在小于 420 px 时收紧布局。

### 结构

1. **标题**：模型的标志、"Plan · 6 steps"（添加或删除步骤时数字会滚动变化），以及其下方的目标。计划为草稿时，右侧显示模型名称；运行后，右侧改为计数 "3 of 6"。
2. **进度线**：位于标题底部分隔线上的 2 px 线条，随已完成的步骤增长。草稿阶段隐藏。
3. **步骤**：一个带编号的列表。每一行都有固定的前导列（拖动手柄，之后为状态标记）、标题，以及固定的尾随列（删除按钮，之后为时间或文字 Skipped）。计划批准后各列不会变化，因此标题的换行方式保持一致，也不会移动。
4. **添加步骤**；列表为空时，会有一句话说明这一点。
5. **页脚**：草稿阶段为 Keep planning 和 Approve plan；运行期间为状态以及 Pause 或 Resume；完成后为总时间、摘要和 Start over。

六种状态标记各有自己的形状，因此状态不只依赖颜色：空心圆环表示待处理（pending），带四分之一弧线的圆环每 800ms 转动一圈表示进行中（in progress），带两条竖杠的圆环表示已暂停（paused），绿色圆盘中一个自行描绘出来的对勾表示已完成（done），红色圆盘中的感叹号表示失败（failed），虚线圆环表示已跳过（skipped）。

### 编辑草稿

每个步骤的标题都是一个会随文字增长的文本区域。

- **Enter** 保存改名并保持焦点。**Escape** 撤销自该字段获得焦点以来（或自上一次 Enter 以来）的编辑。离开该字段时会保存。
- 在空标题中按 **Backspace** 会删除该步骤，并把插入符移到上一步的末尾；在第一个步骤中，插入符则移到下一步的开头。离开一个被清空的字段时，同样会删除该步骤。
- 在标题中按 **Alt+Up** 和 **Alt+Down** 会将步骤移动一个位置；插入符保持原位。
- 拖动手柄可用指针移动步骤：该行会抬起并跟随指针，一旦越过相邻步骤高度的一半，便与该相邻步骤交换位置；松开后该行落定。让手柄获得焦点后按 **Up** 或 **Down**，即可不用指针移动它。
- 删除按钮在该行被悬停或拥有焦点时显示。
- **Add step** 会追加一个空步骤，并将焦点放在其标题上。没有任何步骤时，Approve plan 被禁用；Keep planning 仍然可用。

批准时会去除标题两端的空白并丢弃空步骤。Keep planning 会请智能体再补充一个步骤：按钮中会显示旋转指示器，随后建议的步骤被插入到最后一个步骤之前，其标题每次以几个词的形式流式出现。完成后，它就是一个普通的可编辑步骤，并带有 "Suggested" 备注。

### 受控计划

设置 `Status` 后，计划即为受控状态：控件从不更改状态，也不会改动步骤的状态、活动行、错误或时间。每一项操作都通过事件上报，由你更新数据。

| 事件 | 触发时机 | 你通常会 |
|---|---|---|
| `Approved` | Approve plan，附带按顺序排列、已去除空白且不含空步骤的步骤 | 启动智能体，并将 `Status` 设为 Running |
| `Changed` | 草稿编辑：改名、移动、删除、添加，或流式输出的建议（`Kind`） | 让你保存的草稿副本与之保持一致 |
| `KeepPlanningRequested` | Keep planning | 设置 `IsPlanning`，请求智能体，并调用 `StreamSuggestion` |
| `PauseChanged` | Pause（true）或 Resume（false） | 暂停智能体，并将 `Status` 设为 Paused 或 Running |
| `StepAction` | 失败步骤上的 Retry 或 Skip（`Kind`、`Step`） | 重新运行该步骤或跳过它，并更新其状态 |
| `StartOverRequested` | 完成后的 Start over | 重置步骤，并将 `Status` 设回 Draft |

草稿的编辑直接在 `Steps` 本身中进行（控件在那里改名、移动、删除和添加），并通过 `Changed` 上报；只有运行过程由你负责。

<Demo name="Controlled" />

```csharp
plan.Approved += (_, e) =>
{
    foreach (var step in plan.Steps) { step.State = AgentPlanStepState.Pending; }
    plan.Status = AgentPlanStatus.Running;
    agent.Start(e.Steps);
};
plan.StepAction += (_, e) => agent.Decide(e.Step, e.Kind);
```

智能体工作时，你修改步骤，控件便随之更新：每次只将一个步骤设为 `Active`，在它上报时追加到其 `Activities`，然后以 `Duration` 设为 `Done`。对于失败，先设置 `Error`，再设为 `Failed`，这样公告可以读出失败原因。若要插入一个步骤，请在合适的位置设置 `NoteKind = AddedByAgent`，它便会滑入，并带有淡淡的强调色底纹。在状态变为 Done 之前设置 `Summary`，它会在 "Done in …" 之后流式出现。

### 运行中途的计划

同样的数据也可以驱动一个已经在运行的计划。这个计划有两个已完成的步骤、一个由智能体添加的步骤，以及一个等待决定的失败步骤。

<Demo name="MidRun" />

失败的步骤会显示 Failed 一词、失败原因，以及 Retry 和 Skip。页脚会说明是哪一步失败，同时 Pause 按钮让位。键盘焦点会移到 Retry（否则 Pause 消失时焦点会落回页面）；在 Retry 或 Skip 之后，焦点会回到 Pause。

### 模拟

`AgentPlanSimulation` 是内置的智能体，在 `Status` 为 null 时运行。它由事件驱动：每次状态变化只等待一个计时器，计划空闲时不运行任何内容；Pause 会保留剩余时间，Resume 则从那里继续。它响应的按钮与宿主相同。其节拍依据测得的流程（1× 速度）：

| 节拍 | 时间 |
|---|---|
| Approve plan → 第一个步骤开始 | 280ms |
| 步骤开始 → 其第一条活动行 | 372ms |
| 两条活动行之间 | 540ms |
| 最后一行 → 步骤完成 | 470ms |
| 步骤完成 → 下一个步骤开始 | 290ms |
| 最后一个步骤完成 → 计划完成 | 420ms |
| Retry → 第一条新行，然后 577ms，再 465ms → 完成 | 555ms |
| Skip → 下一个步骤开始 | 277ms |

在示例中，智能体会在第三个步骤的第二行之后（417ms 后）插入一个步骤，而测试步骤在其最后一行之后失败一次，然后无限期地等待 Retry 或 Skip。没有脚本的步骤（即你添加的步骤）会运行 `GenericScript`：三条活动行和 30 秒。脚本通过步骤 id 查找，因此改名后的步骤会保留其脚本，被删除的步骤则会被跳过。Start over 会回到运行开始时的草稿，并让所有建议重新可用。

将你自己的步骤、建议和摘要交给模拟，即可演示另一个流程：

<Demo name="Labels" />

```csharp
plan.Simulation = new AgentPlanSimulation
{
    Steps = [new("a", "Find every link to the old page", new(["Found 14 links"], TimeSpan.FromSeconds(9)))],
    Suggestions = [],
    Summary = "The page now lives at /preferences."
};
```

### 时间与计数

步骤时间在一分钟以内显示整秒（"23s"），满一分钟后显示为分钟加两位秒数（"1m 07s"）。总时间是已完成步骤时间的总和，因此跳过和失败的步骤不计入。标题显示 "{n} of {count}"，其中 n 是第一个未完成且未跳过的步骤的序号；进度线则是已完成与已跳过的步骤数除以全部步骤数。计数在预留的宽度内以等宽数字滚动，因此数字变化时不会推动周围的文字。

### 窄容器

宽度小于 420 px 时，标题、列表和页脚都会收紧，草稿的两个按钮平分一行。标题会按需要换行到任意行数；各列保持不变。

<Demo name="Narrow" />

### 减弱动效

减弱动效和关闭动效时，每个状态、标签、焦点移动和公告都保留，只去掉所有会移动的效果：行、活动行、失败块和页脚都会立即改变高度，弧线不再转动，对勾直接以完整形态出现，闪烁静止，插入的步骤没有底纹，建议的词语则一起出现。模拟运行同样的时间线。

### 内容准则

将标题写成约 6 到 10 个词的指令，活动行则写成简短的过去时短语，以计划认识的动词开头（`AgentPlanActivityIcons`）：Read、Searched、Found、Created、Edited、Ran 或 Measured。可以用 `AgentPlanActivityIcons.Set` 注册更多动词。

| 避免 | 改为 |
|---|---|
| Step 1 | Find every link to the old settings page |
| Doing the search | Searched for /settings |
| Tests | Ran 6 tests |
| Something went wrong | 3 of 14 tests failed: typo matching skips two-letter words |
| Finished! | The page now lives at /preferences, and the old address redirects. |

### 最佳实践

**建议**

- 将计划控制在四到八个步骤。更长的计划就是一个项目，应当拆分。
- 用智能体自己的话说明失败原因，并提供 Retry 和 Skip，让人可以自行决定。
- 为模型提供它自己的标志，使智能体的注释读起来像是智能体本身的。
- 重命名或重新排序时保持 `Id` 稳定；模拟、公告以及你自己的日志都依赖它。

**避免**

- 在有人正在编辑草稿时，来回改变 `Status`。
- 把计划当作日志使用。活动行应是每个步骤中的少量简短事实，并在该步骤完成时收起。
- 为模型使用品牌标志。请使用通用图标，并在文字中写出模型名称。

### 常见问题

**计划会运行任何东西吗？** 不会。它只显示计划，在没有 `Status` 时播放一段虚构的模拟。真正的工作由你负责：响应 `Approved` 并更新步骤。

**如何展示智能体添加的步骤？** 在计划运行时插入一个 `AgentPlanStep`，并设置 `NoteKind = AgentPlanNoteKind.AddedByAgent`。它会滑入，带有底纹和备注，并且会被公告。

**运行时人能编辑吗？** 不能。计划批准后，行就固定下来；唯一的控件是 Pause 或 Resume、失败时的 Retry 和 Skip，以及结束时的 Start over。

**如果我没有模型怎么办？** 将 `Model` 保持为 null（并使用你自己的步骤）：标题不会显示标志和名称。

**为什么 Approve plan 被禁用？** 草稿没有任何步骤。请添加一个，或让智能体继续补充计划。

## API 参考

<!-- api: AgentPlan -->

<!-- api: AgentPlanStep -->

<!-- api: AgentPlanLabels -->

<!-- api: AgentPlanSimulation -->

<!-- api: AgentPlanStepScript -->

<!-- api: AgentPlanScriptedStep -->

<!-- api: AgentPlanApprovedEventArgs -->

<!-- api: AgentPlanChangedEventArgs -->

<!-- api: AgentPlanStepActionEventArgs -->

<!-- api: AgentPlanPauseChangedEventArgs -->

<!-- api: AgentModel -->

枚举如下：

| 类型 | 取值 |
|---|---|
| `AgentPlanStatus` | `Draft`、`Running`、`Paused`、`Done` |
| `AgentPlanStepState` | `Pending`、`Active`、`Done`、`Failed`、`Skipped` |
| `AgentPlanNoteKind` | `None`、`Suggested`、`AddedByAgent` |
| `AgentPlanChangeKind` | `Rename`、`Move`、`Remove`、`Add`、`SuggestionStreamed` |
| `AgentPlanStepActionKind` | `Retry`、`Skip` |

`AgentPlanSamples` 保存虚构的示例（`Steps`、`Suggestions`、`GenericScript`、`Goal`、`Summary`、`CreateModel()`、`CreateSteps()`），因此页面可以从它开始，只修改其中一个步骤。

## 无障碍

- 卡片是一个以模型名称和目标命名的分组（"Lumen 2 Pro plan: Add full-text search to the help center"）。步骤是一个有序列表，因此屏幕阅读器会读出总数以及每个位置。
- 计划运行时，每个步骤在标题之前都会以文字播报其状态（Pending、In progress、Done、Failed、Skipped）。每种状态还有自己的形状，因此不只依赖颜色。
- 失败的步骤会读出 Failed 一词、失败原因，以及两个带标签的按钮（Retry 和 Skip，各自以步骤名称命名）。
- 标题是一个带标签的文本区域（"Step 3"），并由一条提示描述，提示中说明 Alt 加方向键以及 Escape。手柄和删除按钮以其位置和步骤命名（"Move step 3: …"）。
- 进度线是一个带文字数值的进度条（"2 of 6 steps finished"）；计划为草稿时，它对辅助技术隐藏。
- 一个礼貌级别的状态区域会播报编辑、批准（以及运行是否为模拟）、每个完成的步骤、插入的步骤、失败、重试、跳过、暂停和继续、最终摘要以及重置。用指针拖动时不做任何播报。
- 焦点跟随操作：被移动的步骤保留焦点；被删除的步骤将焦点交给相邻步骤（若是最后一个，则交给 Add step）；Add step 将焦点放在新标题上；Approve plan 把焦点交给 Pause；失败时焦点移到 Retry；Retry 和 Skip 把焦点交还给 Pause；Done 时焦点移到 Start over，Start over 时则移到 Approve plan。
- 键盘焦点在控件上显示 2 px 焦点环（位于行内的控件则为内嵌显示）；指针焦点不显示。标题则改为显示一条柔和的内嵌线条。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 草稿中：每个步骤的手柄、标题和删除按钮，然后是 Add step、Keep planning 和 Approve plan。运行中：失败步骤上的 Retry 和 Skip，以及 Pause 或 Resume。完成后：Start over |
| 标题中的 `Alt+Up` / `Alt+Down` | 将步骤移动一个位置；插入符保持不变 |
| 手柄上的 `Up` / `Down` | 将步骤移动一个位置；到达两端时无任何动作 |
| 标题中的 `Enter` | 保存改名并停留在该字段中 |
| 标题中的 `Escape` | 撤销自该字段获得焦点以来或自上次保存以来的编辑 |
| 空标题中的 `Backspace` | 删除该步骤并聚焦上一步 |
| 手柄上的 `Space`、`Enter`、`Home`、`End` | 无任何动作：没有拾起模式 |

## 动效

- **所有大小或位置变化都使用同一种弹簧**：临界阻尼，约 260ms，无超调。插入、删除或移动步骤时，行会滑动并伸缩，被拖动行的相邻行会让开，活动行或失败块展开，页脚和进度线随之跟随。被放下的行以稍快的弹簧落定。
- 步骤变为完成时，其对勾以描边方式绘制（45ms 后开始，历时 200ms），同时圆盘淡入，标题从主要文字色过渡为次要文字色（240ms），用时间淡入。其活动行在同一时刻收起。
- 当前步骤的标题会闪烁（1.8 秒一个来回，之后短暂静止），与转动的弧线相伴。暂停会同时停止两者。
- 建议的文字以每段三到五个词、每 34ms 一段的方式流入，每段只做淡入；智能体插入的步骤以 8% 强调色底纹出现，底纹保持约 640ms，然后在约 1.2s 内淡出。
- 计数以等宽数字在预留宽度内滚动。标题右侧、页脚的行以及状态标记都以一次短暂的交叉淡化切换（旧内容 60ms 内淡出，新内容则在约 145ms 内淡入并上移 3px）。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 高度与位置 | 弹簧 | 立即 | 立即 |
| 状态切换 | 带 3px 上升的交叉淡化 | 短暂的交叉淡化 | 立即 |
| 加载指示器与闪烁 | 转动并扫过 | 静止 | 静止 |
| 对勾 | 200ms 内描绘 | 直接显示为完成 | 直接显示为完成 |
| 插入的步骤 | 带底纹滑入 | 直接出现 | 直接出现 |
| 流式输出的建议 | 词块淡入 | 所有词一次出现 | 所有词一次出现 |
| 模拟的时间线 | 不变 | 不变 | 不变 |

## 相关组件

- `AgentStatus`：用于紧凑地显示一个正在运行的智能体的实时状态。
- `ToolCall`：用于单个工具调用，包括其输入、输出和审批。
- `AgentTimeline`：用于编排运行的时间轴。
- `ContextWindow`：用于显示一次运行占用了模型多少上下文。
- `ChatThread`：当你展示对话本身时使用。
