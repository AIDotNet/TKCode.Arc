---
title: Support conversation
summary: 客服人员查看一位客户对话的界面：消息、可写回复或内部备注并带常用回复的输入区、状态，以及显示客户信息和团队备注的面板。
source-hash: f5bb97038d7b
---

<Demo name="Preview" />

## 何时使用

`SupportConversation` 是客服人员这一侧的单个客户对话。消息在中间，输入区可以写客户能看到的回复，也可以写只有团队能看到的内部备注（备注带暖色底，两者不会混淆）；常用回复一按即可插入，状态切换说明轮到谁处理。右侧面板显示客户是谁：方案、地点、价值、标签和团队备注。

- **客服系统或共享收件箱**：从对话列表中打开。
- **客户成功工具**：账户背景和消息同样重要。

## 何时不用

- **客户这一侧的聊天**：使用 `SupportWidget` 或 `ChatThread`。
- **大量对话的队列**：先用 `InboxTriage` 整理，再在这里打开一个。
- **团队聊天**：单独使用 `ChatThread`。

## 用法

```xml
<arc:SupportConversation x:Name="Conversation" Subject="Charged twice in March" AgentId="agent" Status="Open" />
```

```csharp
Conversation.ReplySent += (_, e) => api.Deliver(e.Text, e.Mode);
Conversation.Customer = new SupportCustomer { Name = "Mara Lindqvist", Email = "mara@example.com", Plan = "Team" };
Conversation.Participants.Add(new ChatParticipant("cust", "Mara Lindqvist"));
Conversation.Messages.Add(new ChatMessage("m1", "cust", "Hi! I was charged twice.", DateTimeOffset.Now));
Conversation.CannedReplies.Add(new SupportCannedReply("Refund issued", "Hi {name}, the refund is on its way."));
```

回复会作为客服的消息加入 `Messages`，并把处理中的对话改为等待中；备注加入 `Notes`。两者都会触发 `ReplySent`，带上文字、模式和加入的项目。常用回复中的 `{name}` 会换成客户的名字。消息以 Markdown 显示。首次加载时没有消息，会显示一个示例对话。

### 窄栏

<Demo name="Narrow" />

`Send()`、`InsertCannedReply(reply)` 和 `OpenCannedReplies()` 可以用代码完成输入区的操作。

## API 参考

<!-- api: SupportConversation -->

<!-- api: SupportCustomer -->

<!-- api: SupportNote -->

<!-- api: SupportCannedReply -->

<!-- api: SupportConversationStatus -->

<!-- api: SupportComposerMode -->

<!-- api: SupportReplyEventArgs -->

<!-- api: SupportStatusChangedEventArgs -->

## 无障碍

- 客户名字是标题；状态和输入模式是有名称的分段控件。
- 输入区的名称随模式变化（“回复”或“内部备注”），读屏用户能知道谁会看到这段文字。发送和状态变化都会读出。
- 备注以“某人的备注”加内容命名；客户信息是标签和值的组合。

### 键盘

| 按键 | 作用 |
|---|---|
| 输入区中的 `Ctrl+Enter` | 发送回复或添加备注。 |
| 空输入区中的 `/` | 打开常用回复。 |
| 常用回复中的 `↑` / `↓`、`Enter`、`Escape` | 选择一条或关闭列表。 |

## 动效

- 新消息以对话列表自身的动效出现（向上浮入，已读标记移到最新已读的消息）。
- 输入区的底色和分段控件以短暂淡入淡出变化；常用回复在一个缩放出现的浮层中打开。
- 减少动效时只保留淡入淡出；关闭动效时变化立即发生。

## 响应式

- 容器没有给出高度时，区块高 620 px（`DefaultHeight`）。
- 宽度在 820 px 及以上时，客户面板在右侧（300 px，`SidePanelWidth`）。更窄时（`:narrow`），它移到标题区的面板按钮后面，显示时覆盖在消息上。

## 相关组件

- `ChatThread`：单独的消息列表。
- `InboxTriage`：对话队列。
- `SupportWidget`：客户这一侧。
