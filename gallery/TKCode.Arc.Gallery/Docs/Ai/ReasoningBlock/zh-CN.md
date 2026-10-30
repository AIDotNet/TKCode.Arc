---
title: Reasoning block
summary: AI 回复中可折叠的思考块：一张卡片，模型思考时标签逐秒计时，最新的推理在其下方流式出现，完成后以轨道上的步骤呈现完整推理。
source-hash: 3c55fed32f02
---

<Demo name="Preview" />

## 何时使用

`ReasoningBlock` 是 AI 回复里的"思考"部分。模型思考时，一个闪烁的 "Thinking" 标签逐秒计时，其推理的最新几个词在下方流式出现，最多两行。完成后，卡片折叠为带箭头的 "Thought for 7s"，点击一次即可把完整推理展开为带标题的步骤，并在底部说明结果如何，还有一个复制按钮。答案始终是焦点，推理只需一次点击。

- **会推理的模型所生成的聊天回复**，位于答案上方。
- **智能体日志**：每一轮都以模型想明白了什么开头。
- **恢复历史消息**：推理保持折叠，并保留原来的思考时间。
- **审计与复核视图**：人们需要阅读结果是如何得出的。

## 何时不用

- **智能体调用的工具**：使用 `ToolCall`；推理块容纳的是文字，而不是带输入与输出的动作。
- **智能体的计划**：使用 `AgentPlan`；计划是人可以编辑的清单，推理是一份记录。
- **没有推理可显示的单行忙碌指示**。
- **模型不应透露的内容**：只传入你希望人们阅读的推理。

## 用法

设置 `Status`，并把推理作为一个不断增长的字符串传给 `Reasoning`：空行分隔段落，写成 `**Heading**` 或 `## Heading` 的行会开始一个新的步骤。仍在流式输出中的标题会先被隐藏，所以不会出现裸露的星号。没有数据时，该块显示 "Thought process"，也没有可展开的内容。

```csharp
// While streaming: set the growing text, then flip the status when the answer starts.
block.Reasoning = thinkingText;
block.Status = done ? ReasoningStatus.Done : ReasoningStatus.Thinking;

// A finished message from history.
block.Reasoning = message.Reasoning;
block.Elapsed = TimeSpan.FromSeconds(message.ThinkingSeconds);
```

或者自行提供步骤，并在模型流式输出时向其 `Text` 追加内容：

```xml
<arc:ReasoningBlock Elapsed="0:0:07">
    <arc:ReasoningStep Heading="Check what is open"
                       Text="Two reconciliations are unmatched and one export has not finished." />
</arc:ReasoningBlock>
```

思考期间由该块自行计秒，结束后保留结果。如果你的服务会报告耗时，请自行设置 `Elapsed`。没有时间的已完成块显示 "Thought process"。

### 结构

1. **头部**：整个顶部一行就是一个按钮：标记（18px，强调色）、标签及其时间、需要时出现的停止或重试按钮，以及最右端的箭头。
2. **预览**：思考中且处于收起状态时，显示最新步骤的最新几个词，最多两行，颜色偏淡。
3. **步骤**：展开时，每个步骤是标题加正文，旁边是带空心圆点的轨道。
4. **底部**：一个对勾加 "Done"，或 "Stopped"，以及复制推理的按钮。

### 状态

上方的标签页展示了实时、已完成、已中断和没有数据的块；各状态显示如下：

| 状态 | 头部显示 | 展开部分 |
|---|---|---|
| `Thinking` | 闪烁的 "Thinking for 3s"，可带停止按钮 | 目前为止的步骤，流式出现 |
| `Done` | "Thought for 7s" | 步骤，然后是 "Done" 和复制 |
| `Stopped` | "Stopped after 4s" | 已流出的内容，然后是 "Stopped" 和复制 |
| `Error` | 警示图标和 "Thinking interrupted"，带重试 | 步骤，然后是 `ErrorText` 中的原因 |

### 停止

<Demo name="Stop" />

`AllowsStop` 会在模型思考时显示停止按钮。它引发 `StopRequested`：终止你的请求，然后把 `Status` 设为 `Stopped`。失败的块会显示重试并引发 `RetryRequested`。示例播放的是一段模拟，停止按钮会自行结束它。

### 精简样式

<Demo name="Plain" />

`Variant="Plain"` 去掉边框和左右内边距，使该块贴合地放在消息气泡中。

### 模拟模型

用于没有模型的演示或原型时，给该块一个 `ReasoningBlockSimulation`，并传入要播放的推理。该块会进入思考、逐秒计时，并以每 45ms 三个词的速度流式输出文字，然后完成；只有在它没有自己的推理、步骤、状态或时间时才会播放，每次接手模拟时都会从头开始，离开屏幕时暂停。停止按钮会把它结束为 Stopped，`Restart()` 会重新播放，`Finished` 会在播放完成时通知你。你自己的数据始终优先：设置 `Reasoning` 后，模拟就会让位。

```xml
<arc:ReasoningBlock AllowsStop="True">
    <arc:ReasoningBlock.Simulation>
        <arc:ReasoningBlockSimulation Reasoning="The export job is still running, so the close waits for it." />
    </arc:ReasoningBlock.Simulation>
</arc:ReasoningBlock>
```

```csharp
// Or from code, with a reasoning you recorded.
block.Simulation = new ReasoningBlockSimulation { Reasoning = recordedReasoning };
```

### 你自己的文字

`Labels` 包含该块显示或朗读的每一个词。替换该对象即可本地化或改写：

```csharp
block.Labels = new ReasoningBlockLabels
{
    Thinking = "Reflexion",
    Thought = "Réfléchi",
    Duration = seconds => $"pendant {seconds}s",
};
```

`Mark` 可以用你自己的 24 × 24 描边图标替换星芒，`MarkBrush` 设置其颜色。

### 减弱动效

减弱动效时，标签不再闪烁，箭头与面板立即变化，文字以短暂淡入淡出交替。关闭动效时一切立即变化。秒数仍会计时：它是信息，不是装饰。

### 内容准则

- 标题只有几个词，说明模型做了什么（"Check what is open"），而不是它的感受。
- 一个步骤是一小段文字。把长推理拆成多个步骤，不要写成一大块。
- 时间写作 "for 7s" 或 "for 1m 5s"；已停止的块写作 "after 4s"。

### 最佳实践

- 把该块放在答案上方，答案出现后保持收起。
- 通过设置不断增长的字符串，或向 `ReasoningStep.Text` 追加来流式输出；不要在每个 token 上重建列表。
- 失败时传入 `ErrorText`，让人在重试旁边看到停止的原因。

### 常见问题

**思考时人可以展开它吗？** 可以。预览会收起，步骤在原地流式出现。

**如何显示固定的时间？** 设置 `Elapsed`；该块随后显示该时间且不再计时。

## API 参考

<!-- api: ReasoningBlock -->

<!-- api: ReasoningStep -->

<!-- api: ReasoningBlockSimulation -->

<!-- api: ReasoningBlockLabels -->

## 无障碍

- 头部是一个按钮，会报告其展开或收起；它的名称先读出标签与时间，再读出按下后的结果（"Thought for 7s, Show reasoning"）。
- 已停止或失败的块不只靠颜色表达：头部有警示图标和 "Thinking interrupted"。
- 键盘焦点在头部内显示 2px 的焦点环；指针焦点不绘制。
- 每个文字都位于 `ReasoningBlockLabels` 中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 移动到头部，然后是出现的停止或重试，再到展开块内的复制按钮 |
| Enter, Space | 在头部展开或收起步骤；在按钮上则按下该按钮 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 思考中的标签 | 闪烁，每 1.8 秒一次 | 静止 | 静止 |
| 标签变为 "Thought" | 旧文字 100ms 淡出，新文字 160ms 淡入并上升 2px | 淡入淡出，130ms | 立即 |
| 预览出现或离开 | 高度弹簧变化（0.26 秒，无回弹），文字淡入 160ms | 立即 | 立即 |
| 预览中某一行换行 | 文字以 0.24 秒的弹簧向上滑动，而不是跳动 | 立即 | 立即 |
| 步骤展开或收起 | 同一根 0.26 秒的高度弹簧，内容淡入 160ms | 立即 | 立即 |
| 展开时来了新步骤 | 追加并单独淡入；其他内容不动 | 立即 | 立即 |
| 箭头 | 以弹簧转动 | 立即 | 立即 |

头部在每种状态下都是 44px 高，预览始终是两行高，所以出现停止或重试按钮、或一行文字流入，都不会改变块的高度。

## 相关组件

- `ToolCall`：智能体发起的工具调用。
- `AgentPlan`：智能体在行动前写下的计划。
- `AgentStatus`：整次运行的实时状态。
