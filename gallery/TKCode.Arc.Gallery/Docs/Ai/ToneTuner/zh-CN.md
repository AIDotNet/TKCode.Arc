---
title: Tone tuner
summary: 一张品牌语气卡片：两个滑块（正式程度与详略）和语气标签改写一封客户回复，每个改动过的句子原地交叉淡化，新词可以对照原稿高亮，"复制"取走当前版本。
source-hash: 1659bebbd79f
---

<Demo name="Preview" />

## 何时使用

`ToneTuner` 为 AI 改写提供几个旋钮。一封客户回复放在卡片里，配有 Friendly、Confident、Empathetic 三个语气标签，以及正式程度与详略两个滑块。每次调整后稍候片刻，它会向你的模型请求新版本；只有改动过的句子会淡出再淡入，相对原稿新增的词可以高亮显示，"复制"取走屏幕上的版本。

- **撰写器或客服工单回复**：发送前提供 AI 语气控制。
- **品牌语气工具**：营销人员比较同一段文案的不同版本。
- **任何改写场景**：人们更愿意拧几个旋钮，而不是写提示词。

## 何时不用

- **带提示词输入框的自由 AI 编辑**：改用聊天或撰写器。
- **长文档**：卡片的尺寸适合几段文字的消息。
- **逐行精确的代码或合同差异**：使用专门的差异视图。

## 用法

把原稿传给 `Text`，把你的模型调用传给 `Rewrite`。该函数收到一个 `ToneRewriteRequest`，其中包含原稿、语气（`Formality`、`Detail`、`Voice`）以及现成的 `Instructions` 字符串（它已经要求模型保留事实、数字与姓名），还有一个 `CancellationToken`，当更新的调整取代这次请求时它会被取消。返回段落分隔与原稿相同的纯文本，这样句子才能对齐，只有改动过的句子会产生动画。

```csharp
tuner.Text = draft;
tuner.RecipientName = "Sofia Ramirez";
tuner.RecipientSubject = "Re: Duplicate charge on INV-20481";
tuner.Rewrite = async (request, token) =>
{
    var reply = await model.CompleteAsync(request.Instructions, request.Text, token);
    return reply.Text;
};
tuner.Rewritten += (_, e) => Save(e.Text, e.Value);
```

既没有 `Text` 也没有 `Simulation` 时，卡片是空的：没有标题栏，消息为空，语气标签、滑块、"Changes"和"复制"都处于关闭状态，直到有原稿传入。你自己的原稿如果没有 `Rewrite`，会用与模拟相同的简单规则改写（见下文），不会发送任何内容。

### 模拟

想在没有模型的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `ToneTunerSimulation`：原稿、收件人、要显示的模型名称，以及你为每种语气写好的版本（`ToneTunerVersion`）。卡片会在 `Latency`（650ms）之后用对应版本回应每种语气，说明这次改写是模拟的、没有发送任何内容，并自动轮流展示各种语气，直到有人在卡片内按下或聚焦任何元素（`AutoPlay="False"` 让它保持静止）。没有对应版本的语气会用几条简单规则改写（缩写、问候语、减少句子、加一句开场白）。你自己的 `Text` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示空卡片。

```csharp
tuner.Simulation = new ToneTunerSimulation
{
    Text = draft,
    RecipientName = "Sofia Ramirez",
    RecipientSubject = "Re: Duplicate charge on INV-20481",
    Model = "Claude Sonnet 5.5",
    Versions =
    [
        new ToneTunerVersion(new ToneSettings(2, 1, ToneVoice.Neutral), formalReply),
        new ToneTunerVersion(new ToneSettings(0, 1, ToneVoice.Friendly), friendlyReply),
    ],
};
```

上方的预览播放的就是一个模拟，其原稿和各个版本来自图库自己的演示数据。让滑块不涉及的句子在各版本之间保持完全相同，这样只有改动过的句子会交叉淡化。

### 你的模型

<Demo name="YourModel" />

原稿在起始语气（`ToneSettings.Default`：适中、标准、中性）下显示。其他任何语气会在最后一次调整后等待 `Debounce`（220ms），再调用 `Rewrite`，因此拖动滑块时，停下后最多只发送一次请求；语气再次变化时，仍在运行的请求会被取消，其结果被忽略。`Rewritten` 报告每个落定的版本、它的语气以及是否为模拟。

### 语气

`Value` 是语气，双向绑定：`new ToneSettings(formality, detail, voice)`，各档位为 0 到 2。`ValueChanged` 在每次滑块步进和标签按下时触发，早于防抖。标签会设置它的语气，并把两个滑块移到它的起点；再按一次则回到中性语气。用 `Presets` 替换这些标签：

```csharp
tuner.Presets =
[
    new TonePreset { Voice = ToneVoice.Friendly, Label = "Warm", Icon = ArcIcons.Smile, Formality = 0, Detail = 1 },
    new TonePreset { Voice = ToneVoice.Confident, Label = "Direct", Icon = ArcIcons.Zap, Formality = 1, Detail = 0 },
];
```

### 改动

`ShowChanges`（双向，即 Changes 开关）用圆角强调色背景高亮与原稿不同的词，页眉会统计它们的数量。在代码中可以通过 `ChangedWords` 和 `DisplayedText` 读取同样的数量与文字。调整期间请保持原稿不变：比较始终针对 `Text`。

### 失败

<Demo name="Failure" />

当 `Rewrite` 抛出异常时，屏幕上的文字保持不变，状态显示"Couldn't rewrite"并附带重试按钮，实时区域会读出错误信息。在代码中可以用 `Retry()` 再次运行最近的语气。

### 窄屏

<Demo name="Narrow" />

卡片在 640px 以内自适应宽度。窄于 520px 时，两个滑块上下堆叠，标签在一行内等宽排列，状态隐藏其设置说明。窄于 400px 时，标签图标、改动词数和"复制"的文字都会去掉，以免被截断；在 360px 下依然完整。

### 自定义文案

`Labels` 包含卡片显示或朗读的所有文字，包括滑块朗读的档位名称：

```csharp
tuner.Labels = new ToneTunerLabels
{
    Formality = "Ton",
    FormalityLevels = ["Familier", "Neutre", "Soutenu"],
    Rewriting = model => $"Réécriture avec {model}",
};
```

### 最佳实践

- 把 `Instructions` 作为系统或用户指令发送给模型；它已经要求保留事实、数字与姓名。
- 返回与原稿相同的段落分隔，让未改动的句子保持静止。
- 在 `Model` 中写明模型名称，让状态说明是谁在改写。

## API 参考

<!-- api: ToneTuner -->

<!-- api: ToneSettings -->

<!-- api: TonePreset -->

<!-- api: ToneRewriteRequest -->

<!-- api: ToneTunerSimulation -->

<!-- api: ToneTunerVersion -->

<!-- api: ToneTunerLabels -->

## 无障碍

- 语气标签是会报告按下状态的切换按钮，位于名为 Voice 的分组中。
- 每个滑块都有名称（Formality、Detail），朗读的是档位名称（"Formal"）而不是数字。
- 一个礼貌型实时区域会播报每次落定的改写及其设置，说明改写是模拟的（没有发送任何内容），并报告失败。
- 高亮的词会在页眉中计数（"12 words changed"），因此改动不只靠颜色表达。
- 状态把图标与简短标签配对：Original draft、Rewriting、Rewritten，或带重试按钮的 Couldn't rewrite。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过语气标签、两个滑块、显示时的重试按钮、Changes 开关和复制按钮 |
| Enter、Space | 按下语气标签、切换 Changes、重试或复制 |
| 左、右方向键 | 让获得焦点的滑块在三个档位之间步进 |
| Home、End | 让获得焦点的滑块跳到第一个或最后一个档位 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 改写落定 | 每个改动过的句子在 100ms 内淡出，所有句子在同一帧替换（只重排一次，且不可见），新句子在 160ms 内淡入；未改动的句子从不动画 | 立即替换 | 立即替换 |
| 正式程度或详略变化 | 文字区域以无回弹的弹簧过渡到新的预留高度 | 立即 | 立即 |
| 语气变化 | 没有任何移动：文字区域已经为当前正式程度与详略下最高的语气预留了高度 | 没有移动 | 没有移动 |
| 状态 | 与输入在同一帧切换为 Rewriting，带柔和的闪光，然后交叉淡化为 Rewritten | 无闪光，短暂淡化 | 立即 |
| Changes 开关 | 高亮在 160ms 内淡入淡出 | 立即 | 立即 |
| 自动轮播 | 在屏幕上时每 3.2s 换一种语气，直到卡片内第一次按下或获得焦点 | 关闭 | 关闭 |

## 相关组件

- `Slider`：单独使用的正式程度与详略旋钮。
- `Switch`：立即生效的设置。
- `CopyButton`：带确认的复制。
- `AgentStatus` 与 `ToolCall`：AI 对话的其他部分。
