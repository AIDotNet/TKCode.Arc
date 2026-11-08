---
title: Plan comparison
summary: 带有月付或年付开关的套餐卡片，开关会把每个价格滚动更新到新的计费周期；突出显示选中的套餐；特性矩阵展示每一档增加了什么。
source-hash: 71d72470075d
---

<Demo name="Preview" />

## 何时使用

Plan comparison 把两到三个套餐并排放在一起，让用户看清它们之间的差别，以及每个套餐的费用。切换计费周期会把每个价格计算为新的金额，选择某个套餐会为其所在的列着色，而一个开关会隐藏所有套餐共有的特性，只留下差异。

- **有两到三个层级的定价页面**：每个套餐占一列，显示按席位计算的价格和一个选择按钮，每个特性行显示每一档包含的内容。
- **月付与年付价格**：计费开关会把每个价格换算为另一个周期的金额，开关下方的提示会根据你的价格算出最大的年付节省。
- **特性列表很长**：“Show differences only” 会折叠所有套餐共有的行，筛选项旁边的数字会显示仍有多少个特性在列表中。
- **应用内的升级界面**：为已登录的用户选择套餐，并监听选择事件，以便把用户引导至结账。

## 何时不用

- **价格取决于席位或流量**：使用计算器，例如 `UsagePricing` 或 `PricingCalculator`，它们会根据用户拖动的数值计算价格。
- **只需要一个计费开关**：单独使用 `SegmentedControl`。
- **降级或取消**：使用 `CancelFlow`，它会在用户给出原因后提供优惠。
- **超过三到四个套餐**：列会变得太窄而难以阅读。请把套餐展示为卡片，并链接到完整的对比表。

## 用法

没有设置套餐和特性时，该区块会显示 Arc 预览中的示例方案：一个 Team 套餐、一个 Studio 套餐和八个特性。通过 `Plans` 和 `Features` 提供你自己的内容（或者绑定 `PlansSource` 和 `FeaturesSource`，设置后它们优先）。每个特性为每个套餐提供一个值，顺序与套餐一致：

```xml
<arc:PlanComparison Currency="EUR" Billing="{Binding Billing}" SelectedPlan="{Binding Plan}">
    <arc:PlanComparison.Plans>
        <arc:PlanComparisonPlan Name="Notebook" Description="For one field researcher" MonthlyPrice="8" YearlyPrice="6" />
        <arc:PlanComparisonPlan Name="Station" Description="For a survey crew" MonthlyPrice="19" YearlyPrice="15" />
    </arc:PlanComparison.Plans>
    <arc:PlanComparison.Features>
        <arc:PlanComparisonFeature Label="Offline maps" IsShared="True">
            <x:String>Included</x:String>
            <x:String>Included</x:String>
        </arc:PlanComparisonFeature>
        <arc:PlanComparisonFeature Label="Observers">
            <x:String>1 person</x:String>
            <x:String>Up to 12</x:String>
        </arc:PlanComparisonFeature>
    </arc:PlanComparison.Features>
</arc:PlanComparison>
```

### 自定义套餐与事件日志

三个以欧元定价的套餐，起初只列出差异。`SelectedPlanChanged` 报告选择的每一次变化（`IsUserAction` 可以区分按钮点击与应用自身的操作），`BillingChanged` 报告计费周期的每一次变化，而 `SelectCommand` 会在有人按下选择按钮时运行。

<Demo name="OwnPlans" />

### 区块自带的卡片

默认情况下，该区块会绘制一张卡片：带有边框、面板圆角、表面色，各部分之间有分隔线，最宽 1060 px 并居中。设置 `IsFramed="False"` 可使其与页面区域齐平嵌入，正如 Arc 预览所示。

<Demo name="Card" />

### 值

值等于 `Labels.Included`（“Included”）时显示绿色对勾；值等于 `Labels.NotIncluded`（“Not included”）时显示为弱化样式。其他文字按原样显示。当每个套餐都提供相同内容时，请将该特性标记为 `IsShared`，这样筛选项就能将其隐藏。

### 文字、文化与货币

所有文字都位于 `PlanComparisonLabels` 中：替换整个对象即可翻译该区块。包含数字或套餐名称的句子是函数。价格使用 `Labels.Culture` 和 `Currency` 中的 ISO 代码；整数价格不显示小数，带分的价格以分为单位计数。

<Demo name="Localized" />

### 窄容器

<Demo name="Narrow" />

### 减少动效

<Demo name="ReducedMotion" />

### 内容规范

- 套餐名称保持为一个词，描述只写几个词，使价格和按钮在各列之间对齐。
- 写出便于比较的值：“30 days” 对比 “Unlimited”，比两句话读起来更快。
- 把差异最大的特性放在靠前的位置；共有的特性可以保留，筛选项会将其隐藏。
- 在脚注中说明价格包含什么：货币、按席位计算、税费，以及年付套餐如何计费。

### 最佳实践

- 双向绑定 `SelectedPlan`，并从 `SelectedPlanChanged` 或 `SelectCommand` 开始结账；再次按下已选中套餐的按钮会清除选择。
- 保持两到三个套餐。每个套餐都会增加一列，而每一列都必须能放在特性名称旁边。
- 年付价格请按月计算。提示会根据你的月付和年付价格算出 “save up to N%”。

## API 参考

<!-- api: PlanComparison -->

<!-- api: PlanComparisonPlan -->

<!-- api: PlanComparisonFeature -->

<!-- api: PlanComparisonSelectionChangedEventArgs -->

<!-- api: PlanComparisonBillingChangedEventArgs -->

## 无障碍

- 矩阵以其套餐命名（“Team and Studio plan comparison”）。每个套餐标题命名为 “Team plan”，每个值以其套餐命名（“Team: 30 days”），每一行以其特性命名。
- 价格是礼貌型实时区域，其名称始终为最终价格，因此屏幕阅读器不会朗读价格计数过程中的数字。
- 选中的套餐会以礼貌方式播报（“Team selected”）。选择按钮保持名称 “Select Team”，并将 “Selected” 报告为其状态。
- 计费开关名为 “Billing period”，筛选开关名为 “Show differences only”。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在计费开关、筛选开关和选择按钮之间移动 |
| `Left` / `Right` | 在计费开关中选择计费周期 |
| `Space` | 打开或关闭筛选 |
| `Enter` / `Space` | 选择套餐；若已选中，则清除选择 |

键盘焦点会在被聚焦的控件周围显示 2 px 的焦点环；指针焦点则不显示。

## 动效

| 变化 | 完整 | 减少动效 | 关闭 |
|---|---|---|---|
| 计费开关切换 | 每个价格在 240 ms 内沿进入曲线计数到新金额 | 直接跳变 | 直接跳变 |
| 特性数量变化 | 在 240 ms 内计数 | 直接跳变 | 直接跳变 |
| 选中某个套餐 | 该列在 240 ms 内着色；导轨原地出现 | 着色和导轨立即出现 | 立即 |
| 选择移动 | 导轨以 Arc 的响应式弹簧（刚度 520，阻尼 44）滑动 | 立即移动 | 立即移动 |
| 行隐藏 | 高度以平滑弹簧收缩（0.4 s，无回弹），同时在 160 ms 内淡出 | 立即消失 | 立即消失 |
| 行恢复 | 高度以平滑弹簧展开，在 40 ms 后的 240 ms 内淡入 | 立即恢复 | 立即恢复 |

被打断的计数会从屏幕上当前显示的数字继续。

## 响应式

- 布局跟随该区块自身的宽度，而不是窗口宽度。在 720 px 或更小（`:compact`）时，计费开关堆叠到标题下方，边距收窄为 20 px。
- 在 540 px 或更小（`:narrow`）时，特性列消失：每个特性名称位于其值的上方，值仍位于其套餐列下方，标题和价格变小。
- 在 340 px 或更小（`:tiny`）时，套餐和值的内边距收紧为 12 px 和 16 px，使两个套餐仍能并排放下。

## 相关

| 组件 | 适用时机 |
|---|---|
| `PlanComparison` | 用户要并排比较几个固定套餐 |
| `UsagePricing` | 套餐由席位和流量决定 |
| `PricingCalculator` | 价格取决于席位数，并随数量变化而滚动更新 |
| `SegmentedControl` | 你只需要计费开关 |
| `CancelFlow` | 用户正在降级或取消 |
