---
title: Pricing calculator
summary: 由席位滑块和计费开关决定价格的方案卡片：拖动滑块时价格逐位滚动，推荐徽章滑动到最适合团队的方案上。
source-hash: 5cd9742bf813
---

<Demo name="Preview" />

## 何时使用

`PricingCalculator` 是面向按席位销售的产品的定价区块。滑块设置团队规模，开关设置月付或年付，三张方案卡片展示该团队在各方案下的费用：每个席位每月的价格、计费周期内的总价，以及该规模下方案包含的功能。底部一行汇总当前使用的方案，显示年付可节省的金额，并提供试用入口。拖动滑块时，每个数字都会逐位滚动到新值，"Best fit" 徽章滑动到最适合团队的方案上，选中框也会跟随当前使用的方案移动。

- **按席位计费的 SaaS 定价。** 按席位计价，提供年付折扣、面向大团队的批量折扣，以及带有席位上限的小型方案。
- **回答"我们要付多少钱？"的定价页面。** 用户看到的是自己的数字，而不必阅读表格，推荐结果也会告诉他们从哪里开始。
- **产品内的升级界面。** 将滑块的初始值设为团队当前规模并选中其方案，让页面展示团队扩容后会发生哪些变化。

## 何时不用

- **价格取决于用量而非席位。** 请求数、存储或分钟数更适合用用量估算来展示（参见用量计费）。
- **逐项功能对比。** 当用户需要比较多个方案的数十项功能时，请使用对比表格（参见方案对比），本区块只用于展示价格。
- **只需要按月/按年切换。** 只有单一价格加周期切换时，只需要 `BillingToggle` 和 `BillingPrice`。
- **结账。** 该区块以试用按钮结束。支付信息、税费和发票属于结账流程。

## 用法

没有任何设置时，该区块显示其示例内容：23 个席位档位（从 1 到 300）、三个方案以及模拟的试用。对于你的产品，请提供你自己的方案和档位。

```xml
<arc:PricingCalculator Title="Plans for every crew" Currency="USD" Billing="Yearly">
    <arc:PricingCalculatorPlan Id="crew" Name="Crew" Description="Dispatch and job notes for small teams"
                               MonthlySeatPrice="18" YearlySeatPrice="15" MaxSeats="16" RecommendFromSeats="1">
        <arc:PricingCalculatorFeature Text="Job board and map" />
        <arc:PricingCalculatorFeature Kind="PerSeat" PerSeatAmount="50" Text="{}{0} photo uploads a month" />
    </arc:PricingCalculatorPlan>
    <arc:PricingCalculatorPlan Id="fleet" Name="Fleet" MonthlySeatPrice="30" YearlySeatPrice="24" RecommendFromSeats="12">
        <arc:PricingCalculatorFeature Text="Everything in Crew" />
        <arc:PricingCalculatorFeature Kind="VolumeDiscount" />
        <arc:PricingCalculatorFeature Text="Quarterly route review" IsYearlyOnly="True" />
    </arc:PricingCalculatorPlan>
</arc:PricingCalculator>
```

```csharp
calculator.SeatStops = [1, 2, 3, 5, 8, 12, 16, 24, 32, 48, 64, 100, 150, 250, 500];
calculator.SetSeats(12);
calculator.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Id, request.Seats, request.Billing, cancellation);
```

### 组成部分

- **标题区。** `Title` 和 `Description`，居中显示；当页面自带标题时，用 `IsHeaderVisible="False"` 将其隐藏。
- **控件。** "Team size" 及其两侧的圆形减号和加号按钮（中间显示席位数）、滑块（为每个方案标出开始推荐的位置），以及在 "Annual" 旁显示年付折扣的计费开关。
- **方案卡片。** 方案名称和 "Best fit" 徽章、一行描述、带 "per seat / per month" 的席位价格、卡片总价、方案按钮和功能列表。
- **选中框。** 围绕当前使用方案的细线轮廓。它位于卡片之上的一层，并在卡片之间滑动。
- **合计。** 方案和席位数、带计费周期的总价、席位价格行、年付节省金额（若当前为月付，则显示一个切换到年付的按钮），以及试用按钮。

### 价格如何计算

每条规则都是数据，因此同一个区块可以为任何按席位计费的产品定价：

- 每个席位每月的价格为方案的 `MonthlySeatPrice`；若为年付，则使用 `YearlySeatPrice`。未设置年付价格的方案，按月付价格减去 `YearlyDiscount`（默认 20%）计算。
- `VolumeTiers` 从某个席位数起，为每个席位减免一定金额；达到的最大档位生效，两种计费周期均适用。
- 卡片总价为席位价格乘以席位数；若为年付，再乘以 12。
- 节省金额是年付一年所节省的金额：(月付价格 − 年付价格) × 12 × 席位数。批量折扣同时作用于两种价格，因此两种周期下的节省金额相同。
- 推荐方案是席位数已达到的 `RecommendFromSeats` 中最大的那个。席位数介于两个阈值之间时，仍停留在较低的方案上。如果需要其他规则，请用 `RecommendPlan` 替换该规则。

计算得到的值可通过 `Seats`、`RecommendedPlan`、`SelectedPlan`、`Total` 和 `YearlySavings` 获取。

### 选择方案与席位上限

在有人选择方案之前，当前使用的方案就是推荐方案，并会随滑块移动。通过按钮选中的方案会一直保持使用状态，无论滑块如何移动（`PickedPlanId` 为双向绑定）。设置了 `MaxSeats` 的方案不能用于超过其上限的席位数：它的卡片会弱化描述和功能列表，用一句简短说明（"Solo covers up to 8 seats"、"You chose 20 seats."）取代价格，其按钮则提供回到上限的操作。超过上限时，当前使用的方案会回退为推荐方案，但选择会被保留：滑回上限以下后，被选中的方案会重新投入使用。

### 功能行

`PricingCalculatorFeature` 行可以随席位数和计费周期变化：

- `Kind="PerSeat"` 将 `PerSeatAmount` 乘以席位数，并把结果填入 `Text` 中的 `{0}`（"500 GB file storage"）。
- `Kind="VolumeDiscount"` 显示该方案当前的批量折扣，仅在折扣生效时出现。
- `MinSeats` 表示从某个席位数起才显示该行；`IsYearlyOnly` 表示仅在年付时显示。
- `TextFactory` 由你自行计算文本；返回 null 则隐藏该行。

出现的行会从零高度展开并淡入；移除的行则淡出并收起。每张卡片都会随其列表一起增高。

### 你的后端、事件与试用

`TrialHandler` 接收方案、席位数、计费周期和总价，并返回试用是否已开始。它运行期间，试用按钮会显示加载指示并忽略点击；试用开始后，按钮会显示 "Trial is live" 持续 `TrialSuccessDuration`（2.8 s），屏幕阅读器会读出开始的是哪个试用。若未设置处理程序，该区块会等待 1.1 s 后成功，与预览一样。`PlanPicked`、`SeatsChanged`、`BillingChanged` 和 `TrialStarted` 报告用户的操作；`PlanPickedCommand` 和 `TrialStartedCommand` 是对应的命令版本。

<Demo name="Backend" />

### 窄容器

该区块读取的是自身的宽度，而不是窗口的宽度。宽度为 860 px 或更小时，卡片改为堆叠排列，未被使用的卡片会收起为名称、价格和按钮；试用按钮移到合计区下方。宽度为 560 px 或更小时，计费开关横跨整个宽度，合计区也改为堆叠排列。

<Demo name="Narrow" />

### 货币、区域与文案

`Currency` 接受 ISO 4217 代码，`Labels.Culture` 负责格式化每个金额和数量。所有文字都位于 `PricingCalculatorLabels` 中；包含滚动金额的句子（例如节省金额行）是带 `{0}` 的格式字符串。

<Demo name="Localized" />

### 减少动效

在减少动效模式下，数字、选中框、徽章和计费滑块会直接跳到新位置，展开和收起的行会立即完成，文字变化则保留短暂的淡入淡出。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: PricingCalculator -->

<!-- api: PricingCalculatorPlan -->

<!-- api: PricingCalculatorFeature -->

<!-- api: PricingCalculatorVolumeTier -->

<!-- api: PricingCalculatorLabels -->

<!-- api: PricingCalculatorTrialRequest -->

<!-- api: PricingCalculatorPlanPickedEventArgs -->

<!-- api: PricingCalculatorSeatsChangedEventArgs -->

<!-- api: PricingCalculatorBillingChangedEventArgs -->

<!-- api: PricingCalculatorTrialStartedEventArgs -->

## 无障碍

- 对辅助技术而言，滑块是名为 "Team size" 的滑块，并以席位数（"20 seats"）作为帮助文本。
- 步进按钮名为 "Remove a seat" 和 "Add a seat"；计费选项是名为 "Billing period" 的分组中的单选按钮，年付选项的名称中包含其折扣。
- 每张卡片以其方案名称命名，其功能列表名为 "Studio features"，每个方案按钮都以其作用命名（"Pick Scale"、"Switch to 8 seats"）。
- 滚动数字是绘制出来的，但其完整文本（"$4,800"）是它的无障碍名称。
- 一个礼貌的实时区域会播报已选中的方案（"Studio picked"）、计费的变化，以及试用已开始（或失败）。滑块移动不会被播报；播报的是滑块自身的值。
- 选择 "Switch to 8 seats" 后，焦点保持在该按钮上，此时该按钮读作 "Current pick"。

### 键盘

| 按键 | 操作 |
| --- | --- |
| Tab | 减号、加号、滑块、已选中的计费选项、每个方案按钮、年付优惠（仅在按月计费时）、试用按钮。 |
| ← / ↓, → / ↑ | 在滑块上：向下或向上移动一个档位。在计费开关上：选择另一个选项，并将焦点移到它上面。 |
| Page Down / Page Up | 在滑块上：向下或向上移动两个档位（范围的 10%）。 |
| Home / End | 在滑块上：移到第一个或最后一个档位。 |
| Enter / Space | 按下已聚焦的按钮。 |

键盘焦点会在被聚焦的控件外显示 2 px 的焦点环；指针焦点则不显示。

## 动效

| 元素 | 动效 |
| --- | --- |
| 数字 | 每一位数字都是一个滚轮。数字增大时，每个滚轮向前转动 (新值 − 旧值) mod 10 步，经过中间的数字；数字减小时则向后转。所有滚轮共用 Smooth 弹簧，没有错开延迟。与下一位相差一步的字形会淡出至零透明度，并模糊 2 px。新的变化会从滚轮当前位置继续，并保持其速度。 |
| 新增数位 | 新的一列（及其分隔符）从零宽度展开，并以 Smooth 弹簧淡入；消失的列则收缩并淡出。 |
| 选中框 | 以临界阻尼弹簧（视觉时长 0.5 s）跟随当前使用的方案。它会追随卡片当前的尺寸，因此卡片收起和展开时，它也随之变化。 |
| Best fit 徽章 | 从旧徽章所在的位置开始，并以同一弹簧弹入就位；若推荐方案再次变化，它会保持其速度。 |
| 计费滑块 | 在所选选项下方以 Settle 弹簧移动并调整大小。 |
| 文字变化 | 新文本在淡入时上移 0.35em（240 ms）；旧文本在淡出时上移 0.3em（160 ms）。 |
| 功能行与收起 | 高度使用 Smooth 弹簧；透明度为 240 ms。 |
| 方案标记 | 推荐方案的标记在 240 ms 内变为文字颜色。 |

减少动效：数字、选中框、徽章和滑块直接跳转，高度立即变化，文字变化只做淡入淡出。

## 响应式行为

- **超过 860 px：** 三列等宽，每张卡片与最高的卡片等高；计费开关位于滑块右侧；合计区为一行。
- **860 px 或更小：** 单列；未使用的卡片收起为名称、价格和按钮；试用按钮在合计区下方横跨整个宽度。
- **约 600 px：** 计费开关移到滑块下方。
- **560 px 或更小：** 标题变小，内边距收紧，计费开关横跨整个宽度，合计区堆叠排列。
- **360 px 或更小：** 年付优惠和节省金额行可能换行。

## 相关内容

- 方案对比：以表格形式列出各方案的功能。
- 用量计费：价格随用量而非席位变化。
- `BillingToggle` 和 `BillingPrice`：周期切换与可滚动的价格。
- `AnimatedCounter`：单个滚动数字。
