---
title: Agent timeline
summary: 在同一时间轴上回放一次多智能体运行：每个智能体的思考、工具调用与等待，它们之间的交接，以及时间、token 与费用。
source-hash: 5696d9478d3e
---

<Demo name="Preview" />

## 何时使用

`AgentTimeline` 以紧凑的甘特图展示一次由多个 AI 智能体编排完成的运行。编排器位于顶行，每个子智能体在其下方占一行，行首有颜色圆点、名称，以及它所用的模型。共享时间轴上的时间段说明每个智能体做了什么、何时做的：思考是智能体颜色的淡色调，工具调用为实心，等待则为斜纹。细折线连接器表示交给子智能体的工作，以及结果回传给上层的过程。运行时播放头会沿时间轴移动，页眉统计已过去的时间、token 与费用；运行结束时，它以摘要收尾，给出并行加速比。

- **运行历史与追踪查看器**：展示一次已结束的多智能体运行，其时间都花在哪里，哪些智能体在并行工作，哪些只是在等待。
- **调试面板**：失败的工具调用及其重试会以叉号和重试箭头突出显示；固定一个时间段，即可追踪它在整次运行中的交接路径。
- **产品演示与更新日志**：以 1x 或更快的速度回放一次真实运行，展示它自身的时间、token 与费用数字。
- **跟随正在进行的运行**：实时模式下，当后端追加事件时，未结束的时间段会增长到当前时间。

## 何时不用

- **单个智能体的步骤与答案**：当人们需要的是智能体的推理、来源和输出，而不是时序时，请用 `ToolCall` 行或 `AgentPlan` 展示对话记录。
- **一个智能体的状态一览**：使用 `AgentStatus`，它是紧凑的实时状态胶囊。
- **分布式请求追踪**：服务与网络调用的时间段属于追踪视图，而不是这里；时间线的词汇（思考、工具调用、等待、交接）针对的是智能体。
- **包含数千步的长追踪**：时间线能轻松处理几百个时间段。更长的追踪请传入运行的一个窗口，或先合并过小的时间段。
- **对智能体一般工作方式的讲解**：请使用 `AgentLoop` 插图，它用虚构的步骤讲述这个故事。

## 用法

没有提供数据时，时间线会展示一次虚构的示例运行，并在它首次有大约五分之一进入视野时开始回放。请在 `UseArcControls()` 旁调用 `UseArcSpecial()` 注册该包。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentTimeline />
</UserControl>
```

`AgentTimeline` 派生自 Avalonia 的 `TemplatedControl`。它会填满其容器，最宽为 744px；在 360 到 760px 宽之间阅读效果最好。

### 结构

1. **页眉**：`Title`（否则为运行的标题，再否则为 "Agent run"），以及其下的状态行。运行回放时，状态行显示当前状态（带闪光的运行中、已暂停或就绪）、已用时间、token 与费用；结束时变为对勾与摘要：智能体数量、长度、并行加速比与费用。
2. **智能体标签**：每个智能体一行，高 44px，编排器在最前：颜色圆点、名称，名称下方是模型的标志与名称。
3. **绘图区**：所有智能体的时间段位于同一时间轴上，刻度处有网格线。仍在运行的时间段，其前缘为方形，并带有一道短促的光泽；已结束的时间段则为圆角。
4. **连接器**：从编排器向下连到子智能体下一个时间段的折线（分派），或从子智能体最后一个时间段向上连到编排器的折线（结果），在与上一行相交处有一个圆点。
5. **播放头与时间轴**：当前时间处有一条细线；行下方是带刻度标签的时间轴和定位滑块。
6. **页脚**：左侧为播放或暂停（运行结束后则为重新播放）、重新开始以及速度控件；右侧为图例（思考、工具调用、等待、交接、失败），空间不足时移到控件下方。
7. **时间段卡片**：将鼠标悬停或聚焦到某个时间段上，即可看到由谁完成、使用哪个模型、做了什么、它的工具或类型、时长与 token，以及失败、重试或交接的说明。

### 示例运行

示例是一次虚构的事故排查，标题为 "Signup emails bouncing since Mar 3"（注册邮件自 3 月 3 日起被退回）。一个协调者规划了三项检查，并将它们交给状态监视器、配置审计员和日志读取员，三者并行工作。日志读取员的第一次扫描失败，缩小范围后的查询在第二次尝试时成功。协调者汇总各份报告，由记录员撰写事件说明并归档。五个智能体、30 个时间段和八次交接，共耗时 26.8 秒、49,920 个 token，费用为 $0.15，并行加速比为 1.5x。`AgentTimelineSamples.CreateIncidentRun()` 会返回它的一份新副本，`CreateRefundRun()` 则返回一个较小的双智能体运行。

### 你自己的运行

把一个 `AgentTimelineRun` 交给时间线：它的智能体按行的顺序排列（编排器在最前），时间段带有以运行开始为起点、以秒为单位的时间，还有交接。你自己的运行会以结束状态打开，可供回放；若希望它在进入视野时自动回放，请设置 `AutoPlay="True"`。

<Demo name="OwnRun" />

```xml
<arc:AgentTimeline>
    <arc:AgentTimeline.Run>
        <arc:AgentTimelineRun Title="Monthly refund summary">
            <arc:AgentTimelineRun.Agents>
                <arc:AgentTimelineAgent Id="planner" Name="Planner" Price="8" />
                <arc:AgentTimelineAgent Id="clerk" Name="Data clerk" Price="0.2" />
            </arc:AgentTimelineRun.Agents>
            <arc:AgentTimelineRun.Spans>
                <arc:AgentTimelineSpan AgentId="planner" Kind="Thinking" Start="0" End="1.2"
                                       Label="Outlined the summary" Tokens="1100" />
                <arc:AgentTimelineSpan AgentId="planner" Kind="Waiting" Start="1.2" End="5.8"
                                       Label="Waiting on the data clerk" />
                <arc:AgentTimelineSpan AgentId="clerk" Kind="Tool" Start="1.5" End="3.9"
                                       Label="Exported last month's refunds" Tool="Ledger"
                                       ToolMark="{x:Static arc:ArcIcons.Database}" Tokens="2200" />
            </arc:AgentTimelineRun.Spans>
            <arc:AgentTimelineRun.Handoffs>
                <arc:AgentTimelineHandoff From="planner" To="clerk" At="1.2" Label="Which refund reasons grew?" />
            </arc:AgentTimelineRun.Handoffs>
        </arc:AgentTimelineRun>
    </arc:AgentTimeline.Run>
</arc:AgentTimeline>
```

```csharp
timeline.SpanSelected += (_, e) => details.Show(e.Span);   // null when the pin is removed
```

- **智能体**：`Id` 用于在时间段和交接中指代该智能体。`Model` 是一个 `AgentModel`（一个名称，加上一个 24×24 的描边标志，例如 `ArcIcons` 的某个字形）。`Price` 是综合单价，单位为每百万 token 的美元数。`Color` 会覆盖行的颜色；默认情况下，编排器为中性色，其后的智能体依次取图表系列色 1 到 4。
- **时间段**：`Kind` 为 `Thinking`、`Tool` 或 `Waiting`。工具调用会指定其 `Tool`，并可带一个 `ToolMark`（未设置时为扳手）。`Status="Failed"` 配合 `Detail` 标记失败的调用；`IsRetry` 标记重试了前一次失败的那次调用，其说明写在 `Detail` 中。`Tokens` 在整个时间段内均匀累计；`Cost` 则会替代根据智能体单价算出的费用。
- **交接**：方向由形状推断。沿行向下交出的工作是分派：它在 `At` 处落下，并变为目标智能体的下一个时间段。向上交回的工作是结果：它从源智能体最后一个时间段的末端离开，并在 `At` 处升到目标。分派的时间应设在目标第一个时间段之前不久，结果的时间应设在源最后一个时间段之后不久。
- 时间线上的 `Agents`、`Spans` 和 `Handoffs` 会覆盖运行中的列表，并且每个列表在发出集合变更通知时都会被观察。时间段是 `AvaloniaObject`，因此新的 `End` 或 `Status` 会立即反映到绘图区。

### 数字与格式

- **时间**：保留一位小数，四舍五入（"26.8s"）；满一分钟后显示为 "1m 05.3s"。
- **Token**：每个时间段的 token 在其起点到终点之间均匀累计，求和后取整，并带千位分隔符。
- **费用**：每个时间段的 token 乘以其智能体每百万 token 的单价（或时间段自身的 `Cost`），保留两位小数。
- **加速比**：所有智能体用于思考和调用工具的时间之和，除以运行的总长度。等待属于空闲时间，不计入。低于 1.05 时，摘要会显示 "no parallel speedup"（无并行加速）。
- 计数器约每秒更新二十次，每个计数器都保留其最终数值的宽度，因此计数时页眉不会跳动。

时间轴的长度与运行相同。其刻度步长取以下列表中能使从零开始的刻度不超过六个、且间距至少为 40px 的最小值：先从 0.1、0.2、0.5、1、2、5、10、15 和 30 秒中选取，再从 1、2、5、10、15 和 30 分钟中选取。因此，26.8 秒的运行在任何宽度下都显示 0 到 25，步长为 5；6.2 秒按 2 递增，一分钟则按 15 递增。

### 播放与速度

`IsPlaying` 与 `Speed` 是双向的：页脚会写入它们，应用也可以绑定它们。播放从回放当前所在的位置继续；一旦播放结束，它会从头开始。重新开始总是回到零点，并以当前速度播放。`Speeds` 列出速度控件的选项（默认为 1x 和 2x）；只有一个选项或没有选项时，该控件隐藏。`Play()`、`Pause()`、`Restart()` 和 `Seek(seconds)` 可在代码中完成相同的操作。`PlaybackChanged` 报告用户的每一次操作（播放、暂停、重新开始、定位与速度），`Completed` 则在每次回放到达结尾时触发。

<Demo name="Speeds" />

以 1x 速度运行时，回放与真实时间同步。单帧最多前进 0.1 秒乘以速度，因此窗口卡顿时会丢失一瞬间，而不是跳过去；并且当时间线移出屏幕时，时钟会停止。

在时间轴上的任何位置按下或拖动，都会将回放移到那里并暂停；此时滑块获得键盘焦点。`Phase` 在零点为 Ready，播放中为 Running，中途为 Paused，到达结尾时为 Finished。

### 固定一个时间段

点击时间段，或在其上按 Enter，即可将其固定。卡片随之保留，并且不在该时间段交接路径上的内容会变暗：时间段降到 28% 的不透明度，连接器降到 16%。对于编排器的时间段，路径包括该时间段本身、在其期间开始或结束的交接，以及这些交接另一端的时间段。对于子智能体的时间段，路径则是该智能体的整行、与该智能体相互交接的每一次交接，以及这些交接另一端的时间段。再次点击该时间段、再次按 Enter，或按 Escape，即可取消固定。`PinnedSpan` 是双向的，`SpanSelected` 会报告每一次变化。

### 实时运行

在运行进行中设置 `IsLive`。时间从运行的 `StartedAt` 开始计（若未设置，则从实时模式开始的那一刻计起），没有 `End` 的时间段会增长到当前时间，并且回放控件隐藏。随着事件到达，追加时间段与交接；时间段关闭时，设置其 `End`。时间轴以平稳的步伐增长：当时间超过时间轴长度的 90% 时，它会以弹簧式动画扩展到一个整齐的长度，且该长度超过时间的 1.5 倍（如 5、8、12、20、30、50 秒，依此类推）。运行结束时将 `IsLive` 设为 false：时间线会停在摘要上，播报摘要，并触发 `Completed`。

<Demo name="Live" />

```csharp
timeline.Run = liveRun;            // an AgentTimelineRun your event handler appends to
timeline.IsLive = true;
// on each event
liveRun.Spans.Add(new AgentTimelineSpan("logs", AgentTimelineSpanKind.Tool, 3.1, null, "Bounce scan"));
span.End = 5.2;
// when the run ends
timeline.IsLive = false;
```

`AgentTimelineLiveDriver` 会把一次已结束的运行播放进时间线，仿佛它正在此刻发生，上方的演示正是这样工作的。

### 窄容器

布局跟随时间线自身的宽度，而不是窗口的宽度。宽度低于 520px 时，标签列从 176px 收窄为 132px，且只显示圆点和名称（时间段卡片仍会显示模型名称），两侧的内边距从 24px 降为 16px。当绘图区会窄于 300px 时，它保持 300px 宽，并在标签固定的情况下左右滚动；滚动后标签旁会出现一条分隔线。如果读者在最近 2.5 秒内滚动过，回放不会把播放头拉回视野；否则回放会让播放头保持在视野内。状态行会在分隔符后换行，图例则移到控件下方，并自行换行。

<Demo name="Narrow" />

### 内容准则

把每个时间段的标签写成智能体做了什么：使用过去时，寥寥几个词即可。卡片会完整显示它，但无障碍名称会把智能体、类型和时间围绕着它朗读。用你的产品中工具的真实身份来命名工具，并以通用字形作为标志。交接标签保持为一句简短的话：它们在卡片中显示为 "From Coordinator: …" 和 "To Coordinator: …"。

| 避免 | 改为 |
|---|---|
| Tool call | Searched blocklist reports for our domain |
| LLM generation step 3 | Matched the bounces to SPF failures |
| Error | Full scan of mail events ran past the time limit |
| Handoff | Mail config changes since Mar 1 |

### 最佳实践

**建议**

- 将编排器放在最前，并安排子智能体的顺序，使连接器穿过空闲的行：最先返回结果的智能体紧挨着编排器之下，分派则先发往最深的那一行。
- 将阻塞的时间标记为 `Waiting`，使并行工作与空闲时间一目了然，加速比也只统计真正的工作。
- 用 `Status="Failed"` 和 `Detail` 标记失败的调用，并用 `IsRetry` 标记重试它的那次调用。
- 为智能体设置 `Price`，使费用有意义；若某些智能体没有单价，则所有智能体都不要设置，而不是只给部分设置。

**避免**

- 使用真实模型供应商或服务的品牌标志。请使用通用字形，并在文字中写明模型名称。
- 在实时运行中每次事件都替换运行对象；请改为向它的列表追加内容。
- 同一屏幕上有多个正在回放的时间线。每个时间线在播放时都会逐帧绘制。

### 常见问题

**为什么我的运行一开始就是结束状态？** 你自己的运行是一份记录，因此会完整打开；若希望它在进入视野时回放，请设置 `AutoPlay="True"`。在减弱动效下，每个时间线都会以结束状态打开。

**为什么连接器只是一小段短线？** 分派会寻找目标第一个在其时间或之后开始的时间段。在实时运行中，该时间段尚未到达；当它到达时，折线才会完整连接。

**为什么卡片显示的时间比时间段更长？** 卡片总是显示时间段的最终长度、token 和交接，即使回放仍在绘制它。

**我可以翻译它吗？** 可以。将 `Labels` 设为一个 `AgentTimelineLabels` 并填入你的文字即可；含有数字的句子是函数，因此词序可以调整。

## API 参考

<!-- api: AgentTimeline -->

<!-- api: AgentTimelineRun -->

<!-- api: AgentTimelineAgent -->

<!-- api: AgentTimelineSpan -->

<!-- api: AgentTimelineHandoff -->

<!-- api: AgentTimelineLabels -->

<!-- api: AgentTimelineSpanSelectedEventArgs -->

<!-- api: AgentTimelinePlaybackChangedEventArgs -->

<!-- api: AgentTimelineLiveDriver -->

<!-- api: AgentModel -->

枚举与取值如下：

| 类型 | 取值 |
|---|---|
| `AgentTimelineSpanKind` | `Thinking`, `Tool`, `Waiting` |
| `AgentTimelineSpanStatus` | `Ok` (default), `Failed` |
| `AgentTimelinePhase` | `Ready`, `Running`, `Paused`, `Finished`, `Live` |
| `AgentTimelinePlaybackAction` | `Play`, `Pause`, `Restart`, `Seek`, `Speed` |
| `AgentTimelineSummary` | `AgentCount`, `Duration`, `Speedup`, `Tokens`, `Cost` (the `Summary` property) |

`AgentTimelineSamples` 保存虚构的运行（`CreateIncidentRun()`、`CreateRefundRun()`）。

## 无障碍

- 时间线以其标题命名。对辅助技术而言，每个时间段都是一个按钮，名称包含智能体、类型、工具、"failed" 或 "retry"、它做了什么、时间范围以及 token 数（例如 "Log reader, Tool call, Mail logs, failed: Bounce scan took too long, 3.1s to 5.2s, 430 tokens"）；被固定的时间段会被读作已按下。时间段整体只占一个 Tab 停靠点，方向键在它们之间移动。
- 绘图区是绘制出来的，因此时间段本身不是控件：键盘所在的时间段上有一个焦点替身，带有它的名称；每次按下方向键，焦点都会移到一个新的替身上，使屏幕阅读器能播报新的时间段。
- 定位滑块名为 "Run time"，其值为当前时间，并带有 "12.4s of 26.8s" 之类的帮助文字。
- 状态从不只依赖颜色：失败的调用在其卡片和图例中显示叉号与 "Failed"，重试显示箭头及其说明，完成的状态则显示对勾与摘要。
- 一个礼貌级别的实时区域会在读者操作时发声："Playing at 1x" 出现在播放、重新开始或播放中更改速度时，"Paused at 11.1s" 出现在暂停或定位时，运行结束时则播报摘要。首次自动回放时保持安静；计时中的状态行不是实时区域（其无障碍名称包含计数以及有多少个智能体正忙）。
- 时间段卡片重复了时间段名称已经表达的内容，因此对辅助技术隐藏。
- 键盘焦点在时间段、滑块和控件周围显示 2px 的焦点环；指针焦点则不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在时间段（整体一个停靠点）、定位滑块、播放或暂停、重新开始与速度控件之间移动 |
| 时间段上的 `Left` / `Right` | 同一行中的上一个或下一个时间段 |
| 时间段上的 `Up` / `Down` | 上一行或下一行中，中点在时间上最接近的时间段（跳过尚无时间段的行） |
| 时间段上的 `Home` / `End` | 所在行的第一个或最后一个时间段 |
| 时间段上的 `Enter` / `Space` | 固定它并高亮其交接路径；再按一次则取消固定 |
| 时间段上的 `Escape` | 取消固定 |
| 定位滑块上的 `Left` / `Down`，`Right` / `Up` | 后退或前进半秒（并暂停） |
| 定位滑块上的 `Shift` + 方向键，`Page Down` / `Page Up` | 后退或前进五秒 |
| 定位滑块上的 `Home` / `End` | 运行的开始（Ready）或结束（Finished） |
| 速度控件上的 `Left` / `Right` | 上一个或下一个速度，并循环 |

## 动效

- 播放头、滑块以及每个活动时间段都会在每一帧随回放时钟移动；时间段的增长不需要任何布局计算。计数器约每秒更新二十次。
- 活动的思考或工具时间段，其前缘为方形，并有一道 28px 的光泽，每 1.5 秒扫过一次，每次耗时 1.05 秒，暂停时也照常进行。时间段结束时，其右侧圆角变圆，前缘淡出；失败的调用则在 160ms 内转为失败样式，叉号或重试箭头也在 160ms 内出现。
- 连接器在 360ms 内从交接的出发一侧画出：分派从顶部，结果从底部。它的圆点则在 160ms 内弹出，分派立即弹出，结果则在线条到达后 260ms 才弹出。
- 固定时的变暗与恢复耗时 160ms；鼠标悬停在思考或工具时间段上时，颜色在 160ms 内加深。
- 时间段卡片在 160ms 内淡入，同时上升 3px，在临界阻尼弹簧（约 20 rad/s）上滑向新的时间段，以弹簧方式调整到新的高度，并交叉淡化其内容（100ms 淡出，160ms 淡入）；离开时先有 80ms 的宽限，然后以 120ms 淡出。
- 运行结束时，播放头在 240ms 内淡出，状态行交叉淡化为摘要（160ms 淡入，100ms 淡出）；播放按钮的图标也会交叉淡化。滑块在聚焦或被拖动时会在 160ms 内放大到 1.3 倍。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开 | 首次进入视野时回放示例 | 以结束状态打开 | 以结束状态打开 |
| 回放 | 应读者请求而播放 | 应读者请求而播放 | 应读者请求而播放 |
| 活动时间段的光泽 | 每 1.5 秒扫过一次 | 无 | 无 |
| 时间段完成、变暗、悬停 | 160ms | 立即 | 立即 |
| 连接器画出与圆点弹出 | 360ms 与 160ms | 立即出现 | 立即出现 |
| 卡片位置与高度 | 弹簧 | 直接跳到位 | 直接跳到位 |
| 卡片淡入与内容交叉淡化 | 160ms / 120ms，100 / 160ms | 保留（短暂的交叉淡化） | 立即 |
| 状态行与播放头 | 交叉淡化，240ms 淡出 | 立即 | 立即 |

## 相关组件

- `AgentStatus`：以紧凑的胶囊展示一个智能体的实时状态。
- `ToolCall`：展示单次工具调用的输入与输出。
- `AgentPlan`：展示智能体逐步遵循的计划。
- `ContextWindow`：展示模型的上下文占用了多少。
- `AgentLoop`：以插图说明单个智能体如何在上下文、工具与检查之间循环。

| 组件 | 何时选用 |
|---|---|
| `AgentStatus` | 你要展示一个正在运行的智能体此刻在做什么 |
| `ToolCall` | 人们需要的是一次调用的输入与输出 |
| `AgentPlan` | 故事的重点是计划，而不是时序 |
| `ContextWindow` | 问题是上下文用掉了多少 |
| `AgentLoop` | 你用一个虚构的故事讲解智能体循环 |
