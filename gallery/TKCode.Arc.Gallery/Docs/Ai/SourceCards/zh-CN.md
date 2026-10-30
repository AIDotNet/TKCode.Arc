---
title: Source cards
summary: AI 回答下方的一排引用来源：模型搜索时卡片流入预留的位置，超出的部分收拢为一叠网站标记，悬停时浮出带原文片段的预览，正文中的 [n] 引用会高亮对应的卡片。
source-hash: 5ae8e5408b8e
---

<Demo name="Preview" />

## 何时使用

`SourceCards` 展示 AI 回答所依据的网页。模型搜索时，一个加载圈和闪烁的 "Searching 3 of 7" 位于预留的位置之上，每条结果到达时淡入自己的位置，因此这一排不会跳动。搜索完成后，状态行显示 "7 sources · 2.1s"，回答在卡片上方流式出现，回答中的 `SourceCitation` 标签会高亮它所引用的卡片。超过 `Max` 的来源收拢为一叠相互重叠的标记（"+4 more"）。悬停或聚焦一张卡片时，会浮出一个预览，显示回答所用的原文片段。

- **AI 搜索或文档助手回答下方的引用。**
- **实时的搜索进度**（"Searching 3 of 8"），在智能体浏览网页时显示。
- **把生成文本中的行内 [n] 引用**链接到它们出自的网页。

## 何时不用

- **完整的搜索结果页**，包含大量结果和筛选条件：请改用列表布局。
- **带参数和输出的工具调用**：使用 `ToolCall`。
- **正文中的单个链接预览**：使用工具提示或悬停卡片。

## 用法

按引用顺序把来源交给卡片，这样第 1 个来源就以 [1] 引用。`Status` 为 `Searching` 且 `Total` 说明将有多少条结果时，随着结果到达向一个可观察列表追加；模型开始作答时切换为 `Done`。把回答放进 `Content`：它位于状态行和卡片之间，流式输出时高度以弹簧变化，卡片随之平滑下移。

```csharp
var sources = new AvaloniaList<SourceCardsSource>();
cards.Sources = sources;
cards.Query = "webhook signature verification";
cards.Total = 8;
cards.Status = SourceCardsStatus.Searching;

// As each result arrives.
sources.Add(new SourceCardsSource(result.Title, result.Url) { Site = result.Site, Snippet = result.Passage });

// When the model starts answering.
cards.Duration = searchTime;
cards.Status = SourceCardsStatus.Done;
```

没有 `Sources` 时，卡片显示你设置的状态；什么都不设置时显示 "No sources found"。用于没有搜索的演示或原型时，给卡片一个 `SourceCardsSimulation`，并传入要播放的来源和回答（写成 `[n]` 的词会变成引用）。它在 `Sources` 为 null 且显示在屏幕上时播放，`Scenario` 可选 Live、Error 或 Empty，切换场景或按下重试时会从头开始。模拟的卡片不会打开任何链接；设置 `Sources` 后模拟即结束。

```csharp
cards.Simulation = new SourceCardsSimulation
{
    Sources = recordedResults, // SourceCardsSource[] in citation order
    Query = "webhook signature verification",
    Answer = "Read the raw body before you parse it [1].",
    Duration = TimeSpan.FromSeconds(1.4),
    Scenario = SourceCardsScenario.Live,
};
```

### 流式输出带引用的回答

<Demo name="Streaming" />

`SourceCitation` 是一个行内标签。用 `InlineUIContainer` 把它放进回答；在 `SourceCards` 内部，它会为屏幕阅读器说出自己的来源，在对应卡片高亮时变为强调色，按下它会把卡片高亮 2.6 秒（卡片在叠放中时会先展开）。

```xml
<arc:SourceCards Status="Done">
    <TextBlock TextWrapping="Wrap">
        <Run Text="Read the raw body before parsing it " />
        <InlineUIContainer BaselineAlignment="Center">
            <arc:SourceCitation Number="1" />
        </InlineUIContainer>
        <Run Text="." />
    </TextBlock>
</arc:SourceCards>
```

自己设置 `Highlighted` 可以保持高亮（例如读者悬停在某句话上时），调用 `HighlightSource(n)` 则得到一次定时高亮。`HighlightChanged` 报告引用被按下。

### 叠放与窄容器

<Demo name="Narrow" />

宽容器显示 `Max` + 1 个等宽列；窄于 540px 时网格变为两列，叠放是最后一格。叠放、Show all 开关以及指向叠放内部的引用都会把它展开（`IsExpanded`，双向绑定），并引发 `ExpandedChanged`。

### 自己的阅读器与失败

<Demo name="Reader" />

按下卡片会引发 `SourceInvoked`，然后在浏览器中打开地址。把事件标记为已处理，就能改在自己的阅读器里打开来源；或者设置 `OpensLinks="False"`，让卡片从不打开链接。搜索失败时保留已经到达的结果，去掉预留的位置，并显示 "Search failed" 和 `ErrorText`；`AllowsRetry` 会加上 Retry（没有找到任何结果时是 Search again），两者都引发 `RetryRequested`。

### 状态行

| 状态 | 图标 | 显示 |
|---|---|---|
| `Searching` | 加载圈 | "Searching 3 of 7" 和查询词 |
| `Done` | 地球 | "7 sources · 2.1s" |
| `Done`，没有来源 | 带叉的搜索 | "No sources found" 和查询词 |
| `Error` | 警示 | "Search failed" 和 `ErrorText` |

当界面已经显示搜索进度时，设置 `IsStatusVisible="False"`；Show all 和 Retry 仍会保留。

### 网站标记

卡片根据域名绘制一个带字母的圆（webref.example 显示 W）。请把网站图标的形状作为 24 × 24 的描边几何图形传给 `SourceCardsSource.Icon`，而不是自行编造标志。

### 自定义文案

`Labels` 包含卡片及其引用显示或朗读的每一个词。替换整个对象即可本地化或改写：

```csharp
cards.Labels = new SourceCardsLabels
{
    Searching = "Recherche",
    Count = n => n == 1 ? "1 source" : $"{n} sources",
    Progress = (n, total) => total is { } t ? $"{n} sur {t}" : $"{n}",
};
```

### 最佳实践

- 让来源保持引用顺序，使 [n] 对应第 n 张卡片；追加，而不是重建列表。
- 搜索时传入 `Total`，这样位置会被预留，什么都不会跳动。
- 不要用自己的链接包裹卡片：在应用内阅读请处理 `SourceInvoked`。

## API 参考

<!-- api: SourceCards -->

<!-- api: SourceCitation -->

<!-- api: SourceCardsSource -->

<!-- api: SourceCardsSimulation -->

<!-- api: SourceCardsLabels -->

## 无障碍

- 卡片是一个名为 "Sources" 的列表，搜索时报告自己处于忙碌状态；预留的位置对辅助技术隐藏。
- 一个礼貌型实时区域对每种状态只播报一次：正在搜索某个查询、找到的数量、没有来源，或失败。不会在每张卡片到达时播报数量。模拟搜索会说明它是模拟的，其卡片不会打开任何内容。
- 每张卡片的名称依次读出编号、标题和域名，并说明它会在浏览器中打开。预览打开期间，原文片段是这张卡片的帮助文本。
- `SourceCitation` 是一个真正的按钮，名称如 "Source 2: Request: text() method, webref.example"；它的高亮从不只靠颜色，因为对应卡片还会出现轮廓。
- 状态行总是图标与文字并用。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过 Retry 或 Show all、引用、每张卡片和叠放。获得焦点的卡片会显示它的预览 |
| Enter, Space | 打开卡片（或引发 `SourceInvoked`）、展开叠放，或高亮引用对应的卡片 |
| Escape | 关闭打开的预览 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 卡片到达 | 在 180ms 内淡入并上升 4px；它的占位在 120ms 内淡出 | 只淡入，不上升 | 立即 |
| 预留的位置 | 条块脉动 | 静止 | 静止 |
| 叠放收起，或失败的搜索去掉占位 | 离开的格子在原地用 120ms 淡出，同时叠放淡入它的格子；网格高度平滑过渡一次 | 淡入淡出，高度立即变化 | 立即 |
| 数量变化 | 在预留宽度内滚动 5px（等宽数字） | 立即 | 立即 |
| 状态图标与文字 | 交叉淡化，淡出 100ms、淡入 160ms | 120ms 交叉淡化 | 立即 |
| 回答流式输出 | 高度以无回弹的弹簧变化，卡片平滑下移；宽度变化时直接跳到新高度 | 立即 | 立即 |
| 预览打开 | 悬停 320ms 后，在 160ms 内从 0.98 倍缩放淡入并上升 4px；之后在卡片之间立即切换 | 只淡入 | 立即 |

没有任何回弹。模拟搜索在滚出屏幕时暂停。

## 相关组件

- `ToolCall`：智能体发起的工具调用。
- `AgentPlan`：智能体搜索时遵循的计划。
- `AgentStatus`：整个运行过程的实时状态。
