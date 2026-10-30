---
title: Response feedback
summary: 面向 AI 回复的反馈控件：点踩会在行内展开一个带原因标签和备注的面板，点赞会询问哪里做得好，提交后短暂显示感谢，随后收回到操作行中。
source-hash: 7d49fffaded3
---

<Demo name="Preview" />

## 何时使用

`ResponseFeedback` 是一整张 AI 回复卡片：气泡中的用户消息、模型的标志与名称、回复内容，以及包含复制、可选的重新生成和点赞点踩的操作行。`FeedbackActions` 是同一控件的单独版本，用于你已经自己渲染的消息行。点踩会在行内展开一个面板，其中有原因标签和可选的备注；点赞会填充图标并询问哪里做得好；提交后显示简短的感谢，然后面板收回到操作行中。

- **对 AI 回复点赞或点踩**：聊天、Copilot 或搜索答案中。
- **结构化的原因**（不准确、不安全），用于模型评估或客服分流。
- **消息操作行**：在反馈旁边提供复制和重新生成。

## 何时不用

- **对产品或会话的星级或数字评分**：使用评分控件。
- **对帖子的表情回应**：使用 `Reactions`。
- **与单条回复无关的一般产品反馈**：使用反馈表单。
- **包含许多问题的长问卷**：使用表单或对话框。

## 用法

没有数据时，`ResponseFeedback` 只显示框架：模型标识、空的回复和操作行；在你设置 `FeedbackHandler` 之前，反馈为模拟发送。传入你的回复、模型名称、它所回答的问题和要复制的文本，并传入 `MessageId`，以便每次提交都能与该消息关联存储：

```xml
<arc:ResponseFeedback ModelName="Claude Sonnet 5.5"
                      Prompt="How do I rotate an API key?"
                      CopyText="Create a new key, deploy it, then revoke the old one."
                      MessageId="msg_0192"
                      AllowsRegenerate="True">
    <TextBlock TextWrapping="Wrap" Text="Create a new key, deploy it, then revoke the old one." />
</arc:ResponseFeedback>
```

`Prompt`、`ModelName` 和 `CopyText` 默认为 null：在你设置之前，问题、名称和复制都保持隐藏。`ModelMark` 接受一个 24 × 24 的描边图形，以 16px 绘制（默认是一个中性的闪光图标）。

### 你自己的消息行

<Demo name="OwnRow" />

`FeedbackActions` 就是单独的操作行。`Leading` 放在点赞点踩之前的操作（在 36px 的小按钮中使用 16px 图标，例如仅图标的 `CopyButton`），`Trailing` 固定在行尾，用于显示时间或令牌数。

### 结构

1. **点赞与点踩**：两个 36px 的圆形切换按钮。激活的那个会在轮廓之上填充，因此没有任何东西移动。
2. **面板**：点踩后显示 "What went wrong?" 和关闭按钮、原因标签、备注，以及底部的隐私说明与 Cancel、Submit 按钮。点赞后显示更轻量的 "What was good?" 行，带有 Skip 和 Send。
3. **感谢**：一个只绘制一次的对勾、"Thanks for your feedback" 和第二行文字；停留 `ThanksDuration`（1.6 秒）后面板收起。
4. **错误行**：操作按钮旁的图标与简短消息；Submit 变为 Try again。评分保存失败时，操作行中会显示同样的错误行，并带有它自己的 Try again。

### 保存反馈

<Demo name="Persisting" />

设置 `FeedbackHandler`。每次按下点赞或点踩时，它都会收到一个 `Rating` 提交；提交原因或备注时，会收到一个 `Details` 提交。抛出异常即可显示错误和 Try again；面板关闭或评分改变时令牌会被取消，所以请把它传给你的请求：

```csharp
reply.FeedbackHandler = async (feedback, cancellation) =>
{
    using var response = await http.PostAsJsonAsync("/api/feedback", new
    {
        feedback.MessageId,
        stage = feedback.Stage.ToString(),
        rating = feedback.Rating.ToString(),
        feedback.Reasons,
        feedback.Comment,
    }, cancellation);
    response.EnsureSuccessStatusCode();
};
```

在 `Rating` 阶段存储评分（`None` 表示用户清除了评分），在 `Details` 阶段附加原因和备注。没有处理程序时发送是模拟的：感谢行和播报都会说明没有发送任何内容。无论哪种情况，每次提交都会引发 `FeedbackSubmitted`，每次按下点赞或点踩都会引发 `RatingChanged`。

`Rating` 是双向的：设置它可以显示从数据库读取的评分，绑定它则可以自己掌管状态。从外部设置的评分只会勾选对应按钮，不会打开面板，也不会发送任何内容。

### 原因与备注

<Demo name="Reasons" />

`NegativeReasons` 和 `PositiveReasons` 是原因标签。`PositiveReasons` 为空时，点赞后不显示后续行。标为 Other 的标签会把焦点移到备注并更换其占位文字。`AllowComment="False"` 去掉备注，`CommentPlaceholder` 与 `CommentMaxLength`（1000）决定它的样子，`PrivacyNote`（null 时隐藏）是 Submit 旁边那行低调的说明。

### 回复仍在流式输出时

<Demo name="Streaming" />

回复仍在流式输出时设置 `IsRatingEnabled="False"`，让人们评价的是完成后的答案。

### 你自己的措辞

`Labels` 包含控件显示或播报的每一个词。替换整个对象即可本地化或改写：

```csharp
reply.Labels = new ResponseFeedbackLabels
{
    GoodResponse = "Bonne réponse",
    BadResponse = "Mauvaise réponse",
    NegativeTitle = "Qu'est-ce qui n'allait pas ?",
    Submit = "Envoyer",
};
```

### 窄容器

宽度低于 480px 时卡片内边距为 16px，以上为 24px；原因标签会换行，因此在 360px 下也不需要横向滚动。宽度低于 400px 时，隐私说明和 Cancel 会让出位置（Close 代替 Cancel），因此错误行和 Try again 总能放在同一行。触摸操作之后，原因标签的高度从 28px 增加到 32px。

### 内容准则

- 原因保持一两个词（"Too long"、"Unsafe"），这样在 360px 下标签只占一两行。
- 在隐私说明中用几个词说明会分享什么。
- 感谢行感谢用户并说明接下来会发生什么，不再索取更多内容。

### 最佳实践

- 单条助手回复及其操作使用 `ResponseFeedback`；自己渲染消息时使用 `FeedbackActions`。
- 始终传入 `MessageId`，并持久化两个阶段。
- 在处理程序中抛出异常以显示错误；控件会处理 Try again，并通过令牌中止过期的调用。

### 常见问题

**用户可以改变主意吗？** 可以。按下另一个按钮会切换评分和面板；再次按下当前激活的按钮会清除评分。每次按下都会发送。

**Escape 有什么作用？** 它会关闭打开的面板，并把焦点还给打开它的那个按钮。评分保持不变。

## API 参考

<!-- api: ResponseFeedback -->

<!-- api: FeedbackActions -->

<!-- api: FeedbackSubmission -->

<!-- api: ResponseFeedbackLabels -->

<!-- api: FeedbackSubmittedEventArgs -->

<!-- api: FeedbackRatingChangedEventArgs -->

## 无障碍

- 点赞和点踩是会报告按下状态的切换按钮，名称为 "Good response" 和 "Bad response"，工具提示相同。
- 原因标签是切换按钮，位于以面板标题命名的分组中。选中的标签会反转为实心填充，因此状态从不依赖色相。
- 用键盘按下点踩会把焦点移到第一个原因；用指针按下则保持焦点不变。用键盘提交后，焦点落在感谢行上，面板收起时回到按钮。
- 一个礼貌（polite）的实时区域播报评分变化、发送中、感谢状态和错误，并在发送为模拟时说明没有发送任何内容。
- 错误显示在操作按钮旁，带有图标和文字，从不只靠颜色。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过复制、重新生成、点赞和点踩，然后进入打开的面板 |
| Enter、Space | 按下点赞或点踩、切换原因标签，或激活 Submit、Send、Skip 和 Cancel。再次按下当前激活的按钮会清除评分 |
| Escape | 关闭打开的面板，并把焦点还给打开它的按钮。评分保持不变 |
| Cmd + Enter、Ctrl + Enter | 在备注框中提交 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 面板打开或关闭 | 测得的高度以弹簧变化（0.24 秒，无回弹），同时面板在 160ms 内淡入淡出 | 高度立即变化，面板在 160ms 内淡入淡出 | 立即 |
| 表单切换为感谢 | 一次交叉淡化：旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px，同时高度以弹簧过渡到新内容 | 120ms 交叉淡化，高度立即变化 | 立即 |
| 按钮填充 | 实心图形在轮廓之上 160ms 淡入；没有任何东西移动或改变尺寸 | 同样的淡入 | 立即 |
| 按下 | 立即缩放到 0.94，松开时以弹簧恢复 | 不缩放 | 不缩放 |
| 成功对勾 | 在 280ms 内绘制一次 | 立即显示 | 立即显示 |

按钮的尺寸从不改变，操作行随容器拉伸，因此按钮填充或面板打开时行宽不会变化。所有结束状态和播报在每个动效等级下都相同。

## 相关组件

- `ReasoningBlock`：回复上方模型的思考过程。
- `ToolCall`：智能体调用的工具。
- `CopyButton`：在你自己的消息行中提供复制操作。
