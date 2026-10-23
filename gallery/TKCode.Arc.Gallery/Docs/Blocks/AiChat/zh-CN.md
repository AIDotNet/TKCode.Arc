---
title: AI 对话
summary: 一个填满容器的 AI 对话界面：顶部是标题菜单、模型按钮和新建对话按钮，线程中有流式输出的 Markdown 回复、可折叠的推理、工具胶囊、编号引用和消息操作，底部是消息输入框。
source-hash: 6a840f2320f1
---

<Demo name="Preview" />

## 何时使用

AI 对话是助手的整个对话界面：过往对话的列表、线程、模型选择，以及人们撰写消息的输入框。回复以流的形式到达，会展示模型在过程中做了什么（推理、搜索、查询），并引用其来源。

- **产品的主要助手**：一个页面或抽屉，人们在其中提问、追问、重试和评价。
- **仪表盘卡片中的多轮助手**：它会适应自身宽度，一直缩小到手机大小的一列。
- **需要核对答案的场合**：引用在悬停时预览其来源，工具胶囊可展开，显示所用的搜索词或执行过的查询。
- **可以编辑的对话**：编辑消息或重试回复会为该轮次生成一个新版本；人们可以回到较早的版本。

## 何时不用

- **只需要一个提示框**：使用 `AiComposer`。
- **人与人之间的聊天**，带有参与者和已读回执：使用 `ChatThread`。
- **一个问题加一篇长回答**：带 `Markdown` 的一页文本更简单。
- **在设置页中选择模型**：使用 `ModelPicker`。
- **运行模型**：对话本身不会调用任何东西。它向你的 `IAiChatResponder` 请求回复，或者等待你的代码填入回复。

## 用法

`AiChat` 派生自 Avalonia 的 `TemplatedControl`，并填满其容器，因此请为它设置高度。设置 `Responder` 即可作答，提供 `Models` 和一个会话，并处理你需要的事件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiChat Height="640" ColumnWidth="760" GreetingText="What should we look at today?">
        <arc:AiChat.Models>
            <arc:AiModel Id="orbit-pro" Name="Orbit Pro 3" Note="Northwind Labs" />
            <arc:AiModel Id="quill-2" Name="Quill 2.4" Note="Fernwood AI" />
        </arc:AiChat.Models>
    </arc:AiChat>
</UserControl>
```

### 用响应器作答

`IAiChatResponder` 接收所显示分支的消息，并返回一个事件流。对话会在每个事件到达时将其显示出来；你不需要接触视图。

```csharp
public sealed class MyResponder : IAiChatResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiChatReasoningDelta("Compare the stage timings before and after the jump.");
        yield return new AiChatReasoningEnd();
        yield return new AiChatToolUpdate("search", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching the docs");
        // ... the same id again with Status Done, a summary, the queries and the sources
        yield return new AiChatSourcesEvent(sources);              // before the text that cites them
        await foreach (var chunk in model.StreamAsync(request, cancellationToken))
        {
            yield return new AiChatTextDelta(chunk);                // any size; the chat shows whole words on a steady clock
        }
    }
}
```

推理使用 `AiChatReasoningDelta` 和 `AiChatReasoningEnd`，工具调用使用 `AiChatToolUpdate`（相同 id 的更新会改变同一个工具），来源使用 `AiChatSourcesEvent`，回复文本使用 `AiChatTextDelta`，失败则使用 `AiChatErrorEvent`。抛出异常与发送错误事件的效果相同。当人们停止回复时，该取消令牌会被取消。

<Demo name="Empty" />

### 对话线程

会话是一个 `AiChatConversation`，带有标题及其 `Turns`。每个 `AiChatTurn` 都有若干版本；一个版本包含提示（`AiChatUserMessage`：文本和附件）、回复（`AiChatReply`），以及跟在它之后的轮次。显示的是**可见分支**：每个轮次中被选中的版本，然后是它之后的延续。响应器只接收该分支。

回复带有其状态（思考中、流式输出、完成、已停止或失败）、推理、工具调用（`AiChatTool`）、来源（`AiSource`）、文本以及人们的反馈。文本使用 Markdown 的一个子集：段落、列表、短标题、粗体、行内代码、代码块，以及按来源到达顺序编号的 `[1]` 引用。

### 推理、工具与引用

模型思考时，回复的开头会显示闪烁的 "Thinking"；思考结束后它变为 "Thought for 3s"，并可展开查看推理。每次工具调用都是一个工具胶囊：运行中（带旋转指示器，尚不能展开）、已完成（带图标、来源标记和简短摘要，例如 "3 sources"），或失败。搜索胶囊展开后显示其搜索词和编号来源；查询胶囊展开后显示其语句和结果；自定义工具则使用你自己的模板（`ToolDetailTemplate`）。

文本中的 `[1]` 会变成一个小的编号胶囊。指向它或使其获得焦点时，会显示一张卡片，包含站点、标题和摘录；按下它会引发 `CitationInvoked`，除非你处理了该事件，否则来源的 `Url` 会被打开。

### 版本

对已完成的回复重试，会为该轮次添加一个版本；编辑消息并发送，同样会添加一个版本。操作栏中会出现版本切换器（"2 / 2"），版本之后的轮次属于该版本，因此回到版本 1 时，它的后续内容也会回来。对失败的回复重试会在原处再次运行，不添加版本。

<Demo name="Versions" />

### 停止与失败

生成回复期间，发送按钮变为停止按钮，在输入框中按 Escape 也会停止。已停止的回复保留已显示的内容，并显示 "Stopped"（或 "Stopped before answering"）；失败的回复会显示带有重试按钮的错误卡片。两者都保留操作栏，失败的回复除外，它没有操作栏。

### 没有响应器

没有 `Responder` 时，发送的消息只出现在线程中，并引发 `MessageSent`；你的代码填入它收到的 `AiChatReply`，视图会跟随其变化。

<Demo name="HostDriven" />

### 会话、模型与新对话

标题按钮会打开 `Conversations` 列表，按 `Group` 分组，其中 "New chat" 排在最前。打开一个会话会引发 `ConversationOpened`；每个会话都保留自己的滚动位置，已完成的回复不会重新播放。模型按钮会打开 `Models`；`SelectedModel` 是双向的，`ModelChanged` 报告变化。新回复使用所选模型；已有的回复保留撰写它的模型的标记。"New chat" 会引发 `NewChatRequested`；如果没有人处理它，对话会清空 `CurrentConversation`，显示带有问候语的空对话，以及最多三条 `Suggestions`。空对话中的第一条消息会创建一个会话（`ConversationCreated`），其标题取自该消息的前六个词。

### 宽度

对话跟随自身的宽度：在 480 及以下时，气泡变宽，来源域名隐藏；在 360 及以下时，按钮收紧。`ColumnWidth`（760）设置轮次列的宽度。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`AiChatLabels`）：要翻译或改写时，替换该实例即可；带格式的文字是函数。设置了 `GreetingText` 和 `PlaceholderText` 时，它们优先于标签。

### 文案规范

- **问候语**：一个邀请人们布置任务的问题，例如 "What should we look at today?"（今天我们先看什么？）。
- **建议**：至多三条，每条都是一个真实的任务，并用人们自己会说的话来表述。
- **工具标签**：运行时用现在时动词（"Searching the docs"，正在搜索文档），完成后用过去时（"Searched the docs"，已搜索文档）；摘要要简短，例如 "3 sources"、"212 rows"、"Timed out"。
- **来源**：人们认得的标题、域名，以及一到两句的摘录。

### 最佳实践

- 在引用来源之前先发送来源，并按此顺序从 1 开始为引用编号。
- 保持工具摘要简短，把详细内容放在面板中。
- 让人们能够停止：始终传递取消令牌。
- 不要隐藏失败：错误事件会给人们一个在原处再次运行的重试按钮。

### 常见问题

**为什么发送的消息没有得到回答？** 没有 `Responder` 时，对话会等待你的代码填入回复。

**切换会话会停止回复吗？** 不会。正在运行的回复会继续运行，已完成的回复在你回来时立即显示。

**我能显示自己的工具详情吗？** 可以：类型为 `Custom` 的工具会用 `ToolDetailTemplate` 绘制，并以该工具作为数据上下文。

## API 参考

<!-- api: AiChat -->

<!-- api: AiChatConversation -->

<!-- api: AiChatTurn -->

<!-- api: AiChatTurnVersion -->

<!-- api: AiChatUserMessage -->

<!-- api: AiChatReply -->

<!-- api: AiChatTool -->

<!-- api: AiSource -->

<!-- api: AiChatSuggestion -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `MessageSent` | 人们发送消息，包括编辑后的消息 | `Conversation`、`Turn`、`Prompt` |
| `ReplyCompleted` | 回复结束：完成、停止或失败 | `Reply` |
| `FeedbackChanged` | 回复被评价，或评价被撤回 | `Reply`、`Value` |
| `ModelChanged` | 人们选择了另一个模型 | 旧模型和新模型 |
| `NewChatRequested` | 人们请求新对话；设置 `Handled` 以自行处理 | |
| `ConversationOpened` | 从菜单中打开一个会话 | `Conversation` |
| `ConversationCreated` | 空对话中的第一条消息创建了一个会话 | `Conversation` |
| `CitationInvoked` | 按下一个引用或来源行；设置 `Handled` 以自行打开 | `Item` |

### 响应器

| 类型 | 说明 |
|---|---|
| `IAiChatResponder` | `RespondAsync(request, cancellationToken)` 以事件流的形式返回回复 |
| `AiChatRequest` | 会话、所选模型以及所显示分支的消息，末尾是需要作答的提示 |
| `AiChatReasoningDelta` / `AiChatReasoningEnd` | 更多推理文本；推理的结束，附带可选的时长 |
| `AiChatToolUpdate` | 工具调用开始或发生变化（相同的 `Id` 会更新同一个工具） |
| `AiChatSourcesEvent` | 文本将要引用的来源 |
| `AiChatTextDelta` | 更多回复文本，大小不限 |
| `AiChatErrorEvent` | 回复失败，附带要显示的消息 |

## 无障碍

- 对话是一个名为 "AI chat" 的区域；线程是轮次的列表，每条消息和回复都以其作者命名。
- 标题按钮名为 "Chats, current: …"，模型按钮名为 "Model: …"；两者都报告它们会打开一个带有已选中项的菜单。
- 一个礼貌式实时区域会播报：回复已开始（"Orbit Pro 3 is replying"）、已完成、已停止或失败，以及复制、评价、版本变化、新模型和打开会话。流式输出的词不会逐个播报。
- 推理按钮报告其是否已展开；工具胶囊报告其状态，运行中时不能展开；版本切换器是一个名为 "Version 2 of 2" 的组。
- 引用按阅读顺序获得焦点，获得焦点时打开其卡片，按 Escape 关闭卡片且不移动焦点。
- 操作按钮都有名称和工具提示；它们在悬停和键盘焦点时出现，并且不占用额外空间。
- 焦点回到人们原来所在的位置：选择会话后回到标题按钮，发送或取消编辑后回到编辑按钮，选择 "New chat" 之后回到输入框。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 标题按钮、新对话、模型按钮、线程中的控件（按阅读顺序）、消息输入框 |
| 按钮上的 `Enter` 或 `Space` | 按下该按钮；在标题或模型按钮上，打开菜单 |
| 菜单中的 `ArrowUp` / `ArrowDown` | 在各行之间移动；字母键跳到匹配的行 |
| 消息输入框中的 `Enter` | 发送（生成回复期间，或输入法正在组字时不发送） |
| `Shift+Enter` | 添加一行 |
| 消息输入框中的 `Escape` | 停止正在生成的回复 |
| 消息编辑器中的 `Enter` / `Escape` | 发送编辑 / 取消编辑 |
| 引用上的 `Escape` | 关闭其卡片，且不移动焦点 |

## 动效

- 流式文本按 34 毫秒的节拍以整词释放（每一步约三个词），每一步在 150 毫秒内淡入；已完成的回复立即显示。
- 推理和工具详情以弹簧展开，其文字在 160 毫秒内淡入，100 毫秒内淡出。
- 胶囊和标签以弹簧方式取得所需的宽度；"Thinking" 在推理结束之前持续闪烁。
- 线程在底部跟随新内容，每一帧缩短剩余距离的五分之一；向上滚动会停止跟随，跳转按钮可以将其带回底部。
- 切换会话时，线程交叉淡入淡出；版本以新的高度淡入。
- 减弱动效会去掉弹簧和移动，保留分批的文字但不淡入；关闭动效时，每项变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新文字 | 每 34 毫秒显示整词，150 毫秒淡入 | 相同的分批，无淡入 | 相同的分批，无淡入 |
| 推理、工具详情 | 高度弹簧，淡入淡出 | 120 毫秒淡入淡出 | 立即 |
| 跟随底部 | 追赶末尾 | 直接跳到末尾 | 直接跳到末尾 |
| 切换会话 | 100 / 160 毫秒交叉淡入淡出 | 120 毫秒交叉淡入淡出 | 立即 |
| 闪烁、旋转指示器 | 移动 | 静止 | 静止 |

## 相关组件

- `AiComposer`：只需要提示框、不需要整个线程时使用。
- `ModelPicker`：单独选择模型时使用。
- `AgentMemory`：查看助手记住的内容。
- `Markdown`：仅用于单独渲染回复的文本。

| 组件 | 适用场景 |
|---|---|
| `AiComposer` | 你需要的是输入框，而不是整个对话 |
| `ChatThread` | 对话发生在人与人之间 |
| `ModelPicker` | 模型选择位于设置页 |
| `Markdown` | 你只需要渲染文本 |
