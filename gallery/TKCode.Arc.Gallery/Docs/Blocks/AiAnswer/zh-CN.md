---
title: AI 回答
summary: 像文章一样回答一个问题的页面：推理、来源卡片、流式输出的回答（含标题、列表、表格、代码以及编号引用，引用会点亮其所支持的句子），然后是操作、相关问题和追问框。
source-hash: 871e3dc49657
---

<Demo name="Preview" />

## 何时使用

AI 回答用于值得单独成页的问题，而不是聊天气泡。它展示答案是如何得出的（推理和来源），以完整的排版撰写回答，
并让人们核对每一条论断：指向某个引用时，会显示该句话的出处。

- **搜索或研究助手**：一个问题进入，一个带引用的回答输出，来源以卡片形式显示在文本上方。
- **帮助中心的答案**：页面把问题保留为标题，因此可以作为链接分享或复制。
- **需要核对的答案**：引用、来源卡片和它们所支持的句子会相互点亮，两个方向都是。
- **可以原地改进的答案**：重新回答、更简洁、更详细或换一个模型，并在各个版本之间翻页。

## 何时不用

- **来回对话**：使用 `AiChat`。在这里再次提问会原地替换回答。
- **只需要一个提示框**：使用 `AiComposer`。
- **没有引用的纯文本**：用 `Markdown` 渲染。
- **运行模型**：本块本身不会调用任何东西。它向你的 `IAiAnswerResponder` 请求回答，或显示你给它的答案。

## 用法

`AiAnswer` 派生自 Avalonia 的 `TemplatedControl`，并填满其容器，因此请为它设置高度。设置 `Responder` 和 `Model`，或者直接给它一个完成的 `Answer`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiAnswer Height="720" Question="Which checks should block a merge during the move?">
        <arc:AiAnswer.Models>
            <arc:AiModel Id="lumen-pro" Name="Lumen Pro 4" />
            <arc:AiModel Id="lumen" Name="Lumen 4" />
        </arc:AiAnswer.Models>
    </arc:AiAnswer>
</UserControl>
```

### 用响应器作答

`IAiAnswerResponder` 接收问题、提问方式和模型，并以事件流的形式返回答案。

```csharp
public sealed class MyResponder : IAiAnswerResponder
{
    public async IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiAnswerSourcesEvent(sources);            // early, so the sources row fills while the model thinks
        yield return new AiAnswerReasoningEvent("The risk is not the new pipeline...");
        yield return new AiAnswerThoughtEvent();                   // the label turns to "Thought for 8s"
        await foreach (var chunk in StreamAnswerAsync(request, cancellationToken))
        {
            yield return new AiAnswerTextEvent(chunk);              // Markdown, of any size
        }
        yield return new AiAnswerDoneEvent(Tokens: 812, Duration: TimeSpan.FromSeconds(18.6), FollowUps: followUps);
    }
}
```

`request.Mode` 为 `Answer`、`Retry`、`Concise`、`Detailed` 或 `SwitchModel` 之一，`PreviousAnswerMarkdown` 是被替换的回答。当人们停止回答、
提出其他问题或离开时，该取消令牌会被取消。抛出异常会使回答失败，并显示一条带有 "Try again" 按钮的提示。

### 已完成的回答

没有响应器时，设置 `Question`、`Answer`（带有 `[1]` 引用的 Markdown）、`Sources`、`Reasoning`、`Usage` 和 `FollowUps`，块会整体显示它们：没有逐步显现，也没有追问框。

<Demo name="Static" />

### 推理与来源

模型思考时，推理行会闪烁显示 "Thinking"；文本开始后，它变为 "Thought for 8s"，并可展开查看推理，推理仍在撰写时也可以展开。
来源行最多显示四个重叠的站点标记和 "N sources"；展开后，来源是两列的卡片，或者在块较窄时（576 及以下）是一行可横向滚动的卡片。
每张卡片都是一个链接：它会引发 `SourceInvoked`，除非你处理该事件，否则会打开它的 `Url`。`IsReasoningExpanded` 和 `IsSourcesExpanded` 都是双向的。

<Demo name="Narrow" />

### 引用点亮句子

指向文本中的引用或使其获得焦点时，它之前的句子、其来源卡片以及引用胶囊本身都会点亮；稍后会出现一张预览卡片，包含站点、标题、摘录和域名。
反过来，指向某张来源卡片时，所有引用了它的句子及其胶囊都会点亮。在窄宽度下，被点亮的卡片会滚动到可见位置。

### 版本

使用 "Try again"、"More concise"、"More detailed" 或 "Switch model"（`Models` 中除当前模型以外的模型，位于子菜单中）重新回答，会为该回答添加一个**版本**。
操作栏中会出现翻页器（"2 / 2"）；每个版本都保留自己的模型、文本、成本、评分以及是否被停止。`CurrentVersionIndex` 是双向的，`Versions` 列出所有版本。

### 反馈

点赞会立即记录。点踩会打开一个简短的原因列表（`FeedbackReasons`，或来自 `Labels` 的默认值）；选择一个原因会记录评分并感谢提出反馈的人，列表随后自动关闭。
再次按下同一评分会将其撤回；两种评分互斥，且每个版本各自独立。`FeedbackChanged` 报告评分值、原因、问题和版本。

### 复制与分享

"Copy answer" 会把文本及其编号来源放到剪贴板上。链接按钮和 "Share thread" 复制的是一个由**你的代码**通过 `ShareLinkProvider` 提供的链接：未设置时，两个按钮都会隐藏，因为本块从不复制它自己编造的链接。`Copied` 报告复制了什么。

### 提问

回答下方的输入框用于追问；相关问题（`FollowUps`，或响应器通过 `AiAnswerDoneEvent` 发送的问题）则会在原处提问。提问会替换整个页面：问题、标题和回答都会更改，之前的版本会被丢弃。
`QuestionAsked` 会先被引发，并带有其来源；设置 `Cancel` 则不提问。`Stop()` 或 Escape 会停止回答：已写出的内容保留，并附带一条说明其可能不完整的提示，以及一个 "Try again" 按钮。

<Demo name="Empty" />

### 空页面

没有问题和回答时，页面显示一个标题、一行说明，以及 `Suggestions` 列表；"New thread" 和 "Share" 处于禁用状态，输入框获得焦点。
`NewThread()` 和 `NewThreadCommand` 会回到此页面。

### 文案

所有文字都来自 `Labels`（`AiAnswerLabels`）：要翻译或改写时，替换该实例即可；带格式的文字是函数。设置了 `Title` 时，它优先于问题。

### 文案规范

- **问题**：完整的问题，就像人们会提出的那样。
- **回答**：先用一句直接作答的开场句，然后是简短的小节；引用论断，而不是整段。
- **来源**：人们认得的标题、域名、日期，以及一到两句的摘录。
- **推理**：几句话，说明模型将要做什么，而不是逐字记录。

### 最佳实践

- 在引用它们的文本之前先发送来源，并从 1 开始为其编号。
- 始终传递取消令牌，使停止真正生效。
- 提供真实的分享链接，或者不提供。
- 相关问题保持在三到四个，并且要能由来源回答。

### 常见问题

**为什么没有对话？** 本块一次只回答一个问题；需要对话时请使用 `AiChat`。

**为什么复制按钮包含来源？** 只有连同其出处一起，回答才值得复制。

**为什么分享按钮不见了？** 没有设置 `ShareLinkProvider`。

## API 参考

<!-- api: AiAnswer -->

<!-- api: AiAnswerVersion -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `QuestionAsked` | 问题即将被回答；设置 `Cancel` 则不提问 | `Question`、`Origin` |
| `FeedbackChanged` | 回答被评分、下评分时获得原因，或评分被撤回 | `Value`、`Reason`、`Question`、`Version`、`VersionIndex` |
| `SourceInvoked` | 按下来源卡片或引用；设置 `Handled` 以自行打开 | `Item`、`FromCitation` |
| `Copied` | 有内容被复制 | `Kind`（`Answer`、`ShareLink`、`ThreadLink`、`Code`）、`Text` |
| `StatusChanged` | 状态发生变化 | `OldStatus`、`NewStatus` |

### 响应器

| 类型 | 说明 |
|---|---|
| `IAiAnswerResponder` | `RespondAsync(request, cancellationToken)` 以事件流的形式返回答案 |
| `AiAnswerRequest` | 问题、模式、模型以及被替换的回答 |
| `AiAnswerReasoningEvent` | 更多推理文本 |
| `AiAnswerSourcesEvent` | 来源；请尽早发送 |
| `AiAnswerThoughtEvent` | 推理结束，文本开始 |
| `AiAnswerTextEvent` | 更多 Markdown 答案，大小不限 |
| `AiAnswerDoneEvent` | 回答完成，附带其成本和追问问题 |

## 无障碍

- 本块是一个名为 "AI answer" 的区域；当相关问题或建议提出新问题时，问题标题会获得焦点。
- 推理行和来源行是按钮，报告其是否已展开；收起的内容会移出 Tab 顺序。来源卡片是名为 "Source 1: title, domain" 的链接。
- 引用按阅读顺序获得焦点，获得焦点时打开其卡片，按 Escape 关闭卡片且不移动焦点；点亮的句子只是视觉辅助，卡片承载了相同的信息。
- 一个礼貌式实时区域会播报 "Thinking"、"Writing the answer"、"Answer ready" 或 "Answer stopped"、失败、复制、评分、评分变化和版本变化。流式输出的词不会逐个播报，
  并且在撰写回答时，答案会被标记为忙碌。
- 操作按钮都有名称和工具提示；评分按钮报告是否已按下；版本翻页器是一个名为 "Version 2 of 2" 的组。
- 焦点会回到人们接下来会看的地方：菜单之后回到 "Try again" 按钮，原因列表之后回到点踩按钮，相关问题之后回到新问题。
- 状态从不只依赖颜色：点亮的句子同时有卡片和编号，失败时有图标和说明文字。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 新线程、分享、推理和来源行、引用、操作栏、相关问题、输入框 |
| `Enter` 或 `Space` | 按下获得焦点的按钮或卡片；在引用上，打开其来源 |
| `Escape` | 关闭引用卡片、菜单或原因列表，且不移动焦点；在输入框中，停止回答 |
| 菜单中的 `ArrowDown` / `ArrowUp` | 在各项之间移动；字母键跳到匹配的项 |
| `ArrowRight` / `ArrowLeft` | 打开和关闭 "Switch model" 列表 |
| 输入框中的 `Enter` | 提问；`Shift+Enter` 添加一行 |

## 动效

- 流式文本以整词释放，约每 50 毫秒一批（每一步约四个词），每一步在 160 毫秒内淡入；标题、列表项、表格行和代码行则在整行到达后才出现。
- 推理和来源以弹簧展开，其内容淡入；指向来源标记时，标记会展开。
- 点亮句子时，其背景在 160 毫秒内淡入。
- 切换版本时，回答交叉淡入；高度以弹簧跟随，翻页器的数字会滚动。
- 菜单和原因列表从其按钮处展开。
- 减弱动效会保留文字的分批节奏，但去掉淡入、弹簧和点亮的过渡；关闭动效时，每项变化都立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新文字 | 每 50 毫秒显示整词，160 毫秒淡入 | 相同的分批，无淡入 | 相同的分批，无淡入 |
| 推理和来源 | 高度弹簧，淡入 | 120 毫秒淡入 | 立即 |
| 点亮的句子和卡片 | 160 毫秒 | 立即 | 立即 |
| 切换版本 | 交叉淡入；高度以弹簧变化 | 120 毫秒交叉淡入 | 立即 |
| 菜单 | 从锚点处展开 | 120 毫秒淡入 | 立即 |

## 相关组件

- `AiChat`：用于对话。
- `AiComposer`：用于提示框。
- `Markdown`：仅用于单独渲染回答的文本。
- `ModelPicker`：用于在别处选择模型。

| 组件 | 适用场景 |
|---|---|
| `AiChat` | 人们需要来回对话 |
| `AiComposer` | 你只需要输入框 |
| `Markdown` | 你只需要渲染文本 |
| `ModelPicker` | 模型选择位于设置页 |
