---
title: LLM cost calculator
summary: 面向 LLM 工作负载的价格计算器：设置每次请求的 token 数、每天的请求数与提示缓存，各模型按月度费用重新排名，条形平滑滑动，价格滚动变化。
source-hash: 6b4c782fba53
---

<Demo name="Preview" />

## 何时使用

`LlmCostCalculator` 在一个面板里给出某个 LLM 工作负载可信的月度估算。头部以大号滚动数字显示所选模型的月度总额。预设、三个滑块和一个 Cached input 开关设定工作负载，所有模型按月度费用从低到高排在一个列表中，每行有一根相对于最贵模型的条形。数值变化时，各行滑动到新的名次；选中某一行，总额、明细与价格行就切换为该模型。

- **价格页或设置页**：需要展示一个工作负载在多个模型上的费用。
- **为某项功能挑选模型**：让人看到费用如何随提示长度、输出长度、流量与缓存变化。
- **内部工具**：在迁移或预算评审前比较各家服务商的价格。

## 何时不用

- **在聊天或输入框里选择模型**，且不需要比较费用：使用模型菜单。
- **单个会话的实时 token 用量**：使用 `ContextWindow`。
- **按套餐计量的实际花费**：本组件只做估算，不做报告。

## 用法

传入要比较的模型及其标价（每 1M token 的美元价格），并在 `Description` 中说明价格来源与核对日期；没有 `Models` 时，面板显示空状态（"No models to compare"）：

```csharp
calculator.Models = priceList
    .Select(p => new LlmModelPrice(p.ApiId, p.Name, p.Provider, input: p.Input, output: p.Output, cachedInput: p.CachedInput))
    .ToList();
calculator.Description = $"List prices per 1M tokens, checked {priceList.CheckedOn:yyyy-MM-dd}";
calculator.Note = "List prices, not a quote. Calculated on this device, nothing was sent.";
```

本页的演示以各厂商的标价（标准档，核对于 2026-10-29）比较真实模型。这些是短提示的价格：Claude Haiku 5.5 对超过 100K token 的提示收取更高费用，GPT-6 系列为超过 272K，Grok 4.7 为达到 200K 起，且按整个请求计费；DeepSeek 的价格是其高峰时段费率（非高峰时段为一半），Gemini 3.8 Flash 的推广价持续到 2026 年底。工作负载预设是虚构的。

`CachedInput` 是缓存读取价格；不设置时，缓存的 token 按输入价格计费。`Mark` 接受模型的标志，即 24 × 24 的描边图形，以 16px 绘制；没有标志时，该行在浅色方块中显示服务商名称的首字母。

### 你的价格与工作负载

<Demo name="YourPrices" />

`Usage` 是工作负载，双向绑定：每次请求的输入与输出 token 数、每天的请求数，以及是否开启缓存。滑块的每一步、每个预设和每次切换缓存都会写入它并触发 `UsageChanged`。介于滑块刻度之间的值仍按精确值计算，滑块显示最近的刻度。`SelectedId` 选择模型（为 null 时选第二个模型；未知的 id 回退到最便宜的模型），通过指针或方向键选中某一行时，`SelectedModelChanged` 会带上该模型及其费用。`SelectedModel` 与 `SelectedCost` 始终是当前的选择。

`CacheHitRate` 是开启 Cached input 时按缓存价格计费的输入占比（默认 0.6，显示在开关旁边）。`DaysPerMonth` 设定一个计费月的天数（30）。

### 在服务器上得到相同的数字

`LlmCostCalculator.ComputeCost` 就是面板使用的算法，因此你可以把同样的估算与套餐一起保存，或写进发票：

```csharp
var cost = LlmCostCalculator.ComputeCost(model, new LlmUsage(16_000, 500, 5_000, Cached: true), cacheHitRate: 0.6);
// cost.Input, cost.CachedInput, cost.Output, cost.Total (dollars a month) and cost.PerRequest
```

### 预设

<Demo name="NoPresets" />

`Presets` 是分段控件那一行里的工作负载快捷方式（演示传入 Chat、RAG、Agent 和 Batch）。滑块与任何预设都不匹配时，Custom 分段会自动亮起。不设置预设或传入空列表时，这一行隐藏。

### 加载、错误与没有模型

`IsLoading` 显示与列表几何完全一致的占位行，并禁用各个控件。`ErrorText` 在列表的位置显示 "Prices did not load" 和你给出的原因；设置 `AllowsRetry` 会出现一个 Try again 按钮，它触发 `RetryRequested`。没有 `Models` 或其为空列表时显示空状态。列表区域在所有状态下保持同一高度，没有列表时各个数字显示为短横线。

```csharp
calculator.IsLoading = true;
try
{
    calculator.Models = await prices.FetchAsync();
    calculator.ErrorText = null;
}
catch (HttpRequestException ex)
{
    calculator.ErrorText = ex.Message;
}
finally
{
    calculator.IsLoading = false;
}
```

### 窄布局

<Demo name="Narrow" />

面板读取自身的宽度，而不是窗口的宽度。宽度低于 600px 时，滑块纵向堆叠。低于 520px 时，总额移到标题下方，每根条形单独占一行、位于模型名称下方（行高从 44px 增加到 58px），明细折成两列。月度费用从 $100,000 起改用紧凑数字（$420K、$4.2M），所以费用列不会溢出；总额保留全部位数。面板填满其容器，最宽 720px。

### 自定义文字

`Labels` 包含面板显示或朗读的每一个词；`Title`、`Description` 和 `Note` 可以覆盖大多数应用会修改的三段文字：

```csharp
calculator.Labels = new LlmCostCalculatorLabels
{
    Title = "Coût mensuel estimé",
    PerMonth = "/mois",
    Status = (name, cost, rank, count) => $"{name} coûte {cost} par mois, rang {rank} sur {count}.",
};
```

### 内容指南

- 价格是输入。传入当前的标价，并在 `Description` 中说明来源与日期。
- 厂商对长提示加价或在非高峰时段降价时，在 `Note` 中写明价格对应的档位。
- 预设标签保持为一个简短的词。

## API 参考

<!-- api: LlmCostCalculator -->

<!-- api: LlmModelPrice -->

<!-- api: LlmUsagePreset -->

<!-- api: LlmCostCalculatorLabels -->

## 无障碍

- 面板是一个以其标题命名的分组。模型列表是一个名为 "Models ranked by monthly cost, lowest first" 的列表，各行是单选按钮，只有所选模型是一个 Tab 停靠点。
- 无障碍树按名次列出各行，所以屏幕阅读器按显示的顺序朗读。每行的名称包含模型、服务商、名次和月度费用。
- 滑块是 Arc 滑块，朗读的是 token 数和请求数，而不是刻度序号。
- 一个礼貌型状态区域在数字静止 450ms 后（而不是拖动的每一步）播报所选模型的月度费用与名次。
- 选中状态以浅色底和强调色竖条显示，并报告为已选中，从不只依靠颜色。错误状态把图标与 "Prices did not load" 文字放在一起。
- 大号总额和滚动数字对辅助技术隐藏，因为状态区域和行名称已经包含这些数值。
- 指针获得焦点时不绘制焦点环；键盘焦点使用 Arc 共用的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过预设、三个滑块、Cached input 开关、所选模型行，错误状态下再到 Try again |
| 左方向键、右方向键 | 在预设上切换工作负载；在滑块上移动一个刻度 |
| Page Up、Page Down、Shift + 方向键 | 在滑块上移动两个刻度 |
| Home、End | 在滑块上跳到最小或最大的刻度；在模型列表中选中最便宜或最贵的模型 |
| 上方向键、下方向键 | 在模型列表中按名次选中上一个或下一个模型，焦点随之移动 |
| 空格 | 切换 Cached input |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 某行的名次变化 | 通过变换在临界阻尼弹簧上滑到新位置（约 240ms，无回弹），不重排 | 直接跳到 | 直接跳到 |
| 条形的占比变化 | 跟随同一弹簧 | 直接跳到 | 直接跳到 |
| 价格变化 | 各位数字在 240ms 内以 ease-out 曲线滚动到新值，使用等宽数字 | 直接跳到 | 直接跳到 |
| 选择变化 | 总额下方的模型名与价格行交叉淡化（淡出 100ms，淡入 160ms）；行的浅色底与强调色竖条在 160ms 内淡入淡出 | 短暂淡化 | 立即 |
| 加载、列表、错误与空状态 | 列表区域以固定高度在 160ms 内交叉淡化 | 短暂淡化 | 立即 |
| 加载 | 占位行呼吸闪烁 | 静止 | 静止 |

行、条形和数字在运动时是绘制出来的，不参与布局，所以一次让八行重新排名的拖动不会触发布局。面板空闲时不运行任何东西。

## 相关组件

- `ContextWindow`：单个会话的实时 token 用量。
- `AgentTimeline`：一次智能体运行的时间、token 与费用。
- `Slider` 与 `SegmentedControl`：面板的输入控件。
