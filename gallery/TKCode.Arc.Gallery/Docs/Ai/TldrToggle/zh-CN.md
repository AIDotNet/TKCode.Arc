---
title: TL;DR toggle
summary: 一张文章卡片，可在原文位置流式呈现 AI 摘要或要点，带有长度滑块，每条要点都能跳回其出处段落。
source-hash: 036af066c83d
---

<Demo name="Preview" />

## 何时使用

`TldrToggle` 是一张文章卡片，带有"全文""摘要""要点"切换和一个长度滑块。切换后，AI 摘要会在原文的位置流式出现，卡片高度随之以弹簧过渡到合适的大小；每条要点都链接回它所出自的段落，点击后该段落会滚动到视野中并高亮。

- **快速摘要或要点**：放在长文章、文档、工单或报告的上方，或直接替换原文。
- **让读者核对 AI 的结论**：要点链接回段落，读者可以对照原文。
- **阅读场景**：读者通过长度控件自行决定想看多少细节。

## 何时不用

- **针对内容的开放式提问**：改用问答或聊天组件。
- **很短的内容**：只有一两段时，摘要省不了时间。
- **一次汇总多篇文档**：改用列表或摘要汇编布局，而不是每项一张卡片。

## 用法

为卡片提供一个 `Article`，其段落带有稳定的 id，再提供一个流式返回 `TldrStreamEvent` 的 `Summarizer`。正文用 `TldrStreamEvent.OfText` 发送；要点则在每条要点的文字之前先发送 `TldrStreamEvent.OfPoint(paragraphId)`，这样它的跳转链接才会落在正确的段落上。没有摘要器时，卡片会用文章的首句模拟摘要（`TldrStreams.Simulate`），不会发送任何内容。既没有 `Article` 也没有 `Simulation` 时，卡片显示其安静的空状态：没有页眉，显示"This article has no text to summarize yet."，切换器和滑块处于关闭状态。

```csharp
card.Article = new TldrArticle
{
    Title = post.Title,
    SourceName = "Engineering blog",
    AuthorName = post.Author,
    Date = "Oct 21, 2026",
    Paragraphs = post.Paragraphs.Select(p => new TldrParagraph(p.Id, p.Text)).ToList(),
};
card.Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles);
card.Summarizer = request => myService.StreamTldr(request); // IAsyncEnumerable<TldrStreamEvent>
card.Jumping += (_, e) => analytics.Track("tldr_jump", e.ParagraphId);
```

请求中包含 `Mode`（`Summary` 或 `KeyPoints`）、`Length`（1 到 5）、`TargetWords`（35 到 130）、`PointCount`（3 到 7）、文章本身，以及一个 `CancellationToken`：在停止、按 Escape、切换模式或长度、换了文章，或卡片离开窗口时取消。把长度以 `TargetWords` 或 `PointCount` 传给模型，而不是"简短"这类含糊的词。

### 模拟

想在没有摘要器的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `TldrToggleSimulation`：文章、要标注的模型、为每个长度写好的摘要，以及按重要性排序的要点。卡片会在短暂的思考停顿之后流式输出写好的文字，说明这是模拟、没有发送任何内容，并在卡片有一半处于可见区域一秒后自动打开摘要（`AutoPlay="False"` 会让它停留在文章上）。没有写好摘要的长度会使用文章的首句。你自己的 `Article` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示空状态。

```csharp
card.Simulation = new TldrToggleSimulation
{
    Article = post,
    Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles),
    Summaries = [shortest, shorter, medium, longer, longest],
    KeyPoints = [new TldrKeyPoint("results", "Median cold start fell from 4.2 seconds to 0.9.")],
};
```

上方的预览播放的就是一个模拟，其文章、摘要和要点来自图库自己的演示数据。

### 结构

1. **页眉**：来源的标志（或放在着色方块中的首字母）、名称、日期和阅读时间；标题；带头像的作者。
2. **控件**：模式切换（`SegmentedControl`）与长度滑块（`Slider`），滑块显示"~60 words"或"4 points"，在全文模式下禁用。
3. **正文**：文章、流式摘要或要点清单，高度以弹簧跟随内容。
4. **页脚**：以图标加文字表示的状态、带 14px 标志的模型，以及停止、重新生成和复制按钮。

### 要点

<Demo name="KeyPoints" />

要点以清单形式出现：每条要点写完时，图标从虚线圆圈变为对勾。整张清单完成后，每条要点的 ¶ 链接会切回文章，等高度稳定后把段落滚动到视野中，把焦点移过去并高亮它。在此之前会先触发 `Jumping`，参数是段落的 id。在代码中调用 `JumpTo(id)` 也能做到同样的事。

### 结果、停止与重新生成

完成的结果按模式和长度保留，所以切回去是即时的，不会再发请求。`Regenerate`（页脚的按钮）会重新请求。停止或按 Escape 会结束流式输出，并保留已收到的内容。`Completed` 会报告每个完成的结果，附带文字、要点和字数，可用于统计或服务端缓存；被停止或失败的运行不会触发它。`ModeChanged` 与 `LengthChanged` 报告读者做出的更改；滑块在拖动松开或按键调整时才报告长度，所以每次更改只发一个请求。

### 自定义摘要器

<Demo name="YourSummarizer" />

服务能流式返回时就流式返回。如果它一次返回完整结果，用 `TldrStreams.FromResult(text, token)` 或 `TldrStreams.FromResult(points, token)` 包装，卡片仍会以每块 3 到 8 个词的节奏流式呈现。

### 失败

<Demo name="Failure" />

摘要器抛出的异常会以警示框显示其消息，并附带"Try again"按钮。`TldrStreams.Simulate(request, fail: true)` 和 `TldrToggleSimulation.Summarize(request, fail: true)` 会在思考停顿之后抛出异常。自行包装模拟时，请设置 `IsSimulated="True"`，让播报和错误文字都注明"Simulated, nothing was sent"。

### 窄容器

<Demo name="Narrow" />

卡片会填满容器，最宽 680px。宽度小于 520px 时，长度滑块移到模式切换下方并占满整行；小于 440px 时，内边距从 24px 缩小到 16px。页脚文字保持一行，模型名称最先被截断。

### 自动播放

没有摘要器时，卡片在至少一半进入视野满一秒后会自动打开摘要，除非读者已经在卡片里按过鼠标或键盘。设置 `AutoPlay` 可以明确开启或关闭。

### 自定义文字

`Labels` 包含卡片显示或播报的所有文字。替换整个对象即可本地化或改写：

```csharp
card.Labels = new TldrToggleLabels
{
    Full = "Article",
    KeyPoints = "Highlights",
    Words = words => $"{words} words",
};
```

### 最佳实践

- 为段落提供稳定的 id，并让模型返回每条要点出自哪个 id。
- 以 `TargetWords` 或 `PointCount` 传递长度。
- 没有摘要器时卡片运行的是模拟，并会在播报中说明；切勿把它发布到生产环境。

## API 参考

<!-- api: TldrToggle -->

<!-- api: TldrArticle -->

<!-- api: TldrParagraph -->

<!-- api: TldrKeyPoint -->

<!-- api: TldrRequest -->

<!-- api: TldrStreamEvent -->

<!-- api: TldrStreams -->

<!-- api: TldrToggleSimulation -->

<!-- api: TldrToggleLabels -->

<!-- api: TldrModeChangedEventArgs -->

<!-- api: TldrLengthChangedEventArgs -->

<!-- api: TldrCompletedEventArgs -->

<!-- api: TldrJumpEventArgs -->

<!-- api: AgentModel -->

## 无障碍

- 卡片以文章标题命名，标题是二级标题。
- 模式切换是带标签的分组（"Reading mode"）；长度滑块朗读的值是目标（"~60 words"、"4 points"），在全文模式下禁用。
- 一个礼貌型实时区域对每次运行只播报一次（"Summarizing with GPT-6 Luna"、"Summary ready, 58 words"、"4 key points ready"、"Stopped"），从不朗读流式片段。使用模拟时会追加"Simulated, nothing was sent"。
- 失败以警示框呈现，附带消息和"Try again"按钮。
- 跳转链接以其要点命名（"Jump to paragraph 3: Partial bootstrapping loads..."），跳转会把焦点移到高亮的段落，让键盘和读屏用户落在视线所在的位置。
- 状态从不只靠颜色表达：完成、停止和失败都配有图标和文字，要点从虚线圆圈变为对勾。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过模式切换、长度滑块、跳转链接和页脚按钮 |
| 方向键 | 在模式切换上，于全文、摘要和要点之间移动；在滑块上，把长度改变一档（每档一个请求） |
| Home、End | 第一个或最后一个模式，或最短、最长的长度 |
| Enter、Space | 在跳转链接上，切回文章，滚动到对应段落，聚焦并高亮它 |
| Escape | 停止正在进行的流式输出，保留已收到的内容 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 正文高度（切换视图或流入文字） | 一个弹簧，0.26s，无回弹 | 立即 | 立即 |
| 容器尺寸变化 | 直接跳到新高度 | 立即 | 立即 |
| 视图切换 | 旧视图 100ms 淡出，新视图 160ms 淡入 | 120ms 交叉淡化 | 立即 |
| 页脚状态与按钮 | 同样的交叉淡化 | 120ms 交叉淡化 | 立即 |
| 流式片段（3 到 8 个词，每 30 到 40ms 一块） | 140ms 淡入 | 直接出现 | 直接出现 |
| 已保留的结果 | 整体出现，不淡入 | 直接出现 | 直接出现 |
| 思考（约 0.7s） | 正文那一行平静地闪烁；随后页脚的 Writing 闪烁 | 静止 | 静止 |
| 要点打勾 | 虚线圆圈淡出并缩小，对勾以同一弹簧放大出现 | 立即 | 立即 |
| 跳转 | 等待高度稳定，平滑滚动，高亮 160ms 淡入、600ms 淡出 | 直接跳到，高亮淡入淡出 | 直接跳到，高亮直接显示 |

同一时间只有一处闪烁。

## 相关组件

- `ReasoningBlock`：AI 回复中的思考过程。
- `SegmentedControl` 与 `Slider`：卡片用它们来切换模式和调整长度。
- `CopyButton`：复制一个值并立即确认。
- `ToolCall`：智能体记录中的工具调用。
