---
title: 用量定价
summary: 一个为你挑选套餐的定价计算器：拖动席位数与每月流量，套餐名称会变形，估算金额会计数到新总额，行动按钮随之变化。
source-hash: c9c11477274e
---

<Demo name="Preview" />

## 何时使用

`UsagePricing` 是按席位计费、并在其上叠加计量部分的产品定价页中的"我要付多少"区块。人们只需设定两个已知的数字：多少席位，以及大致的每月用量。区块会完成其余工作：挑出合适的套餐，用一行话说明原因，显示每月估算并加以拆解。行动按钮始终写明推荐的套餐。

- **席位加用量定价。** API 平台、分析、消息和存储：每个席位一个价格，每个套餐包含一定额度的用量，超出部分按固定费率计费。
- **按上限划分的套餐。** 每个套餐推荐适用于一定数量的席位和一定额度的用量。区块会挑出第一个能同时涵盖两个值的套餐，因此团队在超过任一上限时，都会看到下一个套餐。
- **月付与年付。** 切换开关会显示年付为席位节省的金额，在明细中加入折扣行，并把估算滚动更新。

## 何时不用

- **并排比较的固定档位。** 如果人们需要逐项比较各套餐，请使用 Plan comparison 区块。
- **已有客户的用量预测。** 已经付费的客户希望预测自己的用量时，请使用用量预测（usage forecast），或计费页面上的 `UsageBilling`。
- **超过两个变量的定价。** 区块恰好有两个滑块。附加项、地区或支持等级应放在配置器或结账页中。
- **寻找最便宜的套餐。** 推荐遵循各套餐的上限，而不是最低总价。如果你的套餐有重叠，使较大的套餐反而更便宜，请相应设置上限，或改用比较表展示套餐。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。不做任何配置时，它显示示例：三个套餐（Spark、Studio 和 Fleet）、23 个席位刻度和 27 个每月调用次数刻度，以美元计价。

```xml
<arc:UsagePricing SeatIndex="10" UsageIndex="14" BillingPeriod="Monthly" />
```

你自己的套餐放在 `Plans` 中，从小到大排列。每个 `UsagePricingPlan` 都有席位价格、它所包含的用量、超出该用量后每个 `OverageUnit` 的价格、每个席位的年付折扣、推荐所覆盖的上限以及它的功能。最后一个套餐通常没有上限。

```xml
<arc:UsagePricing Currency="EUR">
    <arc:UsagePricingPlan Name="Solo" SeatPrice="9" YearlySeatDiscount="1"
                          IncludedUsage="1000000" OveragePrice="6" MaxSeats="3" MaxUsage="2000000">
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="7 days" />
    </arc:UsagePricingPlan>
    <arc:UsagePricingPlan Name="Team" SeatPrice="19" YearlySeatDiscount="3"
                          IncludedUsage="10000000" OveragePrice="3">
        <arc:UsagePricingFeature Key="sso" Label="Single sign-on" />
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="90 days" />
    </arc:UsagePricingPlan>
</arc:UsagePricing>
```

滑块沿 `SeatStops` 和 `UsageStops` 逐步移动。刻度沿轨道均匀分布，无论其数值为多少，因此每一步按固定倍数增长的列表会带来对数般的手感。`SeatIndex` 和 `UsageIndex` 是选中的刻度，双向绑定；`Seats` 和 `Usage` 读取对应的数值。

### 结构

1. **标题与说明**：`Labels.Title` 和 `Labels.Description`。
2. **席位滑块与用量滑块**：Arc 的 `Slider`，数值位于右上角；拖动或聚焦时，滑块拇指上会出现气泡；每个第一个套餐之后的套餐，在其起点处都有一个带标签的刻度。
3. **计费开关**：一个 `SegmentedControl`，包含 Monthly 与 Annual，旁边是年付节省的金额。年付生效后，节省金额前会长出一个勾，文字变为绿色。
4. **明细**：席位行（"Studio seats"，16 × $26）、用量行（包含在内，或超出的用量及其费率）、按年付费时显示的年付折扣行，以及预估总额。
5. **推荐卡片**：一个计量条（每个套餐一段）、套餐名称、理由、每月估算、计费说明、功能列表，以及试用按钮及其说明。

### 推荐与理由

推荐的套餐是第一个 `MaxSeats` 与 `MaxUsage` 都能覆盖所选值的套餐（上限为 null 时覆盖一切）。理由行将所选值与上一个套餐的上限相比较，共有四种形式：

| 理由 | 何时 | 默认文字 |
|---|---|---|
| `WithinLimits` | 第一个套餐 | "Fits up to 4 seats and 1.5M calls a month" |
| `SeatsAbove` | 只有席位超过上一个套餐 | "Suggested beyond 36 seats" |
| `UsageAbove` | 只有用量超过上一个套餐 | "Suggested beyond 20M calls a month" |
| `BothAbove` | 席位和用量都超过上一个套餐 | "Suggested beyond 4 seats and 1.5M calls" |

理由可以在套餐不变的情况下变化，例如在 Studio 内从 4 个席位变为 5 个；此时只替换句子。`RecommendationChanged` 对两种变化都会引发。

### 估算

| 数值 | 公式 |
|---|---|
| 席位行 | 席位数 × 席位价格 |
| 用量行 | max(0, 用量 − 包含量) ÷ `OverageUnit` × 超额价格；没有超出时为 "Bundled" |
| 年付折扣 | 席位数 × 每个席位的年付折扣（仅在按年付费时显示并扣减） |
| 预估总额与卡片价格 | 席位行 + 用量行 − 年付折扣 |
| 年付节省 | 席位数 × 每个席位的年付折扣 × 12（仅计席位，用量不计入） |
| 年付计费说明 | 席位数 × (席位价格 − 折扣) × 12 |

金额为整数，使用 `Labels.Culture` 的文化区域和 `Currency` 的货币；负数带有真正的减号（"−$80"）。

### 自有数据与事件

<Demo name="Live" />

该示例为一个存储产品按编辑者和每 100 GB 定价（`OverageUnit = 100`，`UsageFormatter` 写出 GB 与 TB），替换了文字，并通过一个会拒绝最大套餐的模拟注册流程开始试用。`SelectionChanged`、`RecommendationChanged`、`TrialStarted`、`TrialUndone` 和 `TrialFailed` 都是带类型参数的路由事件。

### 试用

按钮上显示 "Try {plan}"。按下后 `IsTrialStarted` 开启：按钮变为次要样式并带一个勾，显示 "Trial is on"，其下方的说明变为 "Trial is on. Undo"。试用归属于开启时所在的套餐：在该套餐内移动滑块会保留试用，而推荐的套餐变化则会将其关闭。撤销会关闭试用，并把键盘焦点交还给按钮。

将 `TrialHandler` 设为调用你的注册流程。任务运行期间按钮显示旋转指示；任务完成时试用开启，抛出异常时试用保持关闭（并引发 `TrialFailed`）。

```csharp
pricing.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Key, request.Seats, request.Usage, cancellation);
```

`StartTrialAsync()` 与 `UndoTrial()` 在代码中执行相同的操作。

### 窄容器

<Demo name="Narrow" />

宽度达到 780px 时，卡片在右侧占据自己的 392px 列。更窄时，标题会缩小，卡片在控件与明细之间移动。

### 货币、文化与文字

<Demo name="Localized" />

每句文字都是 `UsagePricingLabels` 的一个条目。带有滚动金额或变形套餐名称的句子（"{amount} saved per year"、"{plan} seats"、"{amount} per year for seats, calls monthly"）可以把该值放在任何位置：区块会把句子拆开，在其间插入该值。

### 减弱动效

<Demo name="ReducedMotion" />

### 内容指南

- 用简短的词为套餐命名；名称会出现在卡片、计量条、席位行和按钮上。
- 理由句要足够短，在 392px 宽度下能放在一行内。
- 列出在套餐之间会变化的功能。本套餐新增的行排在最前，并以强调色绘制；从上一个套餐继承的行按其原有顺序排在其后，并以较弱的颜色绘制。
- 使用人们熟悉的刻度：整齐的团队规模，以及与你的套餐上限相符的用量数额。

### 最佳实践

- 让每个套餐的上限与下一个套餐的上限衔接：超过某个上限的第一个刻度，正是下一个套餐的刻度标记所在之处。
- 对 `IncludedUsage`、`MaxUsage`、用量刻度和 `OverageUnit` 使用同一单位。
- 保持估算诚实：总计是未含税金额，正如总计行所写。

### 常见问题

**为什么推荐的是更大的套餐，而较小的套餐可能更便宜？** 推荐遵循你设定的上限，而不是价格。如果某个套餐应当被推荐更长时间，请提高它的上限。

**滑块能从别的位置开始吗？** 设置 `SeatIndex` 和 `UsageIndex`，或将它们绑定到你的视图模型。

**能否把人们引导到注册页面，而不是就地开始试用？** 将 `TrialHandler` 设为打开你的注册流程；返回时试用开启，抛出异常时保持关闭。

## API 参考

<!-- api: UsagePricing -->

### UsagePricingPlan

<!-- api: UsagePricingPlan -->

### UsagePricingFeature

<!-- api: UsagePricingFeature -->

### UsagePricingPeriod

<!-- api: UsagePricingPeriod -->

### UsagePricingReason

<!-- api: UsagePricingReason -->

### UsagePricingRecommendationChangedEventArgs

<!-- api: UsagePricingRecommendationChangedEventArgs -->

### UsagePricingSelectionEventArgs

<!-- api: UsagePricingSelectionEventArgs -->

### UsagePricingTrialRequest

<!-- api: UsagePricingTrialRequest -->

### UsagePricingLabels

<!-- api: UsagePricingLabels -->

### UsagePricingSamples

<!-- api: UsagePricingSamples -->

## 无障碍

- 该区块是以其标题命名的区域；卡片是以套餐名称命名的补充性地标。
- 每个滑块以其标签命名，并朗读格式化后的数值（"16"、"12M"），其值为刻度索引。
- 计费开关名为 "Billing period"；每个分段朗读其标签以及是否被选中。
- 功能列表名为 "Included in {plan}"；每一行朗读其标签与数值。计量条对辅助技术隐藏，因为套餐名称已表达相同的内容。
- 当套餐或理由变化时，一个礼貌的实时区域会朗读 "{plan}. {reason}."。
- 试用按钮由其下方的说明描述。点击撤销后，焦点回到按钮。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 席位滑块、用量滑块、计费开关、试用按钮，试用开启时再到撤销 |
| Left / Down，Right / Up | 向下或向上移动一个刻度 |
| Page Down / Page Up | 向下或向上移动三个刻度 |
| Home / End | 第一个或最后一个刻度 |
| 开关上的 Left / Right | 切换到 Monthly 或 Annual，立即生效 |
| Enter / Space | 开始试用，或撤销试用 |

## 动效

| 元素 | 动效 |
|---|---|
| 滑块拇指 | 滑块自身的弹簧；数值与气泡数字以缓动淡入方式滚动 |
| 金额 | 每一位数字在 Smooth 弹簧上依次转过中间的数字，淡出并模糊至距离基线最多 2px；新数字与分隔符逐渐长出 |
| 套餐名称、"{plan} seats"、功能值 | 文字变形：保留的字母滑动，新字母升入，旧字母落下 |
| 理由、计费说明、试用说明、用量值 | 旧文字在 120ms 内向上淡出，新文字在 200ms 内上升 6px |
| 计量条 | 条形在柔和的 Smooth 弹簧上从左侧填充；当前名称在 240ms 内转为强调色 |
| 功能列表 | 保留的行滑动到新位置，新行在 240ms 内从上方 8px 处淡入，离开的行在 120ms 内原地淡出；列表高度按弹簧跟随变化 |
| 年付计费 | 开关的胶囊滑动，节省金额的勾位在 Smooth 弹簧上展开，其笔画在 350ms 内绘制，文字在 240ms 内变为绿色，折扣行展开并淡入 |
| 试用 | 按钮变色，标签与绘制的勾互换，宽度按 Smooth 弹簧跟随变化 |

减弱动效时保留颜色，以及新行和替换句子的短淡入；数字、变形、列表的滑动与高度，以及计量条都直接跳转。关闭动效时，一切都会立即完成。

## 响应式

该区块使用自身宽度，而非窗口宽度。从 780px 起，它排为两列（卡片宽 392px）；低于此宽度时，它按标题、控件、卡片和明细的顺序堆叠，标题为 28px，卡片内边距更紧凑。当节省金额放不进开关旁边时，计费行会换行。该区块没有最大宽度：请给它页面允许的宽度。

## 相关组件

- Plan comparison：逐项比较的固定套餐。
- Pricing calculator：以席位滑块定价的套餐卡片。
- `UsageBilling`：现有客户当前周期的用量。
- `Slider`、`SegmentedControl`、`TextMorph`：该区块所用的控件。
