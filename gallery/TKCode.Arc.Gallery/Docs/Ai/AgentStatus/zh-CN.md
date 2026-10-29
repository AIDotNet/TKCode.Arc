---
title: Agent status
summary: 运行中智能体的实时状态胶囊：标识位于旋转的环中，显示当前步骤与计时；按一下即展开为最近的步骤，并带有暂停、停止与审批控件。
source-hash: 6d0078b450f9
---

<Demo name="Preview" />

## 何时使用

`AgentStatus` 是回答"智能体是否仍在工作，它正在做什么？"这一问题的最简洁、最诚实的方式。它是一个胶囊，放在页眉、标题栏或输入框上方：智能体的标识位于一圈细环中，工作时环会转动；旁边是当前活动及其图标，以及本次运行已经历的时间。按一下，同一个胶囊就地展开为一个浮动面板，其中有最近的步骤、分段进度条，以及暂停、停止、批准或重试的控件。它下方的内容纹丝不动。

- **需要一眼看到运行情况的页眉或标题栏**：智能体此刻在做什么、已经运行了多久，而不必让日志占满页面。
- **审批关卡**：等待人工确认的命令、部署或邮件。胶囊会变成一个请求，并直接带有拒绝与批准按钮，因此在应用的任何位置一按即可作答。
- **偶尔才查看的后台运行**：胶囊保持小巧；只有当有人想看时，面板才显示最近几步和控件。
- **失败且可重试的运行**：胶囊的环会收拢成一个叉，说明原因，并提供重试。
- **一个应用中的多个智能体**：每个页眉槽位都可以放一个自己的胶囊；空闲的胶囊不占用任何动画帧。

## 何时不用

- **运行本身就是页面**：包含每个步骤、工具调用与输出的记录需要足够的空间。请在聊天线程中使用 `ToolCall` 行，或使用完整的运行视图；胶囊的面板只列出最近的步骤。
- **跨多个智能体的审批队列**：请构建一个请求列表；一个胶囊只显示一个智能体的运行。
- **运行前由用户编辑的计划**：使用 `AgentPlan`，它可以重新排序并编辑步骤。
- **在同一时间轴上比较多个智能体**：使用 `AgentTimeline`。
- **一个闪烁的单词，没有计时器也没有控件**：使用 `TextShimmer`。

## 用法

未设置任何内容时，胶囊会自行播放一次示例运行：一个虚构的编码智能体依次完成六个步骤，并停在一个等待多久都可以的审批上。上方的预览还加了一个开关，可切换到第二个示例（研究智能体，其第三步会失败一次，以便看到重试），以及一个 Replay 按钮；这两个控件属于演示本身，而不属于胶囊。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentStatus />
</UserControl>
```

`AgentStatus` 派生自 Avalonia 的 `TemplatedControl`。它位于 Special 包中：请在 `UseArcControls()` 旁调用 `UseArcSpecial()` 进行注册。

在应用中，请根据智能体的事件驱动它。设置 `Status`，让 `Activity` 保持为最新的工具调用并使用现在时描述，列出 `Steps`，为整个运行提供一个始终不变的 `StartedAt`，并处理各个操作事件。胶囊有数据时从不自行更改状态：按下 Approve 会引发 `ApproveRequested`，由你的代码决定接下来做什么。

```xml
<arc:AgentStatus Status="{Binding Run.Status}"
                 Activity="{Binding Run.Activity}"
                 StartedAt="{Binding Run.StartedAt}"
                 Agent="{Binding Run.Agent}" />
```

每个操作都是带有类型化参数的路由事件（`PauseRequested`、`ResumeRequested`、`StopRequested`、`ApproveRequested`、`DenyRequested`、`RetryRequested`、`OpenRunRequested`）。

```csharp
status.PauseRequested += (_, e) => agent.Pause(run.Id);
status.ApproveRequested += (_, e) => agent.Approve(run.Id);
```

```csharp
status.Steps.Add(new AgentStatusStep("scan", "Scanned 9 modules", AgentStatusStepState.Done, ArcIcons.Search)
{
    Duration = TimeSpan.FromMilliseconds(1917)
});
status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, progressValue: 8, progressTotal: 12);
```

### 每种状态

胶囊有七种状态。每种状态都用环与标识配合文字呈现，因此状态从不只依赖颜色。按一下胶囊即可打开其面板。

<Demo name="States" />

| 状态 | 环与标识 | 文字 | 计时 | 按钮 |
|---|---|---|---|---|
| `Idle` | 只有标识，没有环 | 次要颜色的 "Ready" | 隐藏 | 面板中的 Open run |
| `Working` | 一条轨道和一段短弧，每 0.9 s 转一圈 | 活动及其图标；文字闪烁 | 计时 | 面板中的 Pause 与 Stop |
| `NeedsInput` | 弧线消失；一个强调色圆点弹出，伴随一次脉冲 | 文本颜色的请求内容 | 继续计时 | 行内的 Deny 与 Approve；面板中的 Approve 与 Deny |
| `Paused` | 弧线冻结并变为灰色，标识变暗 | 带暂停图标的 "Paused" | 冻结 | 面板中的 Resume 与 Stop |
| `Done` | 环以成功色闭合，标识变为绘制出的对勾 | "Done in 2m 23s · 5 files touched" | 隐藏 | Open run |
| `Failed` | 环以危险色闭合，并围绕一个叉 | 失败原因 | 隐藏 | 行内与面板中的 Retry |
| `Stopped` | 没有环；一个小方块取代标识 | 次要颜色的 "Stopped after 7s" | 隐藏 | Open run |

已完成的运行（`Done` 或 `Stopped`）会在 `SettleAfter` 之后（默认 4 秒；设为 null 时结果会一直保留在屏幕上）恢复为 "Ready"。面板保留上一次运行的步骤与计数器，并会引发 `Settled`。

### 结构

1. **标识圈**：一个 24 px 的圆盘。智能体的标识以 12 px 绘制在 1.5 px 环的中央。工作时环为轨道加一段短弧；完成或失败时为闭合的圆；空闲或已停止时则什么都没有。
2. **文字**：一个 16 px 图标加一行 14 px 文字。新的活动从下方滚入，旧的活动向上滑出。诸如 "8 of 12" 之类的计数会在原位更换数字，并参与闪烁。当文字比可用空间宽时，其右边缘会在 24 px 内淡出，而不是换行。
3. **计时**：以等宽数字显示的分钟与秒（如 "2:20"），在运行进行中显示。
4. **行内按钮**：需要输入时显示 Deny 与 Approve，失败后显示 Retry，高 30 px。
5. **面板**：从胶囊打开。一行显示智能体名称以及 "Step 3 of 6"（运行结束后为 "6 of 6 steps"），一个进度条（每个步骤一段，每段 3 px），最近五个步骤（带图标、可换行的标签与时间），以及带按钮的页脚。

### 面板

面板从胶囊自身的表面生长出来：宽度与高度以弹簧方式同时变化，页眉行保持原位，内容稍后淡入。它浮在窗口的覆盖层中，因此剪裁子元素的页眉或滚动容器都无法遮挡它，下方的内容也不会移动。它的宽度为 384 px 或与胶囊等宽（取较大者），且永远不会超过容器的宽度。

`PanelAlignment` 决定面板保持哪一边不动：`Start` 适用于位于页眉起始端的胶囊，`End` 适用于位于末端的胶囊，`Center`（默认）则向两侧均匀展开。

<Demo name="HeaderBar" />

`IsExpanded` 是双向的。按下胶囊、按 Escape 以及在外部按下，都会改变它；也可以在代码中设置它，或将其绑定到你自己的控件以打开面板。`ExpandedChanged` 报告每一次变化。`Expand()`、`Collapse()` 与 `Toggle()` 可以在代码中执行相同的操作。

```xml
<arc:AgentStatus IsExpanded="{Binding IsRunOpen, Mode=TwoWay}" PanelAlignment="End" />
```

面板列出最近的 `MaxSteps` 个（默认五个）未处于待定状态的步骤。待定的步骤仍会计入进度条。新步骤会在底部淡入，较旧的步骤向上滑动，最旧的步骤在顶部淡出。步骤通过 `Id` 匹配，因此从 "Running tests" 变为 "Passed 12 of 12 tests" 的步骤会保留其行：旧文字淡出，新文字接替其位置。

### 你自己的运行

步骤运行时使用现在时，完成后使用过去时，让文字本身体现状态。步骤的 `StartedAt` 会让其运行时间以整秒计算；已完成的步骤显示其 `Duration`，保留一位小数（如 "1.9s"）。失败和跳过的步骤不显示时间。

计时器从 `StartedAt` 开始计算，并扣除暂停或自行失败所花费的时间。请一次性给出运行的真实开始时间并保持不变；如果后端知道结束时间，请提供 `EndedAt`，否则将使用状态变为 done 或 stopped 的时刻。

<Demo name="HostDriven" />

```csharp
pill.ApproveRequested += (_, e) =>
{
    agent.Approve(run.Id);          // your backend
    pill.Activity = new AgentStatusActivity("Deploying build 2.4.1", ArcIcons.CloudUpload);
    pill.Status = AgentStatusRunState.Working;
};
```

`Notice` 是供屏幕阅读器播报的一次性消息，例如某个操作的结果；它会加入下一次播报。胶囊显示或播报的每一个文字都来自 `Labels`（一个 `AgentStatusLabels`），包括带数字的句子，因此可以翻译或改写。

### 只提供能够兑现的操作

只有当 `Actions` 允许某个操作，且运行状态需要它时，按钮才会显示：工作中显示 Pause，暂停时显示 Resume，工作中或暂停时显示 Stop，需要输入时显示 Approve 与 Deny，失败后显示 Retry；在任何状态下都显示 Open run。`Actions` 是一个标志位值，默认包含全部操作。它与"仅当处理程序存在时才显示控件"的做法相对应；请将其设置为你的应用真正支持的操作。

<Demo name="Actions" />

```xml
<arc:AgentStatus Actions="Approve, Deny, OpenRun" />
```

页脚前两个按钮在状态变化时保持原位：Pause 变为 Resume，Approve 变为 Pause，Stop 变为 Deny。每次都是同一个按钮，因此键盘焦点得以保留。当获得焦点的按钮永久消失时（例如运行结束后的 Pause），焦点会移到胶囊上，而不会移到页面上。Open run 会以一个对勾和文字 "Opened" 确认，持续约 1.6 秒。

### 模拟

示例运行不属于控件的一部分。`AgentRunSimulation` 是一个没有界面的驱动器，它按计时器播放一个 `AgentRunScenario`：列出步骤、推进计数、无限期等待审批或失败，并将 Pause、Resume、Stop、Approve、Deny、Retry 与 Open run 应用到它自己的状态上。把它交给 `AgentStatus.Simulation`，胶囊就会显示它并把按钮操作路由回去；你也可以自行读取它的 `Status`、`Activity`、`Steps` 与 `Notice`，以驱动其他任何视图。

```csharp
var simulation = new AgentRunSimulation(AgentStatusSamples.Research);
pill.Simulation = simulation;
simulation.Replay();                                   // clears the run and starts again after 0.5 s
simulation.Scenario = AgentStatusSamples.Coding;       // starts over after 0.9 s
```

开始延迟为 0.9 秒（`StartDelay`），`Replay()` 之后为 0.5 秒（`ReplayDelay`）。已完成或已停止的运行会保持原样，直到胶囊将其结算。计时器运行在 `TimeProvider` 上，因此测试可以用手动时钟推进运行；胶囊自身也有一个 `TimeProvider` 属性，用于计时和结算延迟。

`AgentStatusSamples` 包含两个虚构的场景、它们的智能体（`CodingAgent`、`ResearchAgent`）以及两种活动。你可以用 `AgentRunScenarioStep` 构建自己的 `AgentRunScenario`：每个步骤都有一个现在时的标签、一个过去时的标签、一个图标、一段时长、一个可选的计数、一个可选的审批关卡，以及首次尝试时可选的失败。

### 窄容器

胶囊跟随其容器的宽度，而不是窗口的宽度，并且永远不会超出它。当可用宽度低于 400 px 时，行内的 Deny、Approve 与 Retry 会变成 30 px 的图标按钮，并带有无障碍名称；这些按钮显示时计时器会让位，面板中的 Open run 也会变成图标按钮。过长的文字在其右边缘淡出，面板中的步骤标签则会换行，而不是被截断。

<Demo name="Narrow" />

### 减弱动效

在减弱动效下，每种状态、文字、焦点与播报都保持不变。宽度与面板会立即改变，新的文字原地淡入而不滚动，环不转动，圆点不脉冲，对勾与闭合的环以绘制完成的状态直接出现，工作中的文字也不闪烁。

<Demo name="ReducedMotion" />

### 内容准则

让胶囊的文字保持简短；历史记录由面板承载。活动应写成智能体此刻正在做的事（如 "Reading 12 files"），请求应写成一个提问的句子（如 "Waiting on you to deploy to staging"），失败应写成出了什么问题（如 "Fetch failed · 2 pages did not load"），结果则写成不带时间动词的结论（如 "5 files touched"）：胶囊会写成 "Done in 1m 12s · 5 files touched"。

| 避免 | 改为 |
|---|---|
| Processing | Checking specs |
| The agent is currently reading the files of the project | Reading 12 files |
| Waiting | Waiting on you to deploy to staging |
| Error | Fetch failed · 2 pages did not load |
| Run complete! All done successfully | 5 files touched |

### 最佳实践

**建议**

- 为每个审批关卡（命令、推送、付款、邮件）使用 `NeedsInput`，并将文字写成请求本身。
- 为整个运行保持 `StartedAt` 不变，并让胶囊扣除暂停与失败的时间。
- 在 `Actions` 中只传入你的后端支持的操作。
- 将胶囊放在一个会设定其宽度的容器中，例如页眉槽位，这样它才能淡出过长的文字并确定面板的尺寸。
- 通过 `AgentModel.MarkBrush` 为标识赋予品牌色；默认标识以文字颜色绘制。

**避免**

- 把它当作日志使用。它只列出最近的步骤，其余的都会被丢弃。
- 每次计数跳动都重写 `Activity`。只需更改计数（`new AgentStatusActivity(label, icon, 9, 12)`）：数字会原地更换，屏幕阅读器也不会被打断。
- 在标签中放入很长的句子。它会在边缘淡出，完整文字则保留在胶囊的无障碍名称中。
- 同一屏幕上放置多个同一运行的胶囊。

### 常见问题

**为什么面板不会把页面向下推？** 它浮在窗口覆盖层中、位于页面之上，从胶囊的占位处开始展开。胶囊在布局中保持原位。

**带有 `ClipToBounds` 的页眉能容纳胶囊吗？** 能。收起的胶囊位于页眉之内；展开的面板则不在其中。

**如何为已经开始的运行显示计时器？** 将 `StartedAt` 设为真实的开始时间，胶囊会从那里开始计时。

**如果同时设置了 `Status` 和 `Simulation` 会怎样？** 由模拟驱动胶囊，`Status` 会被忽略。

**为什么计数不像新活动那样滚动？** 只有新的文字和图标会滚动。计数是原地更改的，因此它可以每秒跳动好几次。

## API 参考

<!-- api: AgentStatus -->

<!-- api: AgentStatusActivity -->

<!-- api: AgentStatusStep -->

<!-- api: AgentStatusLabels -->

<!-- api: AgentStatusActionEventArgs -->

<!-- api: AgentStatusExpandedChangedEventArgs -->

<!-- api: AgentRunSimulation -->

<!-- api: AgentRunScenario -->

<!-- api: AgentRunScenarioStep -->

<!-- api: AgentStatusSamples -->

枚举如下：

| 类型 | 取值 |
|---|---|
| `AgentStatusRunState` | `Idle`、`Working`、`NeedsInput`、`Paused`、`Done`、`Failed`、`Stopped` |
| `AgentStatusStepState` | `Pending`、`Active`、`Waiting`、`Done`、`Failed`、`Skipped` |
| `AgentStatusActions` | `None`、`Pause`、`Resume`、`Stop`、`Approve`、`Deny`、`Retry`、`OpenRun`、`All`（标志位） |
| `AgentStatusPanelAlignment` | `Start`、`Center`（默认）、`End` |

智能体是一个 `AgentModel`（名称、一个 24 乘 24 的描边标识及其画刷），与其他智能体组件共享。

## 无障碍

- 胶囊是一个带有展开状态的按钮。它的名称会读出智能体、带计数的活动以及已经过的时间（如 "Juniper Code 3: Checking specs 8 of 12, 0:09 elapsed"）；运行完成后则读出其结果。
- 礼貌级别的实时区域会播报每个新的活动和每个结果（如 "Juniper Code 3: done in 14 seconds, 5 files touched"），以及通过 `Notice` 传出的操作结果。它不播报计数或计时器的跳动。
- 打开的面板是一个名为 "{agent} run details" 的区域；进度条是一个名为 "Run progress" 的进度条，其值如 "4 of 6 steps finished"。
- 状态从不只依赖颜色：每种状态都有一个文字（"Ready"、"Paused"、"Done in"、"Stopped after"）以及不同的环或图标；步骤在运行时使用现在时，完成后使用过去时。
- 窄容器中的图标按钮都有无障碍名称。被截断的文字在胶囊名称和面板的步骤列表中保持完整。
- 键盘焦点显示 2 px 的环，在胶囊上向内绘制，在按钮之外则向外绘制；指针焦点不显示任何环。
- 焦点会被保留：改变角色的按钮保持焦点；当获得焦点的控件消失时，焦点移到胶囊上。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 先移到胶囊，再移到行内的 Deny 与 Approve 或 Retry。面板展开时：Pause 或 Resume、Stop 或 Deny、Open run，然后继续移到页面中的下一个控件 |
| 胶囊上的 `Enter` / `Space` | 打开或关闭面板 |
| 按钮上的 `Enter` / `Space` | 按下该按钮 |
| `Escape` | 在展开的面板内的任何位置，关闭面板并将焦点返回到胶囊 |

在展开的面板外按下会关闭它，焦点停留在按下的位置。该面板不是模态的，也不会锁定焦点。

## 动效

- 新的活动从下方 12 px 处以 180 ms 滚入，旧的活动则以 140 ms 向上滚出；胶囊的宽度以无回弹的弹簧跟随变化，文字比可用空间宽时，其右边缘会淡出。
- 面板从胶囊的占位处以弹簧方式同时改变宽度与高度（约 0.75 秒稳定）；内容在 80 ms 后开始，以 200 ms 淡入，关闭时则以 75 ms 淡出。
- 工作时环每 0.9 s 转一圈；暂停时在原位冻结，并从同一角度继续。需要输入时，弧线消失，圆点以一次 0.9 s 的脉冲弹出。运行结束时，环在 0.44 s 内绘制闭合，同时标识缩小消失，对勾（或叉）在短暂停顿后的 0.28 s 内绘制出来。
- 工作中的文字带有闪烁效果，循环为 2.4 s（1.7 s 扫过，然后停顿）；计数与活动步骤的文字随之闪烁。
- 新步骤在 155 ms 内淡入，旧步骤沿平滑弹簧向上滑动，进度段在 480 ms 内从左侧填充。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新活动 | 滚动并淡入 | 原地淡入 100 ms | 直接替换 |
| 胶囊宽度与面板尺寸 | 弹簧变化 | 立即 | 立即 |
| 环、闪烁、脉冲 | 有动画 | 静止 | 静止 |
| 完成与失败 | 环与对勾绘制 | 以绘制完成的状态显示，短暂淡入 | 直接显示为已绘制 |
| 计时器与计数 | 原地变化 | 原地变化 | 原地变化 |

## 相关组件

- `ToolCall`：用于记录中的单次调用，附带其输入与输出。
- `AgentPlan`：智能体将执行的步骤，运行前可编辑。
- `AgentTimeline`：在同一时间轴上回放多个智能体。
- `TextShimmer`：单个闪烁的文字。

| 组件 | 何时选用 |
|---|---|
| `AgentStatus` | 需要在页眉中显示一个智能体的运行，并可按需展开 |
| `ToolCall` | 你在记录中列出调用 |
| `AgentPlan` | 用户需要先编辑并批准步骤 |
| `AgentTimeline` | 多个智能体与交接共享同一时间轴 |
| `Alert` | 你以文字报告真实作业的状态 |
