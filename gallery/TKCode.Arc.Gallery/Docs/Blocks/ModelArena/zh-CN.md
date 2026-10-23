---
title: 模型竞技场
summary: 对两个模型进行盲测：一个提示发送给两个匿名模型，两份回答以相同的节奏流式呈现，投票之后才揭示模型身份，每份回答附带首字时间、速度和单次费用，并提供战绩榜。
source-hash: a76b69badd77
---

<Demo name="Preview" />

## 何时使用

模型竞技场用人自己的判断回答"这两个模型哪个更适合我们？"，而不是依赖基准测试。两份回答并排、以相同的节奏出现，因此速度不会左右投票；在投票结束之前，模型始终保持隐藏。

- **选择提供方或模型版本**：让团队跑一遍产品真正会问的问题，然后投票。
- **审查提示或模型的变更**：同一个提示，两个候选，一次投票，以及旁边每份回答的费用。
- **并排展示延迟与价格**：每份回答都报告其首字时间、速度和费用，数据取自真实的流。
- **共享的战绩榜**：人们投票时，胜、平、负逐步累积；通过 `Standings` 保存和恢复它们。

## 何时不用

- **与单个助手的对话**：使用 `AiChat`。
- **自动为数百个提示打分**：竞技场一次只进行一轮，由人来评判。
- **选择默认模型**：使用 `ModelPicker`。
- **比较同一提示的两个版本**：使用 `PromptStudio`，它以词级差异进行比较。

## 用法

`ModelArena` 派生自 Avalonia 的 `TemplatedControl`，由数据驱动：提供 `Contestants`、示例 `Prompts`，以及一个能流式返回某个模型回答的 `Runner`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelArena x:Name="Arena" MaxWidth="800" />
</UserControl>
```

```csharp
arena.Contestants.AddRange(models);
arena.Prompts.AddRange(samplePrompts);
arena.Runner = new MyArenaRunner();

public sealed class MyArenaRunner : IArenaRunner
{
    public async IAsyncEnumerable<ArenaStreamEvent> RunAsync(
        AiModel model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var piece in provider.StreamAsync(model.Id, prompt, cancellationToken))
        {
            yield return new ArenaStreamEvent(piece.Text, piece.InputTokens, piece.OutputTokens);
        }
    }
}
```

### 一轮

运行（在输入框中按 Enter，或 Ctrl+Enter、Mac 上为 Cmd+Enter）会把提示发送给从 `Contestants` 中选出的一对模型。每份回答在第一段文字到达前显示 "Thinking"（思考中），到达后显示 "Writing"（撰写中）；先完成的那一份显示 "Finished first"（先完成）。两者都完成后，投票行打开：提供 "A is better"（A 更好）、"Tie"（平局）、"B is better"（B 更好）或 "Both bad"（都不好）。投票会揭示模型身份、计入战绩榜，并提供 "Next round"（下一轮）：它选出新的一对模型，切换到下一个示例提示并运行。

- **两者节奏相同**：真实的流速可能相差很大，但文字在同一个共享时钟上以整词为单位显示，因此所见内容不取决于哪个模型更快。速度和费用数字则取自原始流。
- **盲测**：投票之前，卡片、其提示框及其无障碍名称中都不会出现任何模型名称或标识。关闭 "Blind"（盲测）开关即可立即看到模型；投票依然计入。
- **停止**：一轮进行时，运行按钮（Run）会变为 Stop，在提示行中按 Escape 也会停止。尚未完成的窗格显示 "Stopped"（已停止）；已完成的则保留其回答。
- **失败**：流失败时，该窗格保留已收到的内容，并显示带有 "Retry model A"（重试模型 A）的错误卡片。重试只运行该模型。若某个错误重试无法修复，抛出 `isRetryable: false` 的 `ArenaRunException`；此时卡片改为提供一个示例提示。

<Demo name="Programmatic" />

### 指标

每个窗格显示首字时间（等待期间计时递增）、以每秒词元数计的速度，以及费用。如果运行器在 `ArenaStreamEvent` 中报告了词元数，就使用它；否则按每个词元四个字符估算。费用为词元数乘以该模型每百万词元的 `InputPrice` 和 `OutputPrice`；没有价格的模型费用为零。

### 战绩榜

奖杯按钮打开战绩榜：列出有战绩的模型，按胜率排序（平局计为半胜），显示前 `LeaderboardSize` 名。投票之后，按钮上会出现一个圆点；之后首次打开时，各行会从旧位置移动到新位置。`Standings` 是以模型 id 为键、值为 `ArenaRecord` 的字典：在第一次投票之前填入它，即可从已保存的结果开始；在 `VoteCast` 时将其保存。

### 窄容器

宽度小于 560 时，两份回答变为两个页签（各自显示名称和状态）；宽度小于 480 时，奖杯按钮只保留图标，键盘提示隐藏，投票按钮组成两行两列的网格。宽度指的是卡片自身的宽度，而非窗口的宽度。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`ModelArenaLabels`）：要翻译或改写文字，替换该实例即可；带格式的文字为函数。

### 内容指南

- **示例提示**：一个简短的芯片标签，加上一个能让模型产生差异的完整提示。
- **模型名称**：名称，以及用于页签的简称。
- **价格**：说明其为示例。

### 最佳实践

- 在运行器中传递取消令牌，这样 Stop 和新一轮才能真正停止流。
- 服务商提供词元数时就报告它；估算只是后备。
- 配对要随机但不重复：默认选择器会避开上一对，或设置 `PairPicker`。
- 在 `VoteCast` 触发时保存 `Standings`。

### 常见问题

**为什么两份回答以相同的速度出现？** 文字由一个共享时钟统一释放；每份回答下方显示的速度是真实的。

**为什么投票之前卡片上不显示任何关于模型的信息？** 这就是盲测；关闭盲测即可看到模型。

**能不用运行器运行吗？** 不能：没有 `Runner` 时，运行按钮处于禁用状态。

## API 参考

<!-- api: ModelArena -->

<!-- api: ArenaPrompt -->

<!-- api: ArenaPaneState -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `VoteCast` | 人们投票 | `Vote` |
| `RoundStarted` | 一轮开始 | `Round`, `Prompt`, `ModelA`, `ModelB` |
| `RunStopped` | 两份回答完成之前运行被停止 | `Round`, `Prompt`, `ModelA`, `ModelB` |
| `PaneFailed` | 一份回答失败 | `Slot`, `Model`, `Error` |
| `LeaderboardReset` | 战绩榜被重置 | |

### 流式输出

| 类型 | 说明 |
|---|---|
| `IArenaRunner` | `RunAsync(model, prompt, cancellationToken)` 以 `IAsyncEnumerable<ArenaStreamEvent>` 返回一个模型的回答 |
| `ArenaStreamEvent` | 一些文字、输入词元数、输出词元数，或三者兼有；字符串会转换为它 |
| `ArenaRunException` | 运行器能够说明的失败：消息、可选标题，以及是否可以重试 |
| `IArenaPairPicker` | 选出一轮中的两个模型 |
| `ArenaVote` | 轮次、提示、两个模型、选择、获胜者，以及两份回答的指标 |
| `ArenaMetrics` | 首字时间、每秒词元数、总耗时、词元数、费用，以及词元数是否为估算 |
| `ArenaRecord` | 胜、平、负；`WinRate` 将平局计为半胜 |

## 无障碍

- 每份回答是一个区域，名称为 "Model A"（模型 A）或 "Model B"（模型 B）（投票之后附上模型名称），其回答区域是可聚焦、有名称、可滚动的区域，其指标是一个有名称的分组。
- 投票之前，无障碍树中的任何地方都不存在模型名称。
- 状态由文字和图标表示，而不只是颜色。页签是一个页签列表，选中项跟随焦点。
- 一个礼貌式实时区域会播报：一轮已开始、哪份回答先完成、投票已开放、结果（附两个模型的名称）、战绩榜已更新、运行已停止、失败以及重置。流式输出的文字不会逐词播报。
- 当有尚未查看的结果时，奖杯按钮会说 "Standings, updated"（战绩榜，已更新）。
- 减少动效时，所有状态和播报都保留。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` 在提示框中 | 运行输入框中的文字 |
| `Ctrl+Enter` / `Cmd+Enter` | 运行输入框中的文字，在 Run 或芯片上同样有效 |
| `Escape` 在提示行中 | 停止运行 |
| `Tab` | 依次为奖杯、Blind（盲测）、输入框、Run、芯片、每份回答、投票按钮 |
| `Enter` / `Space` 在投票按钮上 | 投票；焦点移到 "Next round"（下一轮） |
| `ArrowLeft` / `ArrowRight` 在页签上 | 移动并选中，循环切换；`Home` 和 `End` 跳到两端 |
| `ArrowUp`、`PageUp`、`Home` 在回答中 | 向上滚动，并停止跟随流 |
| `Escape` 在战绩榜中 | 关闭战绩榜；焦点回到奖杯按钮 |

## 动效

- 状态词和名称以短暂的交叉淡化切换，槽位的宽度以快速弹簧跟随变化；加载指示器每秒旋转一次，模型工作时状态文字闪烁。
- 新文字每次以几个词为单位淡入，回答会跟随文字末尾，直到人们向上滚动。
- 投票行在其各状态之间交叉淡化，高度以弹簧跟随变化；模型身份以相同的交叉淡化揭示。
- 速度和费用数字逐位翻动。投票后打开战绩榜会重放移动过程：各行滑动到新位置，条形增长或缩短。
- 减少动效时取消弹簧和位移，保留文字的节奏但不做淡入；关闭动效（Off）则立即应用每一处变化。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 状态、名称、投票行 | 交叉淡化，宽度和高度弹簧 | 120 毫秒交叉淡化 | 立即 |
| 回答文字 | 整词，160 毫秒淡入 | 相同的步进，无淡入 | 相同的步进，无淡入 |
| 战绩榜重放 | 各行滑动，条形缩放 | 各行和条形原地变化 | 立即 |
| 跟随流式输出 | 每帧缓动 | 直接跳到末尾 | 直接跳到末尾 |

## 相关组件

- `AiChat`：与单个助手的对话。
- `PromptStudio`：编辑提示并比较其版本。
- `ModelPicker`：单独选择模型。

| 组件 | 何时选用 |
|---|---|
| `AiChat` | 由一个助手作答 |
| `PromptStudio` | 被比较的对象是提示 |
| `ModelPicker` | 只需要选择模型 |
