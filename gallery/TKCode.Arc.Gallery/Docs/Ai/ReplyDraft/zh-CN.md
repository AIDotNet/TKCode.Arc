---
title: Reply draft
summary: 收到的邮件下方的 AI 回复起草器：快捷回复引导一份新的流式草稿，语气与长度以一次交叉淡化改写它，读者在插入前可以编辑。
source-hash: 19e28ee0c566
---

<Demo name="Preview" />

## 何时使用

`ReplyDraft` 显示一封收到的邮件，并在其下方给出 AI 回复。模型以快速、柔和淡入的片段流式写出草稿；快捷回复标签引导一份新的草稿，语气与长度两组选项以一次交叉淡化改写已完成的草稿，读者可以在按下 Insert 之前就地修改某个词。Insert 把文字（包含读者的修改）交给你的编辑器：卡片本身从不发送任何内容。

- **用模型起草回复**：回复邮件、工单或消息，由人审阅并编辑后再发送。
- **提供意图以及语气与长度选项**：用于收件箱、客服台或 CRM 中生成的文字。
- **在落地页展示 AI 写作功能**：一个会自动播放的逼真示例。

## 何时不用

- **与模型自由对话**：使用聊天线程与输入框；这里的草稿只回复一条消息。
- **输入时的行内自动补全**：在光标处使用幽灵文字，而不是单独的草稿框。
- **发送邮件**：此卡片只插入草稿，发送由你的编辑器负责。

## 用法

把消息、你的快捷回复以及一个流式输出模型回复的生成器交给卡片。每个新操作（标签、语气或长度的变化、Regenerate）都会取消上一个请求的令牌，所以请把它传给你的客户端。

```csharp
draft.Message = new ReplyDraftMessage
{
    FromName = email.From.Name,
    FromEmail = email.From.Address,
    Subject = email.Subject,
    Body = email.PlainText,          // blank lines split paragraphs
    ReceivedAt = email.Received,
};
draft.Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good"), new ReplyDraftSuggestion("later", "Next week")];
draft.Model = new AgentModel("Mistral Medium 3.5");
draft.Generator = request => replies.StreamAsync(email.Id, request.Tone, request.Length,
    request.Suggestion?.Id, request.Mode, request.Previous, request.CancellationToken);
draft.InsertHandler = async args => await composer.InsertAsync(args.Draft);
```

你自己的邮件如果没有 `Generator`，就无法起草：草稿框显示提示行，"Draft reply"按钮处于关闭状态。既没有 `Message` 也没有 `Simulation` 时，卡片是空的：一封空白邮件，没有快捷回复，显示"No reply drafted yet."，"Draft reply"按钮处于关闭状态。

### 模拟

想在没有模型的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `ReplyDraftSimulation`：邮件、快捷回复、要标注的模型，以及你为每个快捷回复、语气和长度写好的回复（`ReplyDraftVersion`）。卡片会在短暂的阅读之后流式输出匹配的回复，说明这是模拟、没有发送任何内容，并循环演示语气、长度、第二个快捷回复和插入，直到有人在卡片内按下或输入。你自己的 `Message` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示空卡片。在产品中请关闭 `AutoPlay`。

```csharp
draft.Simulation = new ReplyDraftSimulation
{
    Message = email,
    Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good")],
    Model = new AgentModel("Mistral Medium 3.5"),
    Replies =
    [
        new ReplyDraftVersion("yes", ReplyDraftTone.Friendly, ReplyDraftLength.Medium, "Hi Maya,\n\nSounds good!\n\nSam"),
        new ReplyDraftVersion("yes", ReplyDraftTone.Formal, ReplyDraftLength.Medium, "Dear Maya,\n\nThat suits me.\n\nSam"),
    ],
};
```

`ReplyDraftSimulation.Generate` 以 `Generator` 的形式流式输出同样的回复，供你自行包装模拟时使用。上方的预览播放的就是一个模拟，其邮件和回复来自图库自己的演示数据。

### 结构

1. **邮件**：邮件图标、主题与时间；发件人的头像、姓名、地址与收件人；正文缩进在头像下方。
2. **回复标题**：模型的标记与 "Reply with ..."，右侧是状态：一个图标加一个词，模型工作时会闪烁。
3. **快捷回复**：可切换的标签；正在引导草稿的那个以强调色填充。
4. **草稿框**：草稿在这里流式出现，完成后变为可编辑的文字。
5. **语气与长度**：两个带滑动胶囊的小型单选组，中间以细线分隔。
6. **操作**：键盘提示、Regenerate 与 Insert。

### 状态

| Status | 草稿框显示 | 状态行 |
|---|---|---|
| `Idle` | 一行闪烁的 "Reading Maya's email"，或在 `AutoStart` 关闭时显示 Draft reply | "No draft yet" |
| `Drafting` | 正在流式写出的草稿 | 闪烁的 "Drafting" |
| `Rewriting` | 变暗且只读的旧草稿 | 闪烁的 "Rewriting" |
| `Ready` | 可编辑的草稿 | 对勾与 "Ready to edit" |
| `Inserting` | 草稿；Insert 显示加载圈 | 闪烁的 "Inserting" |
| `Inserted` | 草稿 | 双对勾与 "Inserted" |
| `Failed` | 错误信息与 Try again | 警示图标与 "Couldn't draft" |

### 使用你自己的模型

<Demo name="OwnData" />

`Generator` 返回由文字增量组成的 `IAsyncEnumerable<string>`。当你的服务一次返回整段回复时，用 `ReplyDraftStreams.FromText` 包装它，卡片会在本地以每块 3 到 8 个词的片段流式显示。请求的 `Mode` 为 `Draft` 表示新的回复，为 `Rewrite` 表示已完成的草稿改变了语气或长度：此时请改写 `Previous`，它保存屏幕上包含读者修改的文字。抛出异常即可显示错误状态。

每当一份草稿或改写完成，`DraftCompleted` 就会触发，并带有文字、语气、长度与快捷回复。

### 插入

<Demo name="Insert" />

Insert，或在卡片内任意位置按 Cmd+Enter（Ctrl+Enter），会先引发 `InsertRequested`，再等待 `InsertHandler`，其间按钮上显示加载圈。处理程序抛出异常时保留草稿，并显示 "Couldn't insert"。既没有处理程序，也没有把 `InsertRequested` 标记为已处理的事件处理程序时，插入是模拟的，播报会说明没有发送任何内容。

### 按需开始

`AutoStart` 会在卡片滚动进入视图时立即写出第一份草稿。将它设为 false 则改为显示一行简短说明和 Draft reply 按钮，只在读者要求时才运行模型。`StartDraft()` 与 `Regenerate()` 在代码中做同样的事。

### 窄容器

<Demo name="Narrow" />

卡片填满容器，最宽 600px。低于 480px 时内边距降为 16px，正文不再缩进到头像下方；低于 460px 时两组之间的细线隐藏，Length 移到单独一行。标签会换行，过长的姓名与地址会截断，不会出现横向滚动。触摸按下之后，选项增大到 32px，标签增大到 36px。

### 自定义文案

`Labels` 包含卡片显示或播报的每个词；替换整个对象即可本地化或改写：

```csharp
draft.Labels = new ReplyDraftLabels
{
    Ready = "Prêt à modifier",
    Insert = "Insérer",
    ReplyWith = model => "Répondre avec " + model,
};
```

`SourceMark` 用你自己的 24 × 24 描边标记替换主题前的邮件图标，`AgentModel.Mark` 替换模型的星芒。

### 内容准则

- 快捷回复的标签用两三个词（"Sounds good"、"Next week"）；它们的 id 是你的提示词能理解的内容。
- 草稿保持纯文本：草稿框是纯文本编辑器。
- 在你自己的编辑器中说明插入会做什么；卡片只负责交出文字。

### 最佳实践

- 把请求的取消令牌传给模型客户端：每个新操作都会中止上一个请求。
- 把 `Rewrite` 当作对 `Previous` 的改写，这样读者的修改在语气变化后仍然保留。
- 在产品中关闭 `AutoPlay`；它用于营销页与文档页。

## API 参考

<!-- api: ReplyDraft -->

<!-- api: ReplyDraftMessage -->

<!-- api: ReplyDraftSuggestion -->

<!-- api: ReplyDraftRequest -->

<!-- api: ReplyDraftGenerator -->

<!-- api: ReplyDraftInsertHandler -->

<!-- api: ReplyDraftStreams -->

<!-- api: ReplyDraftSimulation -->

<!-- api: ReplyDraftVersion -->

<!-- api: ReplyDraftLabels -->

<!-- api: ReplyDraftEventArgs -->

<!-- api: ReplyDraftSuggestionEventArgs -->

<!-- api: ReplyDraftToneChangedEventArgs -->

<!-- api: ReplyDraftLengthChangedEventArgs -->

<!-- api: ReplyDraftTone -->

<!-- api: ReplyDraftLength -->

<!-- api: ReplyDraftMode -->

<!-- api: ReplyDraftStatus -->

## 无障碍

- 卡片以发件人命名（"Email from Maya Chen"）；回复区域是一个有名称的区块。
- 快捷回复是会报告是否按下的切换按钮；语气与长度是单选组，只有选中的选项是 Tab 停靠点。
- 完成的草稿是名为 "Draft reply" 的纯文本可编辑文本框，读者可以在 Insert 之前修改某个词；Insert 交出的就是修改后的内容。
- 礼貌型实时区域会播报起草、改写、就绪、已插入与错误；模拟的操作会说明没有发送任何内容。演示自动播放时它保持安静。
- 状态使用图标加一个短词，从不只靠颜色。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过快捷回复、草稿文字、语气、长度、Regenerate 与 Insert |
| 方向键 | 在 Tone 或 Length 中移动选择并改写草稿 |
| Home、End | 在 Tone 或 Length 中跳到第一个或最后一个选项 |
| Enter、Space | 激活快捷回复、选项或按钮 |
| Cmd+Enter、Ctrl+Enter | 在卡片内任意位置插入已完成的草稿 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 草稿流式出现 | 每 34ms 一块 3 到 8 个词；每块在 140ms 内淡入 | 草稿整体出现 | 草稿整体出现 |
| 语气或长度改写 | 旧草稿变暗，在 100ms 内淡出；新草稿在 160ms 内淡入并上升 2px | 立即 | 立即 |
| 草稿框高度 | 弹簧过渡到新高度（0.26s，无回弹）；重新生成期间保持高度直到新草稿完成 | 立即 | 立即 |
| 语气与长度的胶囊 | 以同样的无回弹弹簧滑动 | 直接跳到位 | 直接跳到位 |
| 状态 | 交叉淡化（淡出 100ms，淡入 160ms）；工作中的词会闪烁 | 交叉淡化，不闪烁 | 立即 |
| 按下标签或选项 | 缩小到 0.97 | 不缩放 | 不缩放 |

## 相关组件

- `Textarea`：由人自己书写的普通多行输入框。
- `ToolCall` 与 `AgentStatus`：展示智能体正在做什么。
- `SegmentedControl`：在少量视图之间切换。
