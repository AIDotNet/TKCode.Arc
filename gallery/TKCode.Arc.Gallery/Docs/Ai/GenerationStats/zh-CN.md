---
title: Generation stats
summary: 流式模型回复的实时统计条：首个 token 时间、带迷你折线的速度、输出 token 与费用逐步累加，结束后定格，并可展开为延迟与 token 费用明细。
source-hash: 599b25d0c851
---

<Demo name="Preview" />

## 何时使用

`GenerationStats` 描述一次模型回复在流式输出时和结束后的情况。头部显示模型的标记和名称，以及由图标和文字组成的状态：Waiting、Streaming、"Done in 4.18s"、"Stopped at 1.90s" 或 Failed。下方是四个单元格：首个 token 时间、带迷你折线的每秒 token 速度、输出 token 和费用。回复流式输出时，每个数字都以等宽数字逐步累加；结束后定格在精确值上。Details 会展开明细，其中有一条延迟条和一张带单价与费用的 token 表。

- **聊天产品中助手消息的下方**，向用户展示一次回复花了多少钱、有多快。
- **Playground、评测运行器和内部开发面板**，在延迟、吞吐量和费用上比较模型。
- **管理或调试视图**，检查单个请求的 token 用量和缓存命中率。

## 何时不用

- **一段对话用了多少上下文**：使用 `ContextWindow`。
- **多个请求或多天的花费**：使用图表或表格；这个统计条只描述一次回复。
- **有多个步骤、长时间运行的智能体**：使用 `AgentStatus`；这里度量的是一次模型调用。

## 用法

用你已有的流式处理程序为统计条提供数据。请求发出时设置 `StartedAt`，并把 `Status` 设为 `Waiting`；收到第一个数据块时设置 `Usage.Ttft`，并把 `Status` 设为 `Streaming`；每个数据块都传入一个带有不断增长的 `OutputTokens` 的新 `Usage`；最后一块时设置 `Usage.Duration`，并把 `Status` 设为 `Done`。速度、延迟和费用会随之得出。既不设置 `Status` 也不设置 `Simulation` 时，统计条处于空闲状态：头部显示 Idle，每个单元格显示短横线。

```csharp
// When the request leaves. The provider is your own key for it, written as given into the copied JSON.
stats.Model = new GenerationModel(name: "Claude Sonnet 5.5", provider: "anthropic", id: "claude-sonnet-5-5");
stats.Pricing = new GenerationPricing(input: rates.Input, output: rates.Output, cachedInput: rates.CachedInput);
stats.StartedAt = DateTimeOffset.Now;
stats.Usage = new GenerationUsage { InputTokens = input + cacheRead, CachedTokens = cacheRead };
stats.Status = GenerationStatus.Waiting;

// On every chunk (the first one also sets Ttft and Status = Streaming).
stats.Usage = stats.Usage with { OutputTokens = outputSoFar };

// On the last one.
stats.Usage = stats.Usage with { Duration = DateTimeOffset.Now - started };
stats.Status = GenerationStatus.Done;
```

<Demo name="Streaming" />

### 结构

1. **头部**：模型的标记（字母徽标，或你在 `GenerationModel.Mark` 中提供的图形）、名称（过长时截断而不换行）、状态图标与文字，以及 Details。
2. **单元格**：First token、Speed（带迷你折线）、Output 和 Cost，以细线分隔。
3. **错误行**：请求失败时，显示 `ErrorText` 中的原因和 Retry。
4. **明细**：延迟条（首个 token、生成、总计）、token 表（输入、缓存输入、输出、推理、总计）及其单价与费用、一条备注，以及把统计复制为 JSON 的按钮。

### 状态

| 状态 | 头部显示 | 单元格 |
|---|---|---|
| `Idle`，或既无 `Status` 也无 `Simulation` | Idle | 短横线 |
| `Waiting` | 旋转图标与 "Waiting" | First token 从 `StartedAt` 起实时计时 |
| `Streaming` | 强调色旋转图标与 "Streaming" | 输出、速度和费用逐步累加；迷你折线逐步绘出 |
| `Done` | 自行绘出的对勾与 "Done in 4.18s" | 精确值 |
| `Stopped` | 方块与 "Stopped at 1.90s" | 已到达的部分 |
| `Error` | 警示图标与 "Failed" | 已到达的部分；错误行展开 |

### 速度与费用

速度在数据块到达时测量（自首个 token 以来的 token 数除以自那时起经过的时间），因此在两个数据块之间保持不动；回复结束后，速度为输出 token 除以生成时间。输出增长期间，迷你折线每 160ms 加入一个平滑后的读数，点数达到 48 时减半，因此始终呈现整个运行过程。如果你的服务会报告这些值，请传入 `Usage.TokensPerSecond` 或 `SpeedSamples`。

费用由 `Pricing`（每百万 token 的美元价格）算出：未缓存的输入按输入单价，缓存输入按缓存单价（未提供时按输入单价），输出和推理按输出单价。如果服务商会报告费用，请传入 `Usage.CostUsd` 并省略 `Pricing`；此时 Rate 列会隐藏。

服务商的用量字段这样对应：Anthropic 的 `input_tokens` 加上 `cache_read_input_tokens` 是 `InputTokens`，`cache_read_input_tokens` 是 `CachedTokens`；OpenAI 的 `prompt_tokens` 和 `prompt_tokens_details.cached_tokens` 按同样方式对应，`completion_tokens_details.reasoning_tokens` 是 `ReasoningTokens`。`OutputTokens` 统计的是不含推理的输出。

### 明细

<Demo name="Breakdown" />

Details 用于展开明细；`IsExpanded="True"` 让它在首次渲染时即展开。用户按下 Details 时会引发 `DetailsToggled`。`Note` 会在明细下方放一条脚注，例如单价的来源。

### 行内

<Demo name="Inline" />

`Variant="Inline"` 是一行安静的文字，用于聊天消息下方的空间（"412ms to first token · 88 tok/s · 284 tokens · $0.0049"）。在手机上它会换到第二行而不是滚动，明细则作为其下方的面板展开。

### 失败与停止

用户停止回复时，设置 `Usage.Duration`，并把 `Status` 设为 `Stopped`。请求失败时，把 `ErrorText` 设为简短而具体的原因，并把 `Status` 设为 `Error`：错误行会带着 Retry 展开，按下 Retry 会引发 `RetryRequested`。`AllowsRetry="False"` 会隐藏它。

### 模拟运行

不设置 `Status`，并给统计条一个 `GenerationStatsSimulation`，即可播放你自己的运行，适用于落地页、原型或演示：每个 `GenerationStatsRun` 指定模型及其价格、最终的 token 数、首个 token 时间和峰值速度。统计条会等待、以带爬升和轻微波动的数据块流式输出、定格，然后进入下一个；带有 `FailAtTokens` 的运行会在该处以其 `ErrorText` 失败一次，重试后成功。统计条位于屏幕上时这些运行循环播放，明细展开时停留，减弱动效下立即定格。上面的预览播放四个真实模型按其标价计费的运行；token 数和耗时是虚构的。

```csharp
stats.Simulation = new GenerationStatsSimulation { Runs = MyRuns.All };
stats.Note = "Simulated runs at list prices.";

public static class MyRuns
{
    public static IReadOnlyList<GenerationStatsRun> All { get; } =
    [
        new()
        {
            Model = new GenerationModel("Claude Sonnet 5.5", "anthropic", "claude-sonnet-5-5"),
            Pricing = MyPrices.ClaudeSonnet, // your price list, US dollars per million tokens
            InputTokens = 3_682,
            CachedTokens = 2_816,
            OutputTokens = 284,
            TimeToFirstToken = TimeSpan.FromMilliseconds(412),
            PeakTokensPerSecond = 96,
        },
    ];
}
```

在 XAML 中，把 `<arc:GenerationStats.Simulation>` 设为一个 `GenerationStatsSimulation`，其 `Runs` 来自 `{x:Static}`，如预览的 Code 标签所示。每个统计条使用自己的模拟（它保存播放状态）；运行数据可以共享。设置 `Status` 后统计条改为显示你的数据，把它设回 null 会让模拟从第一个运行重新开始。

### 响应式

统计条宽度达到 600px 时，四个单元格排成一行；低于 600px 时排成二乘二的网格，让速度单元格保留放迷你折线的空间。低于 440px 时，Details 变为图标按钮（名称仍为 "Show details" / "Hide details"），表格的 Rate 列隐藏。统计条始终填满其容器；数字在预留的等宽单元格中累加，因此旁边的内容不会移动。

### 自定义文案

`Labels` 包含统计条显示或朗读的所有文字。替换整个对象即可本地化或改写：

```csharp
stats.Labels = new GenerationStatsLabels
{
    DoneIn = time => $"Terminé en {time}",
    FirstToken = "Premier jeton",
    Details = "Détails",
};
```

### 最佳实践

- 模型名称使用人们熟知的写法（"Claude Sonnet 5.5"），把 API id 放进 `GenerationModel.Id`。
- 在聊天消息下方使用行内变体，在评测视图、Playground 和管理面板中使用卡片。
- 每个数据块传入一个新的 `Usage`，而不是重建统计条；数字会从当前位置平滑过渡。

## API 参考

<!-- api: GenerationStats -->

<!-- api: GenerationModel -->

<!-- api: GenerationUsage -->

<!-- api: GenerationPricing -->

<!-- api: GenerationStatsSimulation -->

<!-- api: GenerationStatsRun -->

<!-- api: GenerationStatsLabels -->

## 无障碍

- 统计条是一个以模型命名的组（"Claude Sonnet 5.5 stats"）。每个单元格以其标签和值命名，因此读作 "First token, 412ms"。
- Details 是一个会报告展开或折叠状态的按钮；在较窄的宽度下变为图标按钮，但保留名称 "Show details" 或 "Hide details"。
- 一个礼貌型状态区域会播报定格的结果（"Claude Sonnet 5.5 done in 4.18s, 284 output tokens at 88 tokens per second, $0.0049"）、失败和停止，从不播报实时计数。正在播放的模拟只说一次其运行是模拟的，在有人与统计条交互之前保持安静。
- 状态从不只依赖颜色：旋转图标、对勾、警示图标或方块旁边总有文字。延迟条是装饰性的；它的数值以文字列在旁边。
- token 表的每一行都带着列标题命名（"Input: Tokens 866, Rate $2.00 per million tokens, Cost $0.0017"）。
- 指针聚焦不显示焦点环；键盘聚焦显示 Arc 的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 移到 Details，回复失败时再移到 Retry，然后移到已展开明细中的复制按钮 |
| Enter、Space | 展开或折叠明细、重试，或把统计复制为 JSON |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 数字变化 | 以等宽数字在约 100ms 内平滑过渡到新值；数值下降时直接跳到新值 | 立即 | 立即 |
| 状态变化 | 旧文字和图标在 100ms 内淡出，新的在 160ms 内淡入并上升 2px | 短暂交叉淡化 | 立即 |
| 完成 | 对勾自行绘出一次 | 完整绘出 | 完整绘出 |
| 等待、流式输出 | 旋转图标每 800ms 转一圈 | 静止 | 静止 |
| 明细或错误行 | 高度以 0.26s 临界阻尼弹簧变化，内容在 160ms 内淡入淡出；没有回弹 | 立即 | 立即 |
| Details 箭头 | 以同一弹簧旋转 | 立即 | 立即 |
| 模拟运行 | 在屏幕上时循环播放；明细展开时停留 | 停在一个已定格的运行上 | 停在一个已定格的运行上 |

实时时钟只在等待或流式输出时运行，模拟只在统计条位于屏幕上时以虚拟时间推进。

## 相关组件

- `ContextWindow`：一段对话用了模型多少上下文。
- `AgentStatus`：长时间运行的智能体的实时状态。
- `ToolCall`：智能体发起的工具调用。
