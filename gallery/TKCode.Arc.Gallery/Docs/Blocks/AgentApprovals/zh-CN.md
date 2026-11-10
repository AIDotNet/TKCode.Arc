---
title: Agent approvals
summary: 智能体想执行、需要人点头的操作队列：每个请求附有智能体的理由、事实、风险和可选备注，可批准或拒绝，风险高的还要多做一次确认。
source-hash: b3bfea613306
---

<Demo name="Preview" />

## 何时使用

`AgentApprovals` 是人对 AI 智能体想做、但不应独自去做的事情说是或否的地方：向公司外发送邮件、付款、发布到生产环境、删除记录。等待的请求列在一侧并标出风险。选中的请求在另一侧完整显示：智能体想做什么、为什么、操作的事实（收件人、金额、环境）、风险有多大及原因、一个给智能体留言的输入框，以及 Deny 和 Approve。高风险请求要求用户先勾选已核对详情，Approve 才能使用。已决定的请求离开队列，下一个请求随即打开。

- **智能体的人工把关**：涉及资金、客户或生产系统的操作。
- **团队共享的审批收件箱。**

## 何时不用

- **观看智能体正在做什么。** 请使用 `Agent run`。
- **人与人之间的审批**（报销、请假）。表单或表格更合适。
- **就地确认一个操作。** 请使用 `Dialog` 或 `Confirm morph`。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它显示示例请求并在本地作出决定。在应用中，给它你的请求，并把每个决定发送到你的后端：

```xml
<arc:AgentApprovals Requests="{Binding PendingApprovals}" />
```

```csharp
queue.DecisionHandler = (request, decision, note, cancellation) =>
    api.AnswerAsync(request.Id, decision == AgentApprovalDecision.Approved, note, cancellation);
queue.Decided += (_, e) => log.Add($"{e.Request.Title}: {e.Decision}");
```

### 请求与决定

`AgentApprovalRequest` 包含智能体、决定图标的类型（邮件、付款、发布、数据修改、权限、其他）、标题、放在 `Context` 中的智能体理由、作为标签和值的 `Details`、带 `RiskNote` 的 `Risk`，以及提出的时间。`DecisionHandler` 运行时 Approve 显示加载圈；如果它抛出异常，请求保留，消息显示在按钮下方。已决定的请求会从屏幕上的队列中离开，但你的集合不会被修改：可以在后端确认后从中移除，或保留作为记录。`Reset` 会把所有已决定的请求放回队列。

<Demo name="Backend" />

## API 参考

<!-- api: AgentApprovals -->

<!-- api: AgentApprovalRequest -->

<!-- api: AgentApprovalDetail -->

<!-- api: AgentApprovalsLabels -->

## 无障碍

- 队列是名为 "Waiting for approval" 的区域；列表和选中的请求都有标题。
- **Tab** 可到达列表行、备注、确认框（高风险时）和按钮。**Up** 和 **Down** 在列表中移动选择，**Home** 和 **End** 跳到两端；**Enter** 或 **Space** 选中一行。
- 列表行作为列表项读出标题、智能体和风险，并报告是否被选中。
- 作出决定后，一个礼貌式实时区域会播报结果，焦点移到下一个请求。
- 风险由文字、图标和底色表示，从不只靠颜色。

## 动效

- 选中另一个请求时，风险面板的底色在 240ms 内变化；列表行在悬停和选中时变色。
- 减少动效和关闭动效时颜色变化立即生效。

## 响应式

- 队列最宽 920，小于这个宽度时跟随容器。
- 宽度在 680 或以下时列表位于请求上方。
- 列表中的标题最多两行；详情自动换行。

## 相关组件

- `Agent tasks`，查看智能体正在做的工作。
- `Agent run`，详细查看一次运行。
- `Dialog`，就地确认一个操作。
