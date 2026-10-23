---
title: Prompt cache
summary: 共享同一提示前缀的请求会从缓存中读取它，而不再重新处理，节省的成本会不断累积。
source-hash: 5d5a1b5417ae
---

<Demo name="Preview" />

## 何时使用

`PromptCache` 用一个简短的模拟故事讲解提示缓存。一轮最多四个请求共享同一个稳定前缀（工具模式、指令、文档、已有的对话），并各自在末尾添加一条新消息。第一个请求处理整个提示并存储前缀；之后的请求读取前缀，只处理各自的消息。卡片汇总这些操作对输入成本以及第一个输出 token 之前等待时间的影响。它是一幅带有模拟时钟和示例价格的讲解插图，而不是对真实缓存的监控。

- **API 与 SDK 文档**：介绍缓存断点或缓存输入定价的页面。各行展示读者在编写任何代码之前，为什么稳定前缀很重要。
- **定价与用量页面**：解释缓存流量所涉及的三种价格（输入、缓存写入、缓存读取）以及存活时间的章节。传入你自己的价格，总计会随之变化。
- **AI 产品的新手引导**：首次运行时解释提示顺序为何重要。修改开关可以展示：靠近提示开头的修改，比靠近末尾的修改成本更高。
- **工程文章与内部培训**：关于延迟的演讲或文章，其中首个 token 的加速比是人们最容易记住的数字。

## 何时不用

- **报告真实的缓存命中率**：使用 `LineChart` 或 `BarChart`，并填入你遥测数据中的数字。本卡片中的每个数字都由你提供的示例数据计算得出。
- **展示配额的使用比例**：使用 `UsageMeter`。它显示某个上限的真实占比；本卡片讲述的是一个故事。
- **列出真实请求及其成本**：使用 `DataGrid`，并填入你日志中的行。
- **解释其他推理思路**：`SpeculativeDecoding` 解释草稿与验证，`AgentLoop` 解释智能体围绕上下文、动作与检查的循环。

## 用法

卡片不需要数据：它自带一个故事，内容是一家货运公司的客服助手，其四个问题共享一个 45.5K token 的前缀。卡片第一次大部分可见时开始播放，并循环进行。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PromptCache MaxWidth="744" />
</UserControl>
```

`PromptCache` 派生自 `Explainer`，它是 Special 讲解类控件的基类，提供故事时钟、播放、单步与重新开始按钮、实时区域和减弱动效。卡片根据自身宽度进行布局，因此请给它所在列的宽度；744px 可以让请求与缓存并排显示。

### 结构

1. **标题**：标题（`Title`，默认为 "Prompt cache"）、模型行（来自 `Model` 的标记和名称，然后是存活时间 "5-minute cache"），以及三个圆形按钮：播放或暂停、发送下一个请求、重新开始。
2. **图例**：每个前缀段及其颜色和 token 数，然后是新消息。
3. **请求**：每个请求一行。行标题显示 "Request N"、发送时的模拟时间及其状态；其下是提示的条形图；右侧是三个数字：已处理的 token 数、输入成本和首个 token 时间。各行上方有两个标识，分别说明实心的已处理填充与浅色的已缓存填充。
4. **缓存面板**：一个圆环，显示剩余的存活时间；缓存状态（Empty、Writing、Reading、"Expires in 4:22" 或 Expired）；已缓存内容的条形图；已缓存的 token 数；以及三种价格。
5. **说明文字**：一到两句话，说明刚刚发生了什么。
6. **页脚**：本轮的输入成本，与相同提示不使用缓存时的成本对比；命中时的首个 token 加速比；以及修改开关。

条形图使用平方根刻度，因此 200 token 的消息能与 33,800 token 的知识库并排清晰可见；图例与数字给出真实的计数。

### 故事

在完整动效下，一轮大约需要十秒：

1. **请求 1 写入。** 缓存中没有内容，因此整个提示都要处理：每个段依次以稳定的速度填满，然后是新消息。前缀从行中飞出，进入缓存面板，途中从实心颜色变为浅色缓存色调。圆环填满，倒计时从存活时间开始。
2. **请求 2 至 4 命中。** 模拟时钟按该请求的间隔前进，圆环略微下降。前缀转为缓存色调，同时一道光带掠过它（以及面板的条形图）。只处理消息，圆环重新填满：每次命中都会重置存活时间。
3. **数字从上一行滚动过来。** 每行的三个数字从上一行的数值开始，因此你能看到命中时成本和首个 token 时间下降。
4. **缓存空闲并过期。** 没有更多请求时，时钟加速，圆环下降，并在最后一分钟转为警告色，然后清空。循环的卡片随后开始下一轮，其第一个请求已经写入；使用 `IsLooping="False"` 时，卡片停在已过期的缓存上。

卡片中的所有数字都是示意：它们由你传入的价格、token 数和首个 token 估算推算而来，而不是来自真实模型。

### 计算方式

设 `P` 为前缀 token 数，`m` 为消息 token 数，三种每 token 价格分别为：

| 结果 | 从缓存读取 | 写入缓存 | 已处理 | 输入成本 |
|---|---|---|---|---|
| 写入（缓存中没有内容，或缓存已过期） | 0 | `P` | `P + m` | `P × write + m × input` |
| 命中 | `P` | 0 | `m` | `P × read + m × input` |
| 在段 `k` 处部分命中 | 段 `k` 之前的段 | 从 `k` 起的段 | 已写入 + `m` | 已缓存 × 读取价 + 已写入 × 写入价 + `m` × 输入价 |

未使用缓存时，一次请求的成本为 `(P + m) × input`。成本统计比较的是整轮的总计：以内置故事为例，第一次写入的成本为 $0.1712，而不使用缓存时为 $0.1371（高 25%）；三次命中之后，本轮成本为 $0.2147，而不使用缓存时为 $0.5491（低 61%）。加速比仅对缓存命中的首个 token 取平均，并对比有缓存与无缓存两种情况：0.48s 对 1.42s，即 2.9x。写入和部分命中不计入其中。

### 你自己的前缀与请求

请按提示顺序在 `Segments` 中填入 `PromptCacheSegment` 项（一个 id、一个标签、一个 token 数，以及可选的 `Color`），并在 `Requests` 中最多填入四个 `PromptCacheRequest` 项（新消息的 token 数、相对于前一个请求的模拟 `Gap`（以秒计），以及 `Message`，后者会被朗读出来，并在结果中传递）。列表为空时，卡片使用内置故事。若要改为绑定，请设置 `SegmentsSource` 和 `RequestsSource`。

<Demo name="CodeReview" />

```xml
<arc:PromptCache Title="Review bot cache" Ttl="3600" ChangeSegmentId="style">
    <arc:PromptCache.Model>
        <arc:ExplainerModel Name="Quill 2 Mini" />
    </arc:PromptCache.Model>
    <arc:PromptCache.Pricing>
        <arc:PromptCachePricing InputPrice="1.20" CacheWritePrice="2.40" CacheReadPrice="0.12" />
    </arc:PromptCache.Pricing>
    <arc:PromptCache.Segments>
        <arc:PromptCacheSegment Id="repo" Label="Repository map" Tokens="7400" />
        <arc:PromptCacheSegment Id="style" Label="Style guide" Tokens="1800" />
        <arc:PromptCacheSegment Id="diff" Label="Open diff" Tokens="21500" />
    </arc:PromptCache.Segments>
    <arc:PromptCache.Requests>
        <arc:PromptCacheRequest Message="Flag any unchecked null returns in the parser" Tokens="140" />
        <arc:PromptCacheRequest Message="Suggest a clearer name for the retry helper" Tokens="90" Gap="64" />
        <arc:PromptCacheRequest Message="Does the new migration need a rollback step?" Tokens="210" Gap="118" />
    </arc:PromptCache.Requests>
</arc:PromptCache>
```

未设置颜色的段依次采用图表系列颜色 3、2、4、1，因此它们会跟随主题和强调色。更改数据会重建故事并从头开始，所以请只设置一次，而不要在每次刷新时都设置。

### 价格与首个 token

`Pricing` 接受一个 `PromptCachePricing`，价格以每百万 token 的美元数表示。默认值为示例价格，遵循一种常见模式：输入 $3.00，缓存写入为输入价的 1.25 倍（$3.75），读取为十分之一（$0.30）。当你的提供商对写入不额外收费时，请将 `CacheWritePrice` 设为与 `InputPrice` 相同。`Ttl` 是以模拟秒数计的存活时间（默认 300），它驱动圆环、倒计时和标题中的注释。

`FirstTokenEstimator` 根据已处理和已缓存的 token 数，返回到首个输出 token 的秒数。默认值是一个示意模型 `0.40 + processed / 45,000 + cached / 600,000`：固定开销、新 token 的预填充，以及加载已缓存 token 的成本低得多。当你有自己的测量数据时，请传入它们。

```csharp
cache.FirstTokenEstimator = (processed, cached) => 0.25 + processed / 60_000.0 + cached / 900_000.0;
```

### 修改开关

统计数据下方的开关用于编辑一个段，即 `ChangeSegmentId`（默认为 "rules"，即指令）。开关打开（`IsChangeArmed`）时，下一个请求只与缓存匹配到该段为止：该段之前的段被读取，一行警告文字标出断点，该段及其之后的段被重新处理并重新写入。该请求发送后，开关释放，下一个请求将命中被重写的缓存。修改第一段会在匹配之前就中断，因此它只是一次普通写入。将 `ChangeSegmentId` 设为 null（或设为没有任何段使用的 id），即可隐藏开关。

### 播放、单步与事件

`IsPlaying`、`Speed`（0 到 4；0 表示保持静止）、`Play()`、`Pause()` 和 `Restart()` 来自 `Explainer`。单步按钮（"Send the next request"，即 `StepForward()`）会暂停故事，并且只发送一个请求：它之前的暂停会被跳过，正在进行的请求会被完成；而在一轮结束后，它会开始新的一轮，其第一个请求若仍处于温热状态，则会命中缓存。重新开始会清空缓存并保持播放状态，因此处于暂停状态的卡片会停留在起点并保持暂停。

`RequestCompleted` 将每个完成的请求以 `PromptCacheResult` 的形式报告：包括其索引、模拟时间、类型、断点、已缓存、已写入和已处理的 token 数、有缓存与无缓存的成本、有缓存与无缓存的首个 token 时间、它是否经过修改或为冷写入，以及它的消息。`Expired` 会在每次缓存过期时触发。`Results` 保存本轮当前的结果，`CacheState` 保存缓存当前的状态。

<Demo name="EventLog" />

```csharp
cache.RequestCompleted += (_, e) =>
    log.Add($"{e.Result.Kind}: {e.Result.Cost:C4} instead of {e.Result.UncachedCost:C4}");
cache.Expired += (_, _) => log.Add("The cache expired.");
```

### 窄容器

布局跟随卡片的内容宽度，而不是窗口。宽度达到 660px 时，缓存面板与请求并排显示；低于此宽度时，它变为请求上方的一条横带，因此流动动画仍会从某一行一直延伸到缓存中。低于 480px 时，每个请求占三行（标题与状态、条形图，然后是位于共用标题下方的三个数字），统计数据纵向堆叠，开关横跨整张卡片。低于 260px 时，播放按钮移到标题下方。

<Demo name="Narrow" />

### 减弱动效

在减弱动效下，卡片打开时停在一轮已经结束的状态并等待。播放和单步仍然可用，但每个变化都立即生效：没有扫过效果、没有光带、没有飞行动画、没有滚动数字，也没有加速的消退。一轮结束后缓存过期，循环的卡片会稍后开始新的一轮。

<Demo name="ReducedMotion" />

### 内容指南

为各段命名时，请使用它们在提示中所包含内容的名称，并保持标签简短；标签会出现在图例、开关和说明文字中（"Edit the style guide"）。保持顺序符合实际：变化最少的内容放在最前面。将消息写成用户会提出的问题，因为它们会被朗读给屏幕阅读器。

| 不要写 | 请写 |
|---|---|
| Segment 1 | 工具模式 |
| System | 指令 |
| Big context | 知识库 |
| Question 3 | 比较表格中的两份运费报价 |
| $0.1 off | （让卡片计算成本） |

### 最佳实践

**建议**

- 使用你所描述的模型的 token 数和价格，使总计向读者说明关于该模型的真实情况。
- 除非你想在一轮中间展示一次冷写入，否则请让间隔短于存活时间。
- 用修改开关说明提示顺序的重要性：靠前的修改会重写几乎全部内容。
- 在卡片附近说明这些数字仅为示意。

**避免**

- 向卡片输入实时流量。它一次只模拟一轮；真实的测量数据应放在图表中。
- 超过四个请求：卡片只显示一轮中的前四个。
- 按计时器更改数据；每次更改都会使故事从头开始。

### 常见问题

**为什么第一个请求的成本比不使用缓存时更高？** 写入缓存的成本高于普通输入（按示例价格为 1.25 倍），因此第一次写入更昂贵。之后的命中会把这部分成本收回；在此之前，成本统计会显示 "higher"。

**为什么加速比不计入部分命中？** 它比较的是缓存命中与相同提示不使用缓存时的情况。部分命中会重新处理提示的大部分内容，若把它计入，就会掩盖命中所带来的效果。

**卡片能显示我的真实缓存吗？** 不能。使用 `RequestCompleted` 查看模拟计算的结果，真实数字请使用图表。

## API 参考

<!-- api: PromptCache -->

<!-- api: PromptCacheSegment -->

<!-- api: PromptCacheRequest -->

<!-- api: PromptCachePricing -->

<!-- api: PromptCacheResult -->

<!-- api: PromptCacheLabels -->

## 无障碍

- 卡片以其标题命名，并标明它是一个模拟（"Prompt cache, a simulation with sample prices"）。
- 图例、请求和缓存面板是有名称的分组。每个请求行读作一句话，包含其时间、消息、结果、从缓存读取的 token 数、已处理的 token 数、成本和首个 token 时间（运行之前为 "Request 1: not sent yet."）；条形图和滚动数字对辅助技术隐藏。
- 单步、开关、暂停和重新开始会通过礼貌式实时区域播报；故事自行播放时保持静默。
- 结果从不只依赖颜色：每种状态都同时配有图标和文字，已缓存的填充与已处理的填充在色调和轮廓上有所区别。
- 开关是切换按钮，并报告它是否已打开；图标按钮都有名称和提示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在播放或暂停、发送下一个请求、重新开始和修改开关之间移动 |
| `Enter` 或 `Space` | 激活已获得焦点的按钮：播放或暂停、发送请求、重新开始，或打开与释放修改 |

键盘焦点显示 2px 的焦点环；指针焦点不显示。

## 动效

每个故事动画都运行在讲解控件的时钟上，因此暂停会冻结全部动画（包括正在进行的扫过效果），而 `Speed` 会缩放所有动画。

| 变化 | 完整 | 减弱与关闭 |
|---|---|---|
| 处理 | 每个段依次以稳定的速度填满（完整条形图用时 1.1s），然后是消息，用时 120ms | 立即填满 |
| 缓存命中 | 缓存色调在 160ms 内淡入；光带在 520ms 内掠过前缀和面板条形图 | 色调立即出现，无光带 |
| 写入 | 已写入的部分在 460ms 内以强缓出效果飞入缓存面板，实心颜色淡化为色调 | 面板立即填满 |
| 部分命中 | 警告行在变化段之前的间隔中升起（200ms）；面板丢弃发生变化的部分 | 立即显示警告行 |
| 数字 | 在 420ms 内（缓出）从上一行滚动过来 | 立即变化 |
| 圆环 | 以短暂的延迟跟随其目标；最后一分钟转为警告色 | 直接到达目标 |
| 模拟间隔 | 在 0.55s 内以 S 形曲线推进 | 立即推进 |
| 空闲 | 存活时间约在 4.2s 内消退，然后缓存过期 | 暂停后过期 |
| 状态与说明文字 | 交叉淡化（淡入 160ms，淡出 100ms） | 短暂的交叉淡化 |

## 相关

| 组件 | 选择它的情形 |
|---|---|
| `SpeculativeDecoding` | 你要解释草稿模型如何加速生成 |
| `AgentLoop` | 你要解释智能体收集上下文、执行动作并进行检查的循环 |
| `DataFlow` | 你要解释事件如何在数据平台中流动 |
| `LineChart` / `BarChart` | 你要绘制真实的缓存命中率或真实成本随时间的变化 |
| `UsageMeter` | 你要展示真实配额的使用比例 |
