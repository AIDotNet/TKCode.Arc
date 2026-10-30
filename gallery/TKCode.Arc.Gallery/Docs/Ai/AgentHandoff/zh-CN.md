---
title: Agent handoff
summary: AI 助手把对话转交给真人的客服聊天：一张卡片显示它交给团队的摘要和通常的等待时间，随后真人加入，左侧一条细时间线标出每一步。
source-hash: a81b332d51fd
---

<Demo name="Preview" />

## 何时使用

`AgentHandoff` 是一段客服对话：AI 助手先回答，再把对话转交给真人。转接时，对话里的一张卡片会显示助手交给团队的摘要和通常的等待时间。随后真人加入并接手，左侧一条细时间线标出每一步：助手、转接、真人加入和问题解决。

- **客服小组件**：AI 先回答，需要时升级给人工客服。
- **向客户展示助手交给团队的内容**，让他们不必重复说明。
- **营销或文档页面**，讲解从 AI 到真人的转接流程。

## 何时不用

- **只有一个助手、没有转接的普通聊天**：用 `ChatThread`。
- **展示智能体的内部步骤或工具调用**：用 `AgentTimeline` 或 `ToolCall`。
- **给客服人员用的完整工单收件箱**：用列表和详情布局搭建。

## 用法

既没有 `Stage` 也没有模拟时，卡片是一个空对话：只有标题栏和助手的步骤。要接入你的客服后端，设置 `Stage` 和数据，并处理事件：

```csharp
chat.Title = "Order NW-48213";
chat.Subtitle = "Northwind Home support";
chat.Summary = new AgentHandoffSummary(
    "Desk arrived with a cracked leg. Customer wants a full refund.",
    new AgentHandoffField("Order", "NW-48213"),
    new AgentHandoffField("Asks for", "Full refund"));
chat.Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05");
chat.ShowsComposer = true;
chat.AllowsRequestHuman = true;
chat.SendRequested += (_, e) => backend.Send(e.Text);
chat.HumanRequested += (_, _) => backend.Escalate();

// Then follow the backend.
chat.Stage = conversation.Stage;
```

助手的消息放在 `Messages` 里，转接卡片之后的一切放在 `AgentMessages` 里：客户等待时补充的说明，以及与真人的对话。加入步骤会放在真人的第一条消息之前。

### 阶段

<Demo name="YourBackend" />

由后端驱动 `Stage`：创建工单时设为 `Transferring`，有人接手时设为 `Connected`，对方关闭对话时设为 `Resolved`，排队超时设为 `Unavailable`。

| 阶段 | 标题栏状态 | 转接卡片 |
|---|---|---|
| `Assistant` | 星芒图标和“AI assistant” | 不显示；最后一条回答下方可以出现 Talk to a person |
| `Transferring` | 加载圈和“Transferring” | “Transferring you to a person”、通常的等待时间（`ReplyEstimate`）、Cancel |
| `Connected` | 人像图标和“With Jordan” | “Jordan joined from Help desk”、接手的人、摘要 |
| `Resolved` | 对勾和“Resolved” | “Resolved by Jordan”；最后一步收尾时间线 |
| `Unavailable` | 警示图标和“No one available” | “No one is available right now”、Try again |

### 操作与输入框

每个操作在你打开对应属性后才显示，并触发一个冒泡的路由事件：

- `AllowsRequestHuman`：在助手最后一条已完成的回答下方显示 Talk to a person 快捷回复，触发 `HumanRequested`。
- `AllowsCancelTransfer`：转接中在卡片上显示 Cancel，触发 `CancelTransferRequested`。
- `AllowsRetry`：无人接手时在卡片上显示 Try again，触发 `RetryRequested`。
- `ShowsComposer`：显示输入框。按 Enter 或发送按钮会触发 `SendRequested`，携带去掉首尾空白的文本；消息要由你自己追加到 `Messages` 或 `AgentMessages`。占位文字随阶段变化（“Message Jordan”），除非你设置了 `Placeholder`。

### 流式输出与正在输入

要流式输出助手的文字，在设置了 `IsStreaming` 的消息上更新 `Text`；卡片会让每次追加的片段淡入。把 `Typing` 设为 `Assistant` 会显示一行简短的闪光 Thinking，设为 `Agent` 会显示正在输入的圆点。

```csharp
var reply = new AgentHandoffMessage(AgentHandoffSender.Assistant, string.Empty) { IsStreaming = true };
chat.Messages.Add(reply);
await foreach (var chunk in stream)
{
    reply.Text += chunk;
}
reply.IsStreaming = false;
```

### 窄容器

<Demo name="Narrow" />

卡片填满容器，最宽 560px；在 360px 宽时依然可用，内边距更紧凑，气泡更宽。卡片高度固定（默认 600，可设置 `Height`），对话在内部滚动并固定在最新消息处，所以对话变长时页面不会跳动。过长的名字和元信息行以省略号截断，消息文字会换行。

### 标识与头像

`AssistantMark` 和 `ProviderMark` 接受一个 24 × 24 的描边图形（Lucide 图标或你自己的路径），分别用于第一步的助手和转接卡片旁的客服系统；`AssistantMarkBrush` 和 `ProviderMarkBrush` 设置它们的颜色。接手的人的头像显示姓名首字母，或 `AgentHandoffAgent.Avatar` 中的照片。

### 模拟

`AgentHandoffSimulation` 为演示和原型播放一段按剧本进行的对话，就像上面的预览。把台词放进 `AgentHandoffScript`（留空为 null 的台词会被跳过），再把它设为卡片的 `Simulation`：卡片在未设置 `Stage` 时播放它，每次显示时从头开始，输入框以及 Talk to a person、Cancel 和 Try again 操作都能使用。`Scenario="Unavailable"` 会停在无人接手的时候，于是显示 Try again；切换场景会让故事从头开始。它公开 `Stage`、`Messages`、`AgentMessages`、`Typing`、`Summary`、`Agent` 和 `Notice`，并接受 `RequestHuman`、`CancelTransfer`、`Retry` 和 `Send`。设置 `Stage` 后，卡片就回到你的数据。不要在演示之外使用它。

```xml
<arc:AgentHandoff Title="Order 1042" AssistantModel="Your model">
    <arc:AgentHandoff.Simulation>
        <arc:AgentHandoffSimulation Scenario="Unavailable" />
    </arc:AgentHandoff.Simulation>
</arc:AgentHandoff>
```

```csharp
chat.Simulation = new AgentHandoffSimulation
{
    Script = new AgentHandoffScript
    {
        CustomerOpening = "My order arrived damaged.",
        AssistantAnswer = "Sorry about that. I can send a replacement part.",
        CustomerRequest = "Can I talk to a person?",
        AssistantHandoff = "Of course, I'll pass this to our team with a summary.",
        Summary = new AgentHandoffSummary("Order arrived damaged; wants a refund."),
        Agent = new AgentHandoffAgent("Sam Lee", "Support specialist"),
        AgentGreeting = "Hi, I'm Sam. I've read the summary.",
    },
};
```

### 你自己的文案

`Labels` 包含卡片显示或朗读的每一个词。替换整个对象即可本地化或改写：

```csharp
chat.Labels = new AgentHandoffLabels
{
    Transferring = "Übergabe",
    TalkToPerson = "Mit einer Person sprechen",
    With = name => "Mit " + name,
};
```

### 最佳实践

- `Summary` 里放你发给客服系统的同样事实，让客户看到分享了什么。
- 对话很长时只传最近的消息；这张卡片用于进行中的对话，不是存档。
- 一次性的结果（例如 “Refund issued”）用 `Notice`，它只会被朗读一次。

## API 参考

<!-- api: AgentHandoff -->

<!-- api: AgentHandoffMessage -->

<!-- api: AgentHandoffLabels -->

<!-- api: AgentHandoffSimulation -->

<!-- api: AgentHandoffScript -->

## 无障碍

- 对话区是一个名为 “Conversation” 的可聚焦日志，键盘用户可以滚动它；它不会朗读每一个流式片段。
- 阶段变化和 `Notice` 通过礼貌的状态区域朗读一次，其中包括演示是模拟的这一点。
- 每个状态都由图标和文字搭配：Transferring、With Jordan、Resolved、No one available。颜色从来不是唯一的信号。
- 摘要切换按钮会报告展开或收起。正在输入的圆点有名称，例如 “Jordan is typing”。
- 时间线上的标识和头像在可见文字旁只起装饰作用。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次移到对话区、Talk to a person、卡片上的操作、摘要切换按钮和输入框 |
| Up、Down、Page Up、Page Down、Home、End | 滚动获得焦点的对话区 |
| Enter | 发送输入框里的消息，或按下获得焦点的按钮 |
| Space | 摘要按钮获得焦点时展开或收起摘要 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 助手回复流式输出 | 约每 34ms 一段，每段 3 到 8 个词；每段只做 140ms 的淡入 | 回复整段出现 | 回复整段出现 |
| 新行到来或文字换行 | 对话区以无回弹的 0.24s 弹簧跟随测得的高度，向上推开而不跳动 | 立即 | 立即 |
| 转接中 | 一个强调色小圆点沿转接卡片旁的时间线向下移动 | 没有移动的圆点 | 没有移动的圆点 |
| 状态文字变化 | 旧文字 100ms 淡出，新文字 160ms 淡入并上升 2px | 短暂交叉淡化 | 立即 |
| 正在输入的圆点、加载圈 | 呼吸、旋转 | 保持静止 | 保持静止 |
| 模拟对话 | 播放并循环 | 只播放一次 | 只播放一次 |

离开屏幕时循环暂停。

## 相关组件

- `ChatThread`：与单个助手或真人之间的聊天。
- `AgentTimeline`：多智能体运行的各个步骤。
- `ToolCall`：智能体发起的工具调用。
