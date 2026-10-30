---
title: Context window
summary: 模型的上下文窗口呈现为一张由 200 个格子组成的网格，分段填充，并附带压缩阈值、图例，以及说明还剩多少空间的操作按钮。
source-hash: 9f00beff2aa3
---

<Demo name="Preview" />

## 何时使用

`ContextWindow` 展示语言模型的上下文窗口中装入了什么，以及还剩多少空间。窗口是固定的 200 个格子的预算，因此每个格子是它的两百分之一。格子按阅读顺序填充：系统提示、工具定义、文件与记忆、较早消息的摘要和对话，然后是空闲空间；留给回复的部分则以描边格子的形式画在最后。一条虚线标出压缩开始的位置。网格上方是一个大号数字，表示已用的 token 数；旁边的图例列出每个部分的 token 数和占比。Send message、Attach file 和 Compact now 会改变窗口，其下的状态行说明发生了什么。

- **开发者工具与智能体控制台**：在对话记录旁显示会话的预算，让人看清一次长时间运行为何开始遗忘内容，以及一个大附件会占用多少空间。
- **会压缩长会话的编码智能体**：让压缩可见。较早的消息折叠成一段简短摘要，格子随之释放，状态行说明折叠了多少内容。
- **上下文中的模型选择**：切换到窗口更小的模型时，网格会立即重新排列，并显示会话是否仍能放下；放不下时，卡片会折叠对话并说明这一点。
- **文档与入门指南**：用一段可以随意操作的会话来解释上下文窗口、输出预留和压缩，就像本页的预览那样。

## 何时不用

- **简单的配额或套餐额度**：使用 `Progress` 或用量仪表。网格关心的是窗口内部装了什么，而不是套餐用掉了多少。
- **选择模型**：使用模型选择器。这里的胶囊只在你传入的窗口之间切换。
- **账单或随时间变化的花费**：使用图表。卡片只展示某一会话在某一时刻的状态。
- **同时展示许多会话**：每行带一个小条的表格更易读。卡片的设计用途是每屏一张。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContextWindow>
        <arc:ContextWindow.Models>
            <arc:ContextWindowModel Id="your-model" Name="Your model" Window="200000" Reserve="16000" />
        </arc:ContextWindow.Models>
        <arc:ContextWindowItem Category="System" Label="System prompt" Tokens="3800" />
        <arc:ContextWindowItem Category="Messages" Label="Plan the importer rewrite" Tokens="9400" />
    </arc:ContextWindow>
</UserControl>
```

`ContextWindow` 是 Special 包中的 `TemplatedControl`。请在 `UseArcControls()` 旁调用 `UseArcSpecial()` 以注册该包。卡片最宽为 744px，宽度跟随它自身，而不是窗口的宽度。

没有数据时（没有模型、项目、轮次或文件），卡片是空的：没有模型胶囊，所有格子都空闲，也没有 Send message 和 Attach file。请提供你的模型和项目，并为这两个按钮提供 `Turns` 和 `Files`。本页的演示使用真实模型及其厂商公布的上下文窗口（核对于 2026-10-29：Claude Sonnet 5.5 为 1M，Grok 4.7 为 500K，Mistral Medium 3.5 为 256K），以及一段虚构的、用量为 436K token 的编码会话；输出预留是演示应用单次回复所请求的量（32K），绝不超过该模型公布的输出上限。

### 结构

1. **读数**：已用 token 数以大号数字显示，基线处为 "of 1M tokens"，其下是已用百分比（整数，窗口溢出时会超过 100）、"used"、一个圆点，以及空闲 token 数。
2. **模型胶囊**：模型的标志、名称、以弱化数字显示的窗口大小，以及一个箭头。点击会打开模型菜单；被选中的模型带有勾选标记。没有模型时胶囊隐藏。
3. **网格**：20 列 × 10 行的正方形格子，顺序为系统提示、工具、文件与记忆、摘要、对话、空闲空间，以及输出预留。阈值线之后的空闲格子颜色略浅。
4. **阈值线**：一条虚线，位于最接近阈值的行边界上（80% 时在第 8 行之后）。
5. **说明文字**：左侧为 "Each cell is 5K tokens"，右侧为一段虚线样本和 "Compacts at 80%"。
6. **图例**：七行（System prompt、Tools、Files & memory、Summary、Conversation、Free、Output reserve），每行包含色块、token 数及其在窗口中的占比。空行显示 "No summary yet" 或 "0"（弱化文字），且不显示占比。一条较粗的分隔线把已用部分与剩余部分分开。
7. **操作**：Send message（主要）、Attach file（次要）和 Compact now（幽灵按钮，在还有空间时位于右侧）。没有可发送或可附加的内容时，Send message 和 Attach file 会隐藏。
8. **状态行**：一个图标加一句话：普通消息用信息圆圈，接近或超过阈值时用警告三角，消息折叠期间用收缩图标，压缩成功时用对勾圆圈。

### 格子如何计数

窗口被分为 200 个格子，因此一个格子代表窗口的 1/200：在 1M 窗口上是 5K，在 512K 上是 2.6K，在 400K 上是 2K。各部分通过累积舍入来布局：卡片按顺序累加各部分，并把每个累计值舍入为整数个格子（半数向上舍入），每个部分占据两个相邻边界之间的格子。对累计值舍入能让网格保持准确：即使每个部分都很小，已用格子数也总与已用总量一致。预留部分单独舍入，并从最后一个格子向前计算。窗口溢出时，各部分在预留处停止，排在后面的部分不再获得格子；预留始终会被画出。

token 数的显示规则：低于 1,000 时显示 "996"，低于 10K 时保留一位小数且不带末尾的 ".0"（因此是 "4.1K"，而 "2K" 不写作 "2.0K"），低于一百万时显示 "509K"，超过时显示 "1M" 或 "1.2M"。占比低于 10% 时保留一位小数，如 "0.4%"（"1.0%" 也保留小数），10% 及以上显示 "29%"。每一行单独舍入，因此各行加起来可能是 99.8% 或 100.1%。

### 发送、附加与压缩

Send message 会把 `Turns` 中的下一条作为消息添加到窗口末尾；Attach file 会把 `Files` 中的下一个添加到文件与记忆的末尾，摘要和对话随之后移。两者在用完后会从头开始。状态行会写明添加了什么，以及它占用了多少 token。

在阈值的 85% 处（默认 80% 时即窗口的 68%），发送或附加会发出警告：状态行变为警告，并提示即将压缩。达到阈值时，卡片会说明已超过阈值，并在 540ms 后自动折叠。切换到一个会话已超出其窗口的模型时，卡片会立即发出警告，并在 600ms 后折叠。

折叠耗时 700ms。折叠期间，所有操作和模型胶囊都不可用，对话从最旧的消息到最新的消息逐渐变暗。随后，除最新两条之外的所有消息合并为一段摘要（约为被折叠 token 的 6%，至少 4K，至多 24K），摘要落在文件之后，它释放出的格子重新变为空闲空间。之后的压缩会累加到同一段摘要上，摘要本身从不被折叠。当消息数多于最新两条时，Compact now 会按需执行相同的操作。

| 时刻 | 状态 | 时间 |
|---|---|---|
| 超过阈值时发送 | Added “Why does the nightly sync time out?” (+35K) · past 80% | 0 |
| 开始折叠 | Past 80%: folding 35 earlier messages… | 540ms |
| 摘要生成 | Folded 608K into a 24K summary | 1,240ms |
| 切换到较小的模型 | 436K is over 80% of Grok 4.7 | 0 |
| 开始折叠 | Too full for Grok 4.7: folding 22 earlier messages… | 600ms |

文件永远不会被折叠。窗口超过阈值且只剩最新的消息时，状态行会说明暂时没有可折叠的内容；压缩后窗口仍超过阈值时，状态行会说明折叠了多少，并建议移除文件。已用 token 可以超过窗口：此时读数会显示超过 100%，且没有空闲 token。

### 你自己的会话

传入你的模型、项目、轮次和文件。每个 `ContextWindowModel` 都有窗口和预留（以 token 计）、一个名称，以及一个可选的标志（24 × 24 的描边图标，例如 `ArcIcons` 的成员）和它的颜色。每个 `ContextWindowItem` 都有类别、标签和 token 数；网格按类别分组，因此顺序只在同一类别内部有意义。

<Demo name="YourSession" />

```xml
<arc:ContextWindow>
    <arc:ContextWindow.Models>
        <arc:ContextWindowModel Id="heron-2" Name="Heron 2" Window="256000" Reserve="16000" />
    </arc:ContextWindow.Models>
    <arc:ContextWindowItem Category="System" Label="Style guide and instructions" Tokens="2400" />
    <arc:ContextWindowItem Category="Files" Label="handbook/billing.md" Tokens="14600" />
    <arc:ContextWindowItem Category="Messages" Label="Outline the refunds page" Tokens="8200" />
</arc:ContextWindow>
```

```csharp
card.ItemsChanged += (_, e) => log.Add($"{e.Change}: {e.Items.Count} items");
card.Compacted += (_, e) => log.Add($"Folded {e.FoldedCount} messages into {e.SummaryTokens} tokens ({e.Reason})");
```

卡片会随操作改写 `Items`：发送或附加会插入一个项目，压缩会移除被折叠的消息并累加到摘要中。`ItemsChanged` 会在每次此类改动后给出新的项目列表及引起变化的原因，`Compacted` 报告被折叠的内容、生成的摘要，以及压缩前后的 token 数，`ModelChanged` 则跟随胶囊的变化。`SelectedModel` 是双向的。`UsedTokens`、`FreeTokens`、`CanCompact` 和 `Phase` 为只读，并可绑定。

### 从你的应用驱动

在真实会话中，项目来自你的应用。将 `Turns` 和 `Files` 留空即可隐藏 Send message 和 Attach file，并使用 `AddItem` 添加真实项目：它会把项目插入到其类别所属的位置，并执行与内置操作相同的波次、状态和阈值检查。你对 `Items` 或某个项目的 `Tokens` 所做的修改会立即显示，但保持安静：不显示状态行，也不会自动压缩。

<Demo name="HostDriven" />

```csharp
// The summary your summarizer will write: here 5% of the folded tokens, at least 2,000.
card.Summarizer = folded => Math.Max(2_000, folded.Sum(item => item.Tokens) / 20);
card.AddItem(new ContextWindowItem(ContextWindowCategory.Messages, "Summarize the open tickets", 9_400));
```

`Summarizer` 返回被折叠消息的摘要大小；未设置时，卡片使用这些消息的 6%，并限定在 4K 到 24K 之间。若要执行你自己的摘要，请使用 `Compacted`。`AutoCompact` 会关闭自动折叠：状态行仍会说明何时超过阈值，且 Compact now 保持可用。`KeepRecentMessages` 设置压缩后保留的最新消息数（默认两条）。

### 不同的阈值

`Threshold` 是触发压缩的窗口占比（默认 0.8）。虚线位于最接近它的行边界上，因此 0.6 会在第 6 行之后画线，0.75 则在第 8 行之后，而检查本身使用精确的数值。警告从阈值的 85% 开始。

<Demo name="Threshold" />

### 窄容器

布局跟随卡片内容的宽度，即卡片宽度减去两侧各 24px 的内边距。内容宽度不超过 640px 时，图例移到网格下方，格子间距收窄（2px，圆角 2px，而不是 3px），图例占满整行。内容宽度不超过 420px 时，模型胶囊移到读数下方。操作按钮会换行；当 Compact now 无法与其他按钮并排放下时，它会移到新的一行开头，状态行也会换行。

<Demo name="Narrow" />

### 减弱动效

在减弱动效下，格子和数字会立即变化，状态行直接替换而不做淡入淡出，对话不会变暗，压缩也立即完成。每次状态变化仍会按顺序被朗读。

<Demo name="ReducedMotion" />

### 内容准则

用你的产品中的实际名称来称呼各个部分，并保持状态语句简短：每次变化都会被朗读。项目标签只在状态行中显示，消息会加引号，因此请使用几个词。

| 避免 | 改为 |
|---|---|
| User message #42 | Outline the refunds page |
| /Users/me/projects/docs/handbook/billing.md | handbook/billing.md |
| Context compaction has been completed successfully | Folded 279K into a 17K summary |
| Model gpt-like-large-2025 | Heron 2 |

`Labels` 接受一个 `ContextWindowLabels` 对象，其中包含卡片显示或播报的每一个文字，包括图例名称和状态语句。包含数字的句子是函数，因此其他语言中可以调整词序：

```csharp
card.Labels = new ContextWindowLabels
{
    Messages = "Chat",
    Folded = (folded, summary) => $"{folded} folded into a {summary} summary"
};
```

### 最佳实践

**建议**

- 使用会话的真实用量来填充：系统提示、每个工具定义、附加的文件与记忆、摘要，以及每个轮次连同它所产生的工具结果。
- 将预留设为你请求的最大输出，使网格显示回复所需的空间。
- 一旦你的摘要生成器运行过，就传入报告真实摘要大小的 `Summarizer`。
- 在菜单中保留两到三个模型，并使用通用字形作为标志，而不是厂商标志。

**避免**

- 用计时器重建 `Items`。每次变化都会产生动画；应只更新发生变化的项目。
- 把虚构的会话当作真实用量来展示。
- 把网格用于一个窗口以外的任何事情。它始终有 200 个格子；它的比例只对一个预算有意义。

### 常见问题

**为什么有时小的部分没有格子？** 格子由累计值计算得出，因此小于半个格子的部分可能舍入为零，而总量仍然准确。图例始终显示它的 token 数。

**为什么摘要不会缩小？** 压缩会把摘要加到已有的摘要上，而摘要从不被折叠。当你的应用重写摘要时，请在 `Items` 中移除或替换摘要项目。

**切换到更大的模型会怎样？** 网格重新排列，状态行写明新模型和窗口，而且不会有任何折叠。

**为什么没有 Send message 和 Attach file？** 当 `Turns` 或 `Files` 为空时它们会隐藏。

**窗口能溢出吗？** 能。读数会超过 100%，空闲空间显示 0，无法放在预留之前的部分不会获得格子。

## API 参考

<!-- api: ContextWindow -->

<!-- api: ContextWindowModel -->

<!-- api: ContextWindowItem -->

<!-- api: ContextWindowTurn -->

<!-- api: ContextWindowLabels -->

<!-- api: ContextWindowCompactedEventArgs -->

<!-- api: ContextWindowModelChangedEventArgs -->

<!-- api: ContextWindowItemsChangedEventArgs -->

枚举如下：

| 类型 | 取值 |
|---|---|
| `ContextWindowCategory` | `System`, `Tools`, `Files`, `Summary`, `Messages` |
| `ContextWindowPhase` | `Idle`, `Summarizing` |
| `ContextWindowCompactReason` | `Manual`, `Threshold`, `ModelSwitch` |
| `ContextWindowChange` | `Sent`, `Attached`, `Added`, `Compacted` |

## 无障碍

- 卡片是一个名为 "Context window" 的分组。网格是一张图片，其名称完整描述了它：模型、已用 token 数与窗口的对比、每个部分及其 token 数，以及阈值。它随每次变化更新，而不是在滚动的每一帧更新。
- 读数会被朗读为一行（"436K of 1M tokens, 44% used, 532K free"）。图例是一个名为 "Context breakdown" 的列表，其各行会朗读名称、token 数和占比，因此不依赖颜色。
- 胶囊以模型及其窗口命名，并说明它会切换模型。菜单中的模型是单选项，被选中的那一项带有勾选标记。
- 每次发送、附加、切换模型和压缩都会通过礼貌级别的实时区域播报，并且每种状态都同时带有图标和文字。
- 消息折叠期间，所有操作都不可用。为了不让键盘焦点落回页面，卡片会把焦点保留在一个隐藏的占位元素上，该元素以状态命名，并在压缩完成时把焦点交还：交给原先持有焦点的操作，若该操作已不可用，则交给 Send message。
- 键盘焦点显示 2px 的焦点环；指针焦点则不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在模型胶囊、Send message、Attach file 和 Compact now 之间移动 |
| 胶囊上的 `Enter` / `Space` | 打开模型菜单，并定位到当前选中的模型 |
| 胶囊上的 `Up` / `Down` | 打开模型菜单 |
| 菜单中的 `Up` / `Down` | 在各模型之间移动，并首尾循环 |
| 菜单中的 `Enter` / `Space` | 选择模型，关闭菜单并回到胶囊 |
| 菜单中的 `Escape` | 关闭菜单并回到胶囊 |
| 操作上的 `Enter` / `Space` | 发送、附加或压缩 |

## 动效

- 新的 token 会在一段短暂的波次中为其格子重新上色，从第一个变化的格子开始：在末尾添加消息时每个格子 9ms，在中间插入文件时以及压缩释放的格子每个 3ms，且总时长不超过 360ms。每个格子在 240ms 内完成变化，使用标准曲线，并在 OKLab 中混合其颜色。
- 切换模型时，所有变化的格子在一次扫描中从第一个格子开始重新上色，每个格子 1.4ms。
- 消息折叠期间，对话从最旧的消息到最新的消息逐渐变暗（每个格子在 720ms 内降到 0.35 再回到 0.5，相邻格子至多间隔 6ms，总计不超过 420ms）。摘要生成后，格子立即恢复完全不透明，并以 3ms 的波次重新上色。
- 大号数字、百分比以及图例中的 token 数和占比会在 400ms 内以缓出曲线滚动到新值，起点是它们在那一刻显示的数值，因此快速连续发送时不会倒退跳动。空闲 token、窗口大小和说明文字立即变化。
- 状态行原地交叉淡化：旧行在 100ms 内淡出，新行在 160ms 内淡入并上移 3px。
- 指向图例的某一行时，该行的格子会点亮，其他格子淡化至 0.28，其他行淡化至 0.45，耗时 160ms。
- 模型菜单以 CSS 弹簧效果从胶囊处展开，下拉箭头随之翻转。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 格子 | 以波次重新上色 | 立即变化 | 立即变化 |
| 数字 | 在 400ms 内滚动 | 立即变化 | 立即变化 |
| 状态行 | 交叉淡化 | 直接替换 | 直接替换 |
| 折叠 | 变暗 700ms，然后完成 | 立即完成 | 立即完成 |
| 自动压缩 | 540ms 或 600ms 后 | 立即 | 立即 |
| 图例悬停 | 160ms 内淡入 | 立即 | 立即 |

## 相关组件

- `Progress`：用于带标签的单一额度。
- `PromptCache`：说明共享的提示前缀如何在多次请求之间复用。
- `AgentLoop`：说明智能体如何逐步收集上下文。

| 组件 | 何时选用 |
|---|---|
| `Progress` | 一个数字对一个上限就够了 |
| `PromptCache` | 你讲解的是缓存，而不是预算 |
| `AgentLoop` | 你讲解的是智能体如何工作，而不是什么填满了它的窗口 |
