---
title: AI translate
summary: 一张消息卡片：选择一种语言后，AI 译文在原处流式出现，逐句覆盖变暗的原文，并带有检测到的语言标签、“显示原文”开关和状态行。
source-hash: 0c94b955a2d4
---

<Demo name="Preview" />

## 何时使用

`AiTranslate` 在产品内按需翻译一条消息或一段文字。文字上方是作者署名和一个标明文字语言的标签；下方是 Translate 选择器、Show original 开关，以及以模型标志开头的状态行。选择一种语言后，屏幕上的文字会变暗，译文在原处流式出现：每开始一个新句子，就撤下一句旧文字，同时卡片高度以弹簧动画适配内容。完成的译文会被保留，因此“显示原文”和切换回来时都会立即交叉淡化。

- **用另一种语言写的消息、评论、评价或工单。**
- **多个地区的读者会用自己的语言打开的发布说明或公告。**
- **任何希望 AI 翻译显得内联、可撤回的地方**，原文只需一次点击。

## 何时不用

- **翻译整个页面或应用界面**：使用你的本地化框架。
- **带标题和列表的长文档**：卡片只渲染一段纯文本。
- **实时字幕或语音**：使用实时流组件。

## 用法

给卡片设置 `Text`，并把 `Translator` 接到你的模型。翻译函数接收一个 `AiTranslateRequest`（文字、源语言、目标语言和取消令牌），并以文本增量的异步流返回译文。可以在模型流式输出时逐段 yield，也可以一次 yield 整段译文：无论哪种方式，卡片都会把收到的内容按每 34ms 一块、每块 3 到 6 个词的节奏放出。

```csharp
card.Text = message.Body;
card.Translator = TranslateAsync;

async IAsyncEnumerable<string> TranslateAsync(AiTranslateRequest request)
{
    // Pass the token on: it is cancelled when the reader picks another language or shows the original.
    await foreach (var delta in model.StreamAsync(request.Text, request.From, request.To, request.CancellationToken))
    {
        yield return delta;
    }
}
```

没有翻译函数时，如果传了现成译文，卡片会用 `Translations` 模拟流式输出。既没有 `Text` 也没有 `Simulation` 时，卡片是空的：没有署名行和语言标签，段落为空，翻译选择器处于关闭状态，直到有文字传入。

### 模拟

想在没有翻译函数的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `AiTranslateSimulation`：消息、署名、要标注的模型以及预先做好的译文。选择一种语言后，卡片会在短暂等待后原地流式输出现成译文（没有译文的语言会显示失败状态），状态会说明这是模拟、没有发送任何内容，并且卡片会自动依次播放 `Tour`，直到有人在卡片内按下指针或按键。你自己的 `Text` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示空卡片。

```csharp
card.Simulation = new AiTranslateSimulation
{
    Text = "La version 4.2 est disponible.",
    Author = new AiTranslateAuthor { Name = "Camille Laurent", Role = "Product lead" },
    ModelName = "Gemini 3.8 Flash",
    Translations = new Dictionary<AiTranslateLanguage, string>
    {
        [AiTranslateLanguage.English] = "Version 4.2 is out.",
        [AiTranslateLanguage.German] = "Version 4.2 ist da.",
    },
    Tour = [AiTranslateLanguage.English, AiTranslateLanguage.German],
};
```

上方的预览播放的就是一个模拟，其版本说明和译文来自图库自己的演示数据。

### 从你的模型流式输出

<Demo name="Streaming" />

未设置 `SourceLanguage` 时，卡片会通过检查常见词来粗略检测源语言，标签显示“Detected · German”。失败（翻译函数抛出异常）时，之前的文字会恢复，显示“Couldn't translate”和 Retry，异常消息保留为状态行的提示，并引发 `TranslationFailed`。被取消的请求不会引发它。

### 现成译文

<Demo name="Ready" />

把预先做好的译文传给 `Translations`，卡片会用模拟流式输出它们。缺少的语言会显示失败状态。已知源语言时请设置 `SourceLanguage`，标签会显示“Written in Spanish”。把 `Value` 设为源语言以外的语言时，卡片一显示就开始翻译。没有 `Author` 时卡片不显示署名行，语言标签保留。

### 跟随偏好语言

<Demo name="Thread" />

`Value` 是屏幕上的语言（双向）；null 或源语言表示原文。每条消息渲染一张卡片，并用共享的偏好语言设置每张卡片的 `Value`。只有读者选择语言或切换“显示原文”时才会引发 `ValueChanged`，因此可以在其中记住读者的选择。“显示原文”会记住上一次的译文，供“显示译文”使用。

### 窄屏

<Demo name="Narrow" />

宽度小于 470px 时，卡片内边距变为 16px，状态行移到控件下方，与正文左对齐；小于 380px 时，标签只显示语言名称。名字和附加信息超出时以省略号截断。

### 结构

1. **署名行**：带首字母的头像、名字及其下方的角色和时间，最右侧是语言标签。
2. **正文**：屏幕上语言的文字。译文流式输出时，新文字在前，剩下的旧文字变暗跟在后面。
3. **控件**：Translate 选择器（翻译后显示当前语言的本地名称）、Show original 开关，以及失败后出现的 Retry。每个控件高 32px，并预留其最长标签的宽度。
4. **状态行**：模型标志加一个标签：空闲时是模型名称，然后是“Translating to German”“Translated from French”“Showing original”，或警告图标加“Couldn't translate”。

### 状态

| `State` | 正文 | 状态行 |
|---|---|---|
| `Original` | 原样的文字 | 模型名称，之后是“Showing original” |
| `Translating` | 译文在变暗的文字上流式出现 | “Translating to German” |
| `Translated` | 译文 | “Translated from French” |
| `Failed` | 之前的文字，已恢复 | 警告图标和“Couldn't translate”，附 Retry |

### 保存译文

译文完整到达后、卡片缓存之前会引发 `Translated`。缓存按卡片、按文字保存；设置新的 `Text` 会清空缓存并显示新的原文。要跨会话保留译文，请在 `Translated` 中存储，再通过 `Translations` 传回。

### 自定义文字和标志

`Labels` 包含卡片显示或朗读的每个词，包括语言名称。`ModelName` 是空闲时的状态，`ModelMark` 是每个状态开头的 24 × 24 描边图形；传入你的服务商的标志。

```csharp
card.ModelName = "Gemini 3.8 Flash";
card.Labels = new AiTranslateLabels
{
    Translate = "Traduire",
    ShowOriginal = "Voir l'original",
    TranslatingTo = language => "Traduction en " + language,
};
```

### 自动播放

播放模拟的卡片会依次切换到模拟的 `Tour` 中的语言（预览中是 English、Deutsch、日本語 和 Português），再回到原文，并循环播放；没有 `Tour` 时会依次播放每种有译文的语言。卡片离开屏幕时暂停，在卡片内第一次按下指针或按键后永久停止。`AutoPlay` 可为任意卡片开启或关闭（你自己的卡片会依次播放选择器中的每种语言）；减少动态效果时只翻译一次就停止。`IsSimulated` 会在播报中加上“Simulated, nothing was sent”（没有翻译函数或正在播放模拟时默认开启）。

### 内容指南

- 每张卡片放一段或一条消息。对话线程中每条消息渲染一张卡片。
- 标签写源语言；选择器用每种语言的本地名称列出语言，源语言标为 Original。
- 在 `ModelName` 中写你实际调用的模型。

### 最佳实践

- 始终把请求的取消令牌传给网络调用，这样切换语言时旧请求会停止。
- 收到增量就 yield，不要等整段译文完成。
- 已知源语言时提供 `SourceLanguage`；检测只是为短消息准备的粗略检查。

### 常见问题

**句子怎么切分？** 在后跟空格的 `.`、`!` 和 `?` 处切分，也在 `。`、`！` 和 `？` 处切分。4.2 这样的小数保持完整。

**流式输出途中切换语言会浪费请求吗？** 会取消它：令牌被取消，之前的文字恢复，然后新语言开始流式输出。

## API 参考

<!-- api: AiTranslate -->

<!-- api: AiTranslateAuthor -->

<!-- api: AiTranslateRequest -->

<!-- api: AiTranslateSimulation -->

<!-- api: AiTranslateLabels -->

## 无障碍

- 卡片的无障碍名称是“Message from”加作者名；正文的名称是屏幕上的文字，译文流式输出时报告为忙碌。
- 开始、完成、显示原文和失败都会在礼貌型状态区域中播报；模拟时，播报会说明没有发送任何内容。
- 语言菜单是一组单选行，当前语言处于选中状态。Show original 开关报告按下状态，名称切换为 Show translation。
- 状态从不只靠颜色表达：模型标志加标签，或警告图标加“Couldn't translate”。
- 每个控件至少高 32px，标签预留其最长版本的宽度，指针下的内容不会移动。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过 Translate 选择器、Show original 开关，失败后还有 Retry |
| Enter、Space、Arrow down | 在选择器上打开语言菜单，定位到当前语言 |
| 方向键、Home、End | 在语言间移动；输入字母跳到对应语言。列表循环 |
| Enter、Space | 选择高亮的语言并关闭菜单。选择刚失败的语言会重试 |
| Escape | 关闭菜单，焦点回到选择器 |

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 一块文字到达 | 每 34ms 一块，每块 3 到 6 个词（日语约 10 个字符），各自在 140ms 内淡入；没有模糊、滑动或弹簧 | 立即 | 立即 |
| 开始翻译 | 屏幕上的文字在 200ms 内变暗到 45% | 立即 | 立即 |
| 新句子开始 | 撤下一句变暗的旧句子 | 相同 | 相同 |
| 已缓存的译文或原文 | 一次交叉淡化：旧文字在 100ms 内离开，新文字在 160ms 内淡入并上升 2px | 立即 | 立即 |
| 正文高度 | 以临界阻尼弹簧（0.24s，无回弹）跟随文字；窗口尺寸变化时直接跳到新高度 | 立即 | 立即 |
| 状态、选择器和开关的标签 | 在预留宽度内交叉淡化（100ms 淡出，160ms 淡入） | 短淡化 | 立即 |
| 自动播放 | 循环 | 翻译一次后停止 | 翻译一次后停止 |

## 相关

- `ToolCall`：同一对话记录中智能体发起的工具调用。
- `DropdownMenu`：锚定在触发器上的一组聚焦操作。
- `Badge`：类似语言标签的小标签。
