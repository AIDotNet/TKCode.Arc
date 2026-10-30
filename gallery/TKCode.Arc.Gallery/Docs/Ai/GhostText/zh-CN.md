---
title: Ghost text
summary: 行内 AI 自动补全：浅色的幽灵文字在光标处接着把句子写下去，Tab 原地接受，Cmd+Right 接受一个词，Escape 将其关闭，一个安静的标签显示背后的模型。
source-hash: 90d90ae2b1e5
---

<Demo name="Preview" />

## 何时使用

`GhostText` 是一个带 AI 辅助、却不打扰人的文本框。当用户停下来时，浅色的幽灵文字会恰好在光标处接着他们的句子写下去。Tab 让它原地变为正文，Cmd+Right 一次接受一个词，Escape 让它淡出；没有明确的按键，文字永远不会被改动。末尾的小标签显示背后的模型。

- **产品中需要长篇书写的字段**：拉取请求描述、邮件、客服回复、问题正文、发布说明、CRM 备注。
- **简短的单行字段**，可能的补全能省下打字，例如邮件主题或提交信息。
- **任何希望 AI 辅助等到停顿再出现**、且只在按键后才生效的地方。

## 何时不用

- **向助手发送消息的聊天输入框**：使用编写器（composer）。
- **按块接受或拒绝的代码修改**：使用代码建议视图。
- **从已知选项中挑选**：使用组合框或命令菜单。
- **敏感字段**（密码、卡号、健康数据），除非你的模型提供方与数据政策允许。

## 用法

给字段一个 `Suggest` 函数。它收到完整文本（调用时光标总在末尾）和一个取消令牌，返回一个 `GhostTextResponse`：字符串、`Task<string?>`，或用 `GhostTextResponse.FromStream` 包装的分块流。返回 `null` 表示跳过这段文本，且不显示 Thinking。

```csharp
field.Suggest = (text, cancellationToken) =>
    GhostTextResponse.FromStream(StreamCompletion(text, cancellationToken));

async IAsyncEnumerable<string> StreamCompletion(string text,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var response = await http.PostAsJsonAsync("/api/autocomplete", new { text }, cancellationToken);
    response.EnsureSuccessStatusCode();
    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
    var buffer = new char[256];
    int read;
    while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
    {
        yield return new string(buffer, 0, read);
    }
}
```

`Text` 是双向的。去掉首尾空白后的文本达到 `MinLength`（3）个字符，并安静 `Delay`（450ms）之后，字段才会请求。每次按键都会重新计时并取消进行中的请求，所以打字很快的人在停下之前不会触发任何请求。没有 `Suggest` 也没有文本时，字段就是一个普通的文本框，或者播放你传入的模拟。

### 流式建议

<Demo name="Streaming" />

每个流入的分块都追加到幽灵文字末尾，并各自淡入。`Label` 在文本上方加一行安静的标签，`Footer` 是底栏开头的内容。`Rows`（3）是以行计的最小高度，`MaxRows`（10）是字段停止增高、开始滚动的高度。`Model` 给出模型名称与 12px 的标记；用 `AgentModel` 传入你自己的图形和颜色。

用于没有模型的演示或原型时，给字段一个 `GhostTextSimulation`，在 `Script` 中写好各行：它会逐行输入，短暂思考后以 3 到 6 个词的分块流出该行的 `Reply` 并接受，接受方式在 Tab 与逐词之间交替，然后从头开始。点击字段即可把它交给用户，用户会得到剧本中的回复，其他内容则由模拟自己的 `Suggest` 回答（预览传入了一个基于规则的小替身，会补全单词，遇到 "timeout" 时失败）。标签、占位文本、页脚和模型仍设置在字段上。设置 `Suggest` 或 `Text` 后模拟即结束。

```csharp
field.Label = "Commit message";
field.Simulation = new GhostTextSimulation
{
    Script = [new GhostTextSimulationLine("Bump the parser", " to fix the date bug.")],
};
```

### 单行

<Demo name="SingleLine" />

`IsMultiline="False"` 生成单行字段：它随光标横向滚动，标签保持在行尾。输入或粘贴进来的换行会变成空格。

### 精简样式

<Demo name="Plain" />

`Variant="Plain"` 去掉边框、背景和焦点环，让文字平贴在你已有的卡片里。请给那张卡片自己的焦点样式。

### 事件与方法

<Demo name="Events" />

`Accepted` 报告被接受的文字，以及接受的是整条建议还是一个词；`Dismissed` 报告用 Escape 关闭的内容。把两者都记录下来，就能按模型和提示词衡量接受率。`Status`、`Suggestion` 和 `IsStreaming` 随时描述建议的状况，`StatusChanged` 报告每一次变化。

`AcceptAll()`、`AcceptWord()`、`Dismiss()`、`Request()`（立即请求，不等停顿）和 `FocusField()` 可以从你自己的按钮或测试中驱动字段。

### 状态

| 状态 | 标签显示 | 文本 |
|---|---|---|
| `Idle` | 模型的标记与名称 | 只有你的文字 |
| `Loading` | 闪烁的 "Thinking" | 只有你的文字 |
| `Ready` | Tab 键帽与 "to accept"（触摸屏上为 "Tap to accept"） | 你的文字，后接幽灵文字 |
| `Error` | 警示图标、"No suggestion" 和 "Retry" | 只有你的文字 |

按下 Tab 后，标签会短暂显示带对勾的 "Accepted"，然后回到模型名称。

### 编写建议

- 只请求续写：不要开场白，也不要重复输入。
- 文本在句中结束且末尾没有空格时，以空格开头；文本末尾已有空白时，字段会去掉开头的空格。
- 控制在一句话或一短行以内，并使用快速的模型：人们期望停顿后约半秒内看到幽灵文字。
- 对永远不想补全的上下文（代码块内、URL 之后）返回 `null`，这样 Thinking 不会一闪而过。

### 你自己的文字

`Labels` 保存字段显示或朗读的每一个词。替换整个对象即可本地化或改写：

```csharp
field.Labels = new GhostTextLabels
{
    Thinking = "Réflexion",
    ToAccept = "pour accepter",
    SuggestionAnnouncement = suggestion => $"Suggestion : {suggestion}. Tab pour accepter.",
};
```

### 常见问题

**在幽灵文字上继续打字会重新请求吗？** 不会。与幽灵文字开头相同的字符会原地消耗它；其他任何输入都会清除它，并在下一次停顿后请求新的建议。

**为什么点击后幽灵文字消失了？** 建议只从文本末尾续写。把光标移离末尾会清除建议；在末尾再次停顿后它会回来。

## API 参考

<!-- api: GhostText -->

<!-- api: GhostTextResponse -->

<!-- api: GhostTextLabels -->

<!-- api: GhostTextSimulation -->

<!-- api: GhostTextSimulationLine -->

## 无障碍

- 字段是原生文本框，由 `AutomationProperties.Name` 或字符串形式的 `Label` 命名，并由一句话描述 Tab、Command 或 Control 加右方向键以及 Escape 的作用（`IsHintVisible` 会连同标签一起关闭它）。
- 幽灵文字只是文本的绘制方式，从不属于文本本身，因此不会被当作内容朗读。一个礼貌的实时区域会朗读完成的建议（"Suggestion: date the plan changed. Press Tab to accept."），之后是 Suggestion accepted、Suggestion dismissed 或 Couldn't get a suggestion。`Notice` 向同一区域添加一次性的消息。
- 标签是一个按钮，每种状态都有清晰的名称（Accept suggestion、Retry suggestion）。只有在错误状态下 Tab 才能到达它，此时 Retry 就是它的操作；就绪状态下 Tab 本身就是接受。按下它永远不会夺走字段的焦点。
- 状态从不只靠颜色表达：Thinking、带 "to accept" 的 Tab 键帽、带对勾的 Accepted 和带警示图标的 No suggestion 都有文字。
- 键盘焦点会在整个字段周围画出 Arc 的焦点环；指针焦点不会。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 接受整条建议，光标保持在末尾。没有建议时，Tab 照常移动焦点 |
| Cmd+Right、Ctrl+Right | 接受下一个词，其余保留。Option+Right 和 Alt+Right 同样可用。没有建议时，这些键照常移动光标 |
| Escape | 关闭建议。没有建议时不处理 Escape，外层对话框仍可关闭 |
| 打字 | 与幽灵文字开头相同的字符会原地消耗它；其他输入会清除它 |
| 方向键、点击 | 把光标移离末尾会清除建议 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 分块流入 | 在 140ms 内淡入，只改透明度 | 立即 | 立即 |
| Escape、离开字段 | 幽灵文字在 110ms 内淡出 | 立即 | 立即 |
| 输入不同的字符 | 幽灵文字立即消失 | 立即 | 立即 |
| Tab、Cmd+Right | 被接受的文字在 200ms 内原地从幽灵墨色过渡到正文墨色；其余幽灵文字不会再次淡入 | 立即 | 立即 |
| 文本变多或变少 | 高度以 180ms 补间跟随，移动时裁剪，从不回弹 | 立即 | 立即 |
| 标签状态 | 旧内容 100ms 淡出、新内容 160ms 淡入，宽度以锚定右边缘的 200ms 补间跟随 | 短暂交叉淡化 | 立即 |
| 键帽 | 对应按键或方法触发时按下 160ms | 立即 | 立即 |
| Thinking | 安静的闪光 | 静止 | 静止 |

完整动效下模拟会循环播放；减弱或关闭动效时只播放一次就停止。它在屏幕外时会暂停。

宽度小于 520px 时，单词与 Escape 提示会让开，只保留标签，底栏备注以省略号截断。

## 相关组件

- `Textarea` 与 `Input`：普通文本框。
- `MentionInput`：消息中的 @ 提及。
- `TextShimmer`：Thinking 状态的安静闪光。
- `ToolCall` 与 `AgentStatus`：AI 回复的其余部分。
