---
title: Ticket router
summary: 支持消息逐条到达，AI 分类器标注意图、情绪与置信度，每张工单都会移入带负责人的团队泳道，或在复核泳道中等待一次点击。
source-hash: 7fc5577c3e60
---

<Demo name="Preview" />

## 何时使用

`TicketRouter` 展示 AI 分类器如何分拣进来的支持消息。来自邮件、聊天、消息工作区和帮助台的消息逐条到达。分类器为每条消息标注意图、情绪和置信度，一道脉冲沿连接线跑向目标团队，工单随即移入该团队的泳道，由轮到的下一位成员接手。分类器拿不准的工单会带着它的建议停在复核泳道里，在那里点一下即可送走。

- **帮助台与运营仪表板**：一个实时分拣小部件，并为低置信度保留人工复核泳道。
- **展示 AI 路由如何工作**：分类器读到了什么、有多确定、工单去了哪里。
- **支持类产品的落地页与新手引导**，用 `Simulation` 循环播放一个由你提供的收件箱。

## 何时不用

- **完整的工单收件箱**（带搜索和批量操作）：使用表格或列表。
- **聊天记录中的单个工具步骤**：使用 `ToolCall`。
- **通用的多步骤智能体进度**：使用 `AgentStatus` 或 `AgentPlan`。

## 用法

```xml
<arc:TicketRouter />
```

`TicketRouter` 是 Special 包中的 `TemplatedControl`（用 `UseArcSpecial()` 注册）。它填满容器，最宽 840px；请放在约 360 到 880px 的内容栏中。没有工单时，它停在默认泳道（Billing、Engineering、Success 和 Needs review）中的 “No tickets yet”。设置 `Tickets` 来路由你自己的消息，或设置 `Simulation` 让它自行播放一个收件箱。

### 模拟

`TicketRouterSimulation` 是 `Tickets` 为 null 时路由器播放的收件箱：分类器的 `Model` 名称、`Lanes`、按到达顺序排列的 `Tickets`（每条的 `Sample` 中是模拟分类器给出的答案），以及已在泳道中的 `Routed` 工单。它会循环播放，卡片离开屏幕时等待，并随 `IsPaused` 暂停；它的播报会说明这是一次模拟运行、没有发送任何内容。你自己设置的 `Tickets`、`Lanes`、`InitialRouted` 和 `Model` 优先于它；把 `Simulation` 设为 null 会回到空状态重新开始。

```csharp
router.Simulation = new TicketRouterSimulation
{
    Model = "Your classifier",
    Tickets =
    [
        new TicketMessage("t-1", TicketChannel.Email, "Hannah Brooks", "Lumen Bakery", "Charged twice for the March invoice",
            "Our card shows two charges of $49 on the 3rd.")
        {
            Sample = new TicketClassification(TicketIntent.Billing, TicketSentiment.Negative, 0.94),
        },
    ],
};
```

本页的演示从 `TicketRouterDemoData` 获取收件箱：人物、公司和消息都是虚构的，分类器使用真实模型的名称（核对于 2026-10-29）。

### 结构

1. **头部**：着色方块上的模型标记、标题、模型名称、状态（Routing、Paused、All caught up）以及暂停 / 继续按钮。
2. **到达的工单**：渠道图标与名称、客户与公司、到达时间、主题和开头几句话。每一行都只有一行高，并以省略号结尾，所以长主题永远不会改变卡片高度。
3. **分类条**：分类器读取时 "Classifying with Mistral Small 4" 闪烁，随后意图、情绪和置信度（一条短进度条和百分比）依次淡入，末尾是路由目标：团队，或 "Needs review · Suggested Billing"。
4. **连接线**：从舞台引出的竖线、横跨各泳道的细线，以及伸入每条泳道的短线。
5. **泳道**：每条泳道的名称和数量，下方是最新的几行。一行显示负责人的头像（姓名首字母）、主题，以及意图和客户。复核行显示置信度和它将去往的泳道，并带一个箭头。

### 你的分类器

把 `Classifier` 设为你的模型调用。它接收消息和取消令牌，返回一个 `TicketClassification`（意图、情绪、0 到 1 的置信度）。流程暂停、卡片滚出屏幕以及被卸载时，令牌都会被取消；流程恢复时会重新调用。抛出异常即显示错误状态："Couldn't classify"，并提供 Retry 和 Send to review。

<Demo name="YourClassifier" />

```csharp
router.Classifier = async (ticket, token) =>
{
    var reply = await http.PostAsJsonAsync("/api/classify", ticket, token);
    return (await reply.Content.ReadFromJsonAsync<TicketClassification>(token))!;
};
router.TicketRouted += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.Ticket.LaneId, e.Ticket.Assignee);
router.ReviewConfirmed += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.LaneId, e.Ticket.Assignee);
```

没有分类器时，卡片读取每条消息的 `Sample`，它的播报会说明这是一次模拟运行、没有发送任何内容。没有示例答案的消息会被当作一个不确定的问题，进入复核。

### 泳道与路由

`Lanes` 从左到右列出泳道。每个 `TicketLane` 有 id、标签、轮流接单的负责人（轮询）、此前已路由的工单数 `Count`，以及在收集低置信度工单的泳道上设置的 `IsReview`。`Route` 为一次分类挑选泳道 id；默认的 `TicketRouter.DefaultRoute` 把账单和退款送往 `billing`，缺陷送往 `engineering`，功能请求和问题送往 `success`。不匹配任何团队泳道的 id 会回退到第一条团队泳道。没有 `Lanes`（且没有播放模拟）时，卡片使用带有这些 id 的默认泳道（`TicketRouter.BillingLaneId`、`EngineeringLaneId`、`SuccessLaneId` 和 `ReviewLaneId`），名称来自标签文字，没有负责人。

低于 `ReviewThreshold`（默认 0.7）的工单会落入复核泳道，以挑选出的泳道作为建议，且没有负责人。点击它，或在它上面按 Enter 或 Space，就会把它送往该泳道、由那里的下一位负责人接手，并触发 `ReviewConfirmed`。`ConfirmReview(ticket)` 在代码中做同样的事。

### 你的队列

`Tickets` 按顺序接收你的消息。除非设置 `Loop`，你自己的队列不会循环，因此最后停在 "All caught up"；向 `ObservableCollection` 追加消息，流程就会接着处理新工单。空队列显示 "No tickets yet"。`InitialRouted` 在首次显示时填充泳道（最新的在前），`Restart()` 从头开始，`RoutedTickets` 保留最新的 80 张工单。

### 暂停与速度

`IsPaused`（双向）让流程停在当前位置，并取消正在进行的分类器调用；暂停 / 继续按钮会设置它，每次变化都会触发 `PausedChanged`。在标记中设置 `IsPaused="True"` 即可以暂停状态启动。`Speed` 调整节奏的倍数（1 约为每张工单两秒）。一个帧循环驱动整个流程，卡片不在屏幕上时它会等待。

### 窄容器

<Demo name="Narrow" />

内容宽度低于 600px 时，四条泳道折叠成 2 × 2 网格，每条最多两行（更宽时 `MaxVisible` 为 3），连接线隐藏，标签堆叠在路由目标上方。低于 400px 时，置信度进度条和模型名称隐藏，百分比保留。

### 你自己的文案

`Labels` 包含卡片显示或播报的每一个词，包括意图、情绪、渠道以及每一条播报：

```csharp
router.Labels = new TicketRouterLabels
{
    Routing = "Tri en cours",
    Intent = intent => intent == TicketIntent.Billing ? "Facturation" : intent.ToString(),
    AnnounceRouted = (subject, lane, assignee) => $"{subject} : {lane}",
};
```

`Title`、`Model` 和 `ModelMark`（24 × 24 描边图形，默认为闪光图标）设置头部。渠道使用中性的图标（信封、聊天气泡、消息方框、救生圈），从不使用品牌标志。

### 内容指南

- 主题是客户写下的原文；保持原样，让卡片去截断。
- 泳道名称保持一两个词（"Billing"、"Needs review"）。
- 把阈值设在"路由错误的代价高于短暂等待"的位置。

### 最佳实践

- 把 `Classifier` 接到你的模型，把 `TicketRouted` 接到你的帮助台；卡片本身从不发送任何内容。
- 用好复核泳道：低置信度正是应该由人来决定的时刻。
- 把 `CancellationToken` 一路传给你的 HTTP 调用，这样暂停才能真正停止它。

### 常见问题

**同一条消息会落地两次吗？** 模拟循环时会；每次落地都是一个独立的 `RoutedTicket`。

**复核泳道中的工单会计入某个团队吗？** 不会。在有人确认之前，它计入复核泳道。

## API 参考

<!-- api: TicketRouter -->

<!-- api: TicketMessage -->

<!-- api: TicketClassification -->

<!-- api: TicketLane -->

<!-- api: RoutedTicket -->

<!-- api: TicketRouterLabels -->

<!-- api: TicketRouterSimulation -->

<!-- api: TicketRoutedEventArgs -->

<!-- api: TicketReviewConfirmedEventArgs -->

<!-- api: TicketClassificationFailedEventArgs -->

<!-- api: TicketRouterPausedChangedEventArgs -->

## 无障碍

- 卡片以其标题命名。每条泳道以其标签和工单数命名（"Billing, 14 tickets"）。
- 一个礼貌的实时区域播报每次路由（"routed to Billing, assigned to Andre Williams"）、复核工单、失败、暂停和确认；没有设置分类器时，还会补充说明这是模拟运行。
- 意图、情绪和复核状态总是图标与文字成对出现；置信度以百分比读出，从不只靠颜色。
- 复核工单是真正的按钮，名称包含主题、置信度和目标泳道。团队行不是 Tab 停靠点。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 在暂停按钮、出现时的 Retry 与 Send to review，以及各复核工单之间移动 |
| Enter、Space | 暂停或继续、重试失败的分类、送入复核，或确认复核工单的泳道 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 工单到达 | 旧卡片在 100ms 内淡出，新卡片在 160ms 内淡入并上升 4px | 淡入淡出，不上升 | 立即 |
| 分类中 | 标签闪烁 | 静止 | 静止 |
| 标签 | 以 50ms 的间隔依次在 160ms 内淡入并上升 2px | 淡入，不上升 | 立即 |
| 连接线 | 一道脉冲在 240ms 内画向目标泳道，然后淡出 | 无 | 无 |
| 落地 | 行以位置弹簧（0.32s，无回弹）从舞台移入其泳道 | 原地出现 | 原地出现 |
| 行的进入与离开 | 高度以弹簧变化（0.24s，无回弹） | 立即 | 立即 |
| 落地高亮 | 行亮起强调色底色，并在 900ms 内淡去 | 保留 | 无 |
| 泳道计数 | 在预留的等宽位置中滚动 | 立即 | 立即 |

速度为 1 时每张工单约两秒：到达 220ms、分类、标签、240ms 的脉冲，然后落地。卡片不在屏幕上时流程暂停。工单流动时卡片高度从不改变。

## 相关组件

- `AgentStatus`：单个运行中智能体的实时状态。
- `ToolCall`：对话记录中的单个工具步骤。
- `AgentPlan`：以清单形式呈现的智能体计划。
