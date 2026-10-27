---
title: Budget variance
summary: 按部门对比预算与实际支出：共享刻度的子弹图、带正负号的差异、让每根条一起弹簧的期间切换、原地展开的预算科目，以及合计行。
source-hash: 87ccea4ca2f3
---

<Demo name="Preview" />

## 何时使用

`BudgetVariance` 一眼回答一个问题：哪些团队花得比计划多，哪些花得少，差了多少。每一行都是同一刻度上的子弹图，所以预算刻度从上到下对齐，超支最多的那一行不用读数字就能看出来。

- **月结与预算回顾。** 按部门或成本中心对比计划与实际运营支出，再展开某个部门，看是哪些科目造成了差异。
- **财务主管的看板。** 在本月、本季度和年初至今之间切换；每根条和每个数字一起移动到新的期间。
- **任何带容差的计划对实际。** 人力成本、各团队的云支出或项目的各个阶段：上下几个百分点没关系，超出就值得看一眼。

## 何时不用

- **解释一个总数是怎么变化的。** 从起始值走到结束值的瀑布图更合适。
- **跨很多期间的趋势。** 这个区块一次只比较一个期间；连续序列请用 `LineChart` 或 `BarChart`。
- **收入。** 每个数字都按支出来解读，所以低于预算是有利的。收入对目标的解读方向正好相反。
- **几十个成本中心。** 当排序与筛选比图形更重要时，请用 `Table`。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。`Periods` 是切换器里的期间；每个 `BudgetPeriod` 有一个 id、一个简短标签、标题下方显示的说明，以及用于读屏句子的短语。`Departments` 包含 `BudgetLine`，每个科目针对每个期间 id 有一个 `BudgetAmount`（预算与实际）。部门数字和合计都由科目求和，所以它们总是对得上。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BudgetVariance SelectedPeriod="month" ExpandedDepartment="eng">
        <arc:BudgetVariance.Periods>
            <arc:BudgetPeriod Id="month" Label="Month" Description="March 2026" Phrase="in March" />
            <arc:BudgetPeriod Id="quarter" Label="Quarter" Description="January to March 2026" Phrase="in the first quarter" />
        </arc:BudgetVariance.Periods>
        <arc:BudgetDepartment Id="eng" Name="Engineering" Owner="Ravi Menon">
            <arc:BudgetLine Id="cloud" Name="Cloud hosting">
                <arc:BudgetAmount Period="month" Budget="176000" Actual="213700" />
                <arc:BudgetAmount Period="quarter" Budget="528000" Actual="613400" />
            </arc:BudgetLine>
        </arc:BudgetDepartment>
    </arc:BudgetVariance>
</UserControl>
```

在代码中，`new BudgetLine("cloud", "Cloud hosting").With("month", 176_000, 213_700)` 添加一个期间的金额并返回这个科目。

### 结构

- **头部**：`Title`、期间说明（或 `Description`）以及期间切换。
- **列标题**：Department、图表上方的刻度（预算的 0%、50% 与 100%）、Actual、Budget 和 Variance。
- **合计行**：所有部门的总和，名称下方是部门数量。
- **部门行**：负责人的头像（除非设置了 `OwnerAvatar`，否则显示首字母）、名称与负责人、子弹图、数字和一个箭头。展开的部门带浅色填充，它的科目列在下方，并缩进到头像之后。
- **图例**：说明条、刻度、红色部分和区带的含义。

### 读懂子弹图

- 每根条后面的**区带**标出容差：到预算的 `RangeLow`、从那里到 `RangeHigh`，以及再往外。它们用低透明度的文字颜色绘制，所以无论主题色是什么，区块都保持中性。
- **条**是实际支出。超过预算**刻度**的部分用危险色绘制。
- **刻度范围**由每一行、每一个期间共享：至少是预算的 120%，并向上取整到十分之一，让任何期间里最大的数字都放得下。切换期间时刻度位置不变。
- **差异**是一个带正负号的金额和一个以 "over"、"under" 或 "on plan" 结尾的比例。差异小于预算的 `OnPlanThreshold`（默认千分之五）时视为 on plan，保持中性；超支为红色，节省为绿色。

<Demo name="Tolerance" />

### 期间与部门

`SelectedPeriod`（双向）指定显示的期间；切换器会改变它。`ExpandedDepartment`（双向）指定展开的部门，或为 null；`ToggleDepartment(id)` 展开或收起一个部门。展开一个部门会收起原来展开的那个。`PeriodChanged` 报告期间变化，`DepartmentToggled` 报告每一次展开与收起，被收起的那个先报告。

<Demo name="Events" />

### 窄容器

区块读取的是自身宽度。宽度不小于 760 时是列对齐的表格。更窄时，每一行把名称、"实际 of 预算"和差异放在上面，子弹图横跨整行放在下面，所以图表永远不会被挤压。长名称会换行，而不是被截断。

<Demo name="Narrow" />

### 文案与货币

`Currency` 放在每个金额前（默认 "$"）；金额缩写为 k 和 M。其余所有文字都来自 `Labels`（一个 `BudgetVarianceLabels`）：替换它即可本地化区块，包括读屏听到的句子。

## API 参考

<!-- api: BudgetVariance -->

<!-- api: BudgetPeriod -->

<!-- api: BudgetDepartment -->

<!-- api: BudgetLine -->

<!-- api: BudgetAmount -->

<!-- api: BudgetVarianceKind -->

<!-- api: BudgetVarianceLabels -->

## 键盘

- **Tab** 先到期间切换，再进入部门列表，然后离开区块。
- 在期间切换上，**Left** 和 **Right** 改变期间。
- 在部门列表中，**Up** 和 **Down** 移到上一个或下一个部门，**Home** 和 **End** 移到第一个或最后一个。
- **Enter** 或 **Space** 展开或收起焦点所在的部门。

## 无障碍

- 每个部门行都是一个报告展开或收起状态的按钮，名称是一个完整的句子：在哪个期间、花了多少、预算是多少、差异多少，以及是否有利。合计行和每个预算科目也带有同样的句子；图表和正在计数的数字本身不会被读出，所以不会重复读，也不会读到计数中途的值。
- 切换期间时，通过礼貌型实时区域播报新的期间和合计。
- 差异从不只靠颜色表达：金额带正负号，每个比例都以 over、under 或 on plan 结尾。
- 指针聚焦不画焦点环；键盘聚焦时该行带填充，并显示 Arc 的焦点环。

## 动效

- **切换期间**：每根条在同一个临界阻尼弹簧（smooth，无过冲）上伸缩到新长度，每个金额和比例也在同一弹簧上计数到新值。红色的超支部分就是同一根条被裁剪到刻度右侧的部分，所以颜色恰好在条越过刻度时出现。
- **展开部门**：科目在高度与透明度上一起长出，没有过冲；收起更快。箭头沿 Arc 的弹簧曲线转动，展开时使用主题色。
- 每个动画都可以被打断：新的期间或展开从条和数字当前的位置开始。
- 减弱动效时，条和数字直接跳到终值，科目直接以全高展开。关闭动效时所有变化立即生效。

## 相关组件

- `Market terminal`，Finance 分组的另一个区块。
- `SegmentedControl` 和 `Avatar`，构成这个区块的控件。
- 只有一根对照目标的进度条时用 `Progress`。
