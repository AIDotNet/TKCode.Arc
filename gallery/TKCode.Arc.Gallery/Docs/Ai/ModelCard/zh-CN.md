---
title: Model card
summary: 模型详情卡片：切换模型时，每个价格、上限和分数都在原位滚动到新值，用量滑块估算每月费用，基准测试条显示各模型的排名，比较开关以交叉淡化切换到与任一对手并排的表格。
source-hash: dd18090b77cf
---

<Demo name="Preview" />

## 何时使用

`ModelCard` 描述一个 AI 模型：它接受哪些输入、能容纳多少内容、费用多少，以及与相近对手相比得分如何。切换模型时，每个价格、上限和分数都在原位滚动到新值；用量滑块实时估算每月费用；比较开关以交叉淡化切换到与任一对手并排的表格。无论切换模型、视图还是状态，卡片尺寸都不变。

- **模型选择器的详情面板**：在有人选定之前，展示当前高亮的模型能做什么、费用多少。
- **文档或市场中的模型页面**：展示上限、价格，以及与相近对手的快速基准对比。
- **团队选择默认模型的设置页**：需要与备选模型并排核对。

## 何时不用

- **在许多模型中挑选**：渲染一个紧凑的选择器，并为高亮的模型显示一张 `ModelCard`；卡片是详情视图，不是列表行。
- **显示进行中的对话占用了多少上下文**：使用 `ContextWindow`。
- **套餐定价**：使用定价表；这张卡片显示的是按 token 计费的 API 价格。
- **包含许多模型的官方排行榜**：基准测试条只显示当前模型和两个对手。

## 用法

给卡片设置 `Model`、一组 `Rivals` 以及要显示的 `Benchmarks`。所有模型共用同一组对手即可：卡片会自动排除模型本身，因此数值滚动时，基准测试行和排名保持稳定。没有 `Model` 时，卡片保留其框架，显示“No model selected”，并把每个值显示为未列出。

```csharp
var model = new ModelCardModel
{
    Id = entry.ApiId,                 // from your model catalog
    Name = entry.Name,
    Provider = entry.ProviderName,
    Summary = entry.Summary,
    Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Tools,
    ContextWindow = entry.ContextWindow,
    MaxOutput = entry.MaxOutputTokens,
    KnowledgeCutoff = entry.KnowledgeCutoff,
    Pricing = new ModelCardPricing { Input = entry.Input, Output = entry.Output, CachedInput = entry.CachedInput },
    Scores = entry.Scores,            // benchmark id → score; leave out what the vendor does not publish
};

card.Benchmarks = [new ModelCardBenchmark { Id = "gpqa-diamond", Name = "GPQA Diamond", ShortName = "Science" }];
card.Rivals = catalog;                // every model, the current one included
card.Model = model;
card.Note = "List prices and vendor-published scores, checked 2026-10-29.";
```

`ContextWindow` 和 `MaxOutput` 是 token 数（1,000,000 显示为 1M）。`KnowledgeCutoff` 是 ISO 月份，例如 `"2026-03"`（显示为 Mar 2026），其他文本按原样显示。价格以 `Currency` 计，单位为每百万 token。缺少的值显示为“Not listed”。`Note` 是比较表下方的脚注：说明这些数据的来源和核对日期。数据仍是占位时打开 `IsSample`：基准测试说明末尾会加上“Sample scores”，脚注则说明价格和分数是示例。

本页的演示展示真实模型及其厂商标价（标准档，核对于 2026-10-29），并且只显示各厂商公布的基准测试分数；Gemini 4 Argon 的价格是推广价，Terminal-Bench 分数按各厂商自己选择的推理强度报告。每个 Terminal-Bench 版本都使用单独的基准测试 id，因此 4.0 的分数从不与 2.1 的分数一起排名；没有公布分数的模型显示为“Not published”（演示设置了 `Labels.None` 和 `Labels.NotListed`），而不是零。

### 结构

1. **头部**：模型标志放在着色的方块中（没有标志时显示名称首字母），旁边是名称和提供方，以及比较开关。
2. **简介**：一句话，预留两行（窄卡片预留三行），超出部分截断。
3. **模态标签**：全部七个（文本、图像、PDF、音频、视频、工具、推理）；不支持的标签为虚线边框并变淡。
4. **规格**：上下文窗口（带一个容量条和可容纳的页数，1M token 约为 1,500 页）、最大输出和知识截止时间。
5. **价格**（每百万 token）：输入、输出和缓存输入（只有当对手池中有模型提供缓存价格时才显示缓存单元格）。
6. **费用估算**：每月用量滑块（1M 到 1B token）和估算费用，按 `InputShare` 计费。
7. **基准测试**：切换器、说明文字、模型及其前两个对手的条形，以及带排名的读数。
8. **底部**：API 模型 id、Copy model id 按钮，以及用于选择器的 Use model 按钮。

### 比较

<Demo name="Compare" />

比较开关或 `View="Compare"` 以交叉淡化切换到与一个对手对比的表格：每项规格、价格、所选用量下的费用和每个基准测试各占一行。较优的值以更粗的字重显示，并带有对勾、供屏幕阅读器朗读的“better”以及领先幅度（+2.3、−20%、2x）。表格下方的统计说明谁领先。从菜单中选择对手，或把 `CompareWith` 设为对手的 id。

### 加载与错误

<Demo name="States" />

`IsLoading` 显示一个脉动的骨架屏，禁用比较开关，并播报“Loading model details”。`ErrorText` 用消息替换卡片主体；`CanRetry` 添加一个 Retry 按钮，它会触发 `RetryRequested`。两种状态都保留头部和卡片尺寸。没有 `Model` 时（Empty），头部显示“No model selected”，比较开关和基准测试隐藏，Copy model id 被禁用。

### 用在模型选择器中

<Demo name="Picker" />

`CanSelect` 添加一个主要的 Use model 按钮，点击时以该模型触发 `SelectRequested`；当它是当前选择时设置 `IsSelected`，按钮会显示 Selected 和对勾。`SelectLabel` 用于重命名该按钮。

```csharp
card.SelectRequested += (_, e) => settings.DefaultModel = e.Model.Id;
card.IsSelected = card.Model?.Id == settings.DefaultModel;
```

### 窄卡片

<Demo name="Narrow" />

卡片在 640px 以内填满容器，并读取自身宽度。低于 480px 时，规格和价格变为“标签与值”的行，费用估算移到滑块下方，基准测试切换器移到标题下方，底部按钮移到模型 id 下方。低于 420px 时，简介预留三行；低于 360px 时，每个条形移到名称下方。宽度达到 520px 时，比较表格带有标签列并显示领先幅度和迷你条形；更窄时，每个标签位于其两个值的上方。

### 事件

```csharp
card.ViewChanged += (_, e) => Save("view", e.View);
card.CompareWithChanged += (_, e) => Save("rival", e.Id);
card.BenchmarkChanged += (_, e) => Save("benchmark", e.Id);
card.MonthlyTokensChanged += (_, e) => Save("volume", e.Tokens);
card.IdCopied += (_, e) => Log("copied", e.Id);
```

`View`、`CompareWith`、`Benchmark` 和 `MonthlyTokens` 都是双向的，也可以直接绑定它们。`MonthlyTokens` 会吸附到最近的档位：1M、5M、10M、25M、50M、100M、250M、500M 或 1B。

### 你自己的措辞

`Labels` 包含卡片显示或朗读的每个词；带数值的句子是函数：

```csharp
card.Labels = new ModelCardLabels
{
    Compare = "Vergleichen",
    Rank = (rank, count) => $"Platz {rank} von {count}",
};
```

### 减弱动效

在减弱动效下，视图、条形、数字和值都在原位直接变化，骨架屏保持静止。关闭动效时，所有变化立即完成。

### 内容准则

- 简介保持一句话，约 90 个字符。
- 为模型和对手提供相同基准测试 id 的分数。没有分数的行显示 None，比较表格显示 Not listed，因此行不会出现或消失。
- 数据仍是占位时打开 `IsSample`；使用真实价格和分数时保持关闭，并在 `Note` 中说明数据来源。
- 只对同一版本的基准测试分数排名：每个版本使用自己的 id。
- 标志只起装饰作用；可以把任意 24 × 24 的描边图形作为 `Mark` 传入并设置 `MarkColor`，或省略它以显示首字母。

### 常见问题

**为什么隐藏比较视图时卡片不会变小？** 所有面板都叠放在同一个单元格中，所以卡片始终与最高的面板一样高。切换不会移动周围的内容。

**每月费用如何估算？** 用量中 `InputShare` 的部分按输入价格计费，其余按输出价格计费（默认 75% 为输入）。

## API 参考

<!-- api: ModelCard -->

<!-- api: ModelCardModel -->

<!-- api: ModelCardPricing -->

<!-- api: ModelCardBenchmark -->

<!-- api: ModelCardLabels -->

<!-- api: ModelCardViewChangedEventArgs -->

<!-- api: ModelCardIdEventArgs -->

<!-- api: ModelCardMonthlyTokensEventArgs -->

<!-- api: ModelCardModelEventArgs -->

## 无障碍

- 卡片以模型名称命名。每项规格和价格都会连同其标签一起朗读。
- 比较表格的每一行以其标签命名；较优的值会读出“better”，因此对比不依赖颜色。
- 切换视图、对手或基准测试时，一个礼貌的状态区域会说明发生了什么变化（“Comparing Claude Opus 5.5 with Gemini 4 Argon”）。Copy model id 会自行播报 Copied。
- 基准测试条对辅助技术隐藏；每一行都可以获得焦点，并以完整的读数（分数、排名以及与领先者的差距）命名。标志只起装饰作用。
- 模态标签会读出支持或不支持；不支持的标签既变淡又使用虚线边框。
- 加载时比较开关被禁用，状态区域播报“Loading model details”。错误会立即播报。
- 隐藏的面板不接收指针输入，Tab 也不会进入其中。
- 指针获得焦点时不显示焦点环；键盘获得焦点时显示 Arc 的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过比较开关、用量滑块、基准测试切换器和每个基准测试行（获得焦点时显示其读数），在比较视图中则经过对手菜单，然后是 Copy model id 和 Use model |
| 方向键、Page Up、Page Down、Home、End | 在用量滑块上逐档切换每月用量；估算费用滚动到新值 |
| Space、Enter | 切换比较、复制模型 id 或选择模型 |
| 左方向键、右方向键 | 在切换器中的基准测试之间移动 |
| Enter、下方向键 | 打开对手菜单；方向键在对手之间移动，Enter 选择，Escape 关闭 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 详情、比较、加载、错误 | 旧面板在 100ms 内淡出，同时新面板在 160ms 内淡入 | 立即 | 立即 |
| 切换模型：价格、上限、费用、页数、分数 | 数字以等宽数字滚动到新值 | 立即 | 立即 |
| 切换模型：名称、简介、截止时间、读数 | 原位交叉淡化（淡出 100ms，淡入 160ms） | 立即 | 立即 |
| 条形与上下文容量条 | 以临界阻尼弹簧滑动（0.24s，无回弹） | 立即 | 立即 |
| 首次出现的条形 | 卡片大部分进入视口时从零生长一次，间隔 40ms | 立即 | 立即 |
| 悬停或聚焦基准测试行 | 该行着色，其他行淡到一半，读数交叉淡化（160ms） | 立即 | 立即 |
| 加载 | 骨架屏脉动 | 静止 | 静止 |

每个位置都已预留：两行简介、七个标签、每个规格和价格单元格、三个基准测试行，以及保持行数的读数，因此卡片尺寸始终不变。

## 相关组件

- `ContextWindow`：显示进行中的对话占用了多少上下文。
- `CopyButton`、`Switch`、`Slider` 和 `SegmentedControl`：卡片内部使用的控件。
- `DropdownMenu`：对手菜单。
