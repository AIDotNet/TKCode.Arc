---
title: PII redaction
summary: 一张防护卡片：提示词送到模型之前，其中的个人数据会被脱敏；回复返回时，原始值再被放回。
source-hash: f3818a4777ad
---

<Demo name="Preview" />

## 何时使用

`PiiRedaction` 展示隐私防护对提示词做了什么。扫描会在消息中标出每个姓名、邮箱、电话号码和卡号及其类型，然后在一次交叉淡化中把它们各自换成带编号的占位符，例如 `[EMAIL_1]`。发送会让安全提示词沿一条小流程（你、PII 防护、模型）传递，回复带着占位符流式返回，防护再为用户放回原始值。"显示原文"在安全提示词与用户输入的内容之间切换。

- **说明或证明一项防护措施**：客服助手、医疗或金融副驾驶、企业聊天等 AI 产品。
- **设置或引导页面**：管理员在开启 PII 过滤之前先看到它会做什么。
- **演练场**：用户粘贴文本，准确看到哪些内容会到达模型。

## 何时不用

- **对话本身**：使用聊天或输入框组件，在后台脱敏；只在需要解释防护的地方显示这张卡片。
- **合规**：不要依赖内置检测器。通过 `Detector` 接入真正的 DLP 或 NER 服务。
- **对话记录里任意的工具步骤**：使用 `ToolCall`。

## 用法

```xml
<arc:PiiRedaction AutoPlay="False" Message="" />
```

```csharp
card.Names = [user.Name];
card.Detector = async (text, token) => await dlp.ScanAsync(text, token);   // spans with an exclusive end
card.Sender = async (prompt, token) => await chat.CompleteAsync(prompt, token); // may contain [NAME_1], [EMAIL_1]
card.Redacted += (_, e) => audit.Log("pii.redacted", e.Entities.Select(x => x.Type));
```

`PiiRedaction` 是 Special 包（`UseArcSpecial()`）中的 `TemplatedControl`，宽度填满容器，最多 640px。既没有 `Message` 也没有 `Simulation` 时，卡片显示一个空的消息框（"Paste a message with names, emails, phone numbers or cards"），在输入内容之前 Scan 处于关闭状态；在设置 `ModelName` 之前，流程条把模型显示为 "Model"。

### 模拟

想在没有模型的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `PiiRedactionSimulation`：消息、要标记的姓名、安全提示要发往的模型，以及带占位符的模型回复。卡片没有 `Message` 时会显示模拟的消息，并循环播放扫描、发送和还原，直到有人接手；回复是模拟的，卡片会说明这一点。编辑过的消息会得到一条简短的替身回复。你自己的值优先于模拟的值；把 `Simulation` 设为 null 会停止模拟并显示空的消息框。

```csharp
card.Simulation = new PiiRedactionSimulation
{
    Message = "Hi, I'm Juniper Vale. Send the receipt to juniper.vale@example.com.",
    Names = ["Juniper Vale"],
    ModelName = "Grok 4.7",
    Reply = "Done, [NAME_1]. The receipt is on its way to [EMAIL_1].",
};
```

上方的预览播放的是一段虚构客服对话的模拟，来自图库自己的演示数据：Juniper Vale 关于重复扣款的请求，其中有一个测试卡号（4242 4242 4242 4242）、一个 example.com 邮箱和一个虚构 555 号段的电话号码。

### 结构

1. **头部**：一个盾牌、"PII guard"，以及回复为模拟时显示的 Simulated 标签。
2. **消息**：扫描前是可编辑的输入框；扫描时是带标记的原文；之后是安全提示词，每个占位符以其类型的颜色显示在圆角标记上。上方是标签（"Message"、"Safe prompt" 或 "Original message"）和"显示原文"开关。
3. **类型格**：Name、Email、Phone 和 Card，每格显示 "Not checked"、闪烁的 "Checking"、它的令牌（"EMAIL_1"，更多时为 "EMAIL_1 +1"）或 "None found"。找到内容的格子会带上浅色底色。
4. **流程条**：你、防护和模型，每一跳都有一个圆点经过。正在工作的节点标签会闪烁。
5. **回复**：模型的标记和名称位于回复上方；回复带着占位符流式出现，随后显示还原后的文本，每个放回的值下方都有标记。
6. **底部**：带图标的状态、有内容可重置时出现的 Reset，以及唯一的主操作。

### 阶段

主操作是同一个按钮，标签随阶段变化，因此键盘焦点在整个流程中保持不动。

| 阶段 | 状态 | 主操作 |
|---|---|---|
| `Idle` | Ready to scan | Scan |
| `Scanning` | Scanning for personal data | Stop |
| `Redacted` | 4 items redacted，或 No personal data found | Send |
| `Sending` | Sending the safe prompt | Stop |
| `Thinking` | Waiting for Grok 4.7 | Stop |
| `Streaming` | Grok 4.7 is replying | Stop |
| `Restoring` | Restoring original values | Stop |
| `Done` | Reply restored | Run again |
| `Error` | 异常的消息 | Retry |

扫描中按 Stop 会回到可编辑的消息；请求中按 Stop 会取消传给 `Sender` 的令牌，并回到安全提示词。Retry 会重新运行失败的步骤。Reset 取消所有进行中的操作并回到输入框。`Scan()`、`Send()`、`Stop()` 和 `Reset()` 在代码中做同样的事。

### 演练场

<Demo name="Playground" />

`AutoPlay="False"` 配合 `Message=""` 得到一个空白输入框。用户粘贴文本后按 Scan，或在输入框里按 Cmd 或 Ctrl 加 Enter。`Message` 是双向的，应用可以读取输入的内容。`IsEditable="False"` 让输入框保持只读。

### 你的检测器和你的模型

<Demo name="OwnModel" />

`Detector` 用你的 DLP 或 NER 服务替换内置检测器。它接收文本和一个取消令牌，返回 `PiiMatch` 区间（类型、起点和不含在内的终点）。`Sender` 把安全提示词发送给你的模型，并返回带占位符的回复；卡片会还原它们。没有 `Sender` 时回复是模拟的：头部显示 Simulated 标签，状态会补充 "Simulated, nothing was sent"。两者任何一个抛出异常时，都会显示异常消息并提供 Retry。

`Redacted` 报告安全提示词和每个 `PiiEntity`（类型、位置、值、令牌和占位符）。`ReplyRestored` 报告原始回复、还原后的文本以及它是否为模拟。`PhaseChanged` 跟随每一次阶段变化。`Phase`、`SafePrompt` 和 `Reply` 只读且可绑定；`ShowOriginal` 是双向的。`Entities` 保存上一次脱敏的实体，`IsSimulated` 表示回复是否为模拟。

`ModelName` 和 `ModelMark`（24 × 24 的描边图形，默认是火花）在流程条和回复上方标明模型；`ModelMarkBrush` 为标记着色。`NameBrush`、`EmailBrush`、`PhoneBrush` 和 `CardBrush` 设置各类型的颜色（默认是 Series1 到 Series4）。

### 不带卡片的辅助方法

```csharp
// Server side, before the prompt leaves your backend.
var result = PiiRedactor.Redact(message, PiiRedactor.Detect(message, [user.Name]));
var reply = PiiRedactor.Restore(await model.CompleteAsync(result.Prompt), result.Entities);
```

| 成员 | 作用 |
|---|---|
| `PiiRedactor.Detect(text, names)` | 找出邮箱、卡号（13 到 19 位并通过 Luhn 校验）、电话号码（7 到 15 位）和姓名（名单中的姓名，以及 "I'm"、"I am"、"this is" 和 "my name is" 后面首字母大写的词），按位置排序且不重叠 |
| `PiiRedactor.Redact(text, matches)` | 返回包含安全提示词和实体的 `PiiRedactionResult`；第一个之后与之重叠的区间会被跳过，重复的值保留同一个令牌 |
| `PiiRedactor.Restore(reply, entities)` | 放回原始值，未知令牌保持原样 |
| `PiiRedactor.PassesLuhn(value)` | 对一个值中的数字做 Luhn 校验 |

| 类型 | 成员 |
|---|---|
| `PiiType` | `Name`、`Email`、`Phone`、`Card` |
| `PiiRedactionPhase` | `Idle`、`Scanning`、`Redacted`、`Sending`、`Thinking`、`Streaming`、`Restoring`、`Done`、`Error` |
| `PiiMatch` | `Type`、`Start`、`End`（不含在内）、`Length` |
| `PiiEntity` | `Type`、`Start`、`End`、`Value`、`Token`（`EMAIL_1`）、`Placeholder`（`[EMAIL_1]`）、`PromptStart`、`PromptEnd` |
| `PiiRedactionResult` | `Prompt`、`Entities` |

### 窄容器

<Demo name="Narrow" />

布局跟随卡片自身的宽度。窄于 520px 时，四个类型格变成两行，每行两个。窄于 400px 时，卡片收紧内边距和间距并隐藏 Simulated 标签（状态仍会播报），过长的模型名称在流程条中以省略号截断。文字图层叠放在同一个单元格中，回复区预留模拟回复还原后的高度，因此在 360px、390px 或桌面宽度下都不会跳动。

### 内容指南

- 在系统提示词中要求模型原样保留占位符（"Refer to [EMAIL_1] exactly as written"），以便防护能把它们还原。
- 如果模型会回头引用某个值，请在整个对话中为它保留同一个令牌：`Redact` 每次调用单独编号，所以要在轮次之间保存实体映射。
- 示例中的样例数据必须一看就是假的：虚构的姓名、example.com 邮箱、555 号码和测试卡号。

### 最佳实践

**建议**

- 在生产环境中于服务端脱敏，并把实体保存在请求范围内。
- 在产品界面中设置 `AutoPlay="False"`。
- 只记录被脱敏内容的类型，绝不要把值和提示词记录在一起。

**避免**

- 把内置检测器当作完整方案：名单之外的姓名和简单自我介绍之外的情况需要 NER 模型。
- 把原始消息发送到卡片标示为安全的任何地方。

### 常见问题

**自动播放什么时候停止？** 只要有人编辑消息、切换"显示原文"或按下任何按钮，就永久停止。它只在卡片至少四分之一位于屏幕内时运行，减弱动效时从不运行。

**卡片不认识的占位符会怎样？** 保持原样，所以模型编造的 `[CARD_7]` 会原样显示。

**能只保留安全提示词而不显示回复吗？** 让卡片停在 `Redacted` 并读取 `SafePrompt`；是否发送由用户决定。

## API 参考

<!-- api: PiiRedaction -->

<!-- api: PiiRedactionLabels -->

<!-- api: PiiRedactionSimulation -->

<!-- api: PiiRedactedEventArgs -->

<!-- api: PiiReplyRestoredEventArgs -->

<!-- api: PiiRedactionPhaseChangedEventArgs -->

## 无障碍

- 一个礼貌的状态区域会播报每个阶段（"Scanning for personal data"、"4 items redacted"、"Reply restored"），回复为模拟时还会补充 "Simulated, nothing was sent"。
- 状态从不只依赖颜色：每个阶段都把图标和简短标签配对，每个类型格读出名称及其令牌或 "None found"，原文会列出它的标记（"juniper.vale@example.com: Email, redacted as EMAIL_1"）。
- Scan、Stop、Send、Run again 和 Retry 是同一个按钮，只是标签改变，所以焦点保持不动。Reset 会把焦点送回消息输入框。
- 只有可见的文字图层会暴露给辅助技术；用来保持高度稳定的隐藏图层不会。
- 流程条是装饰性的，对屏幕阅读器隐藏，因为状态区域已经播报了每一跳。
- "显示原文"是开关自身的标签，在有安全提示词之前开关被禁用并变暗。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过消息输入框（扫描前）、有安全提示词后的"显示原文"、显示时的 Reset，以及主操作 |
| Cmd + Enter、Ctrl + Enter | 从输入框开始扫描；焦点移到主操作，此时它显示 Stop |
| Space、Enter | 切换"显示原文"，或按下 Scan、Stop、Send、Run again、Retry 或 Reset |

## 动效

- 扫描逐个标记实体，间隔 120ms，每个以其类型颜色在 160ms 内着色，同时对应的类型格从 "Checking" 交叉淡化为它的令牌。
- 换成占位符是两个叠放文字图层之间的一次交叉淡化（旧图层 100ms 淡出，新图层 160ms 淡入），因此没有词会滑动或重新换行，高度也从不改变。"显示原文"使用同样的交叉淡化。
- 流程的每一跳是一个圆点以缓入缓出曲线在 460ms 内走完连接线；正在工作的节点标签会闪烁。
- 回复每 34ms 流入三到六个词，每块只在 160ms 内淡入，然后一次交叉淡化为还原后的文本。
- 标签、状态和 Reset 按钮原地交叉淡化；没有回弹，状态之间也没有尺寸变化。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标记 | 逐个出现，间隔 120ms，160ms 着色 | 同时出现 | 同时出现 |
| 图层切换 | 100ms 淡出，160ms 淡入 | 100ms 淡出，160ms 淡入 | 立即 |
| 流程跳转 | 圆点移动 460ms | 无 | 无 |
| 回复 | 分块流入，然后交叉淡化 | 整段显示，然后交叉淡化 | 立即 |
| 自动播放 | 在屏幕内时循环 | 关闭 | 关闭 |

## 相关组件

- `ToolCall`：对话记录里任意的工具步骤。
- `AgentStatus`：整个智能体运行的实时状态。
- `Switch`：单独使用的开关。
