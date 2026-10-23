---
title: 客户星系
summary: 每位客户都是一个点：点的面积随收入变化，颜色表示健康度，整体如星系般旋转；可将它们分成簇，飞入其中，并查看合计。
source-hash: de95bcccc15c
---

<Demo name="Preview" />

## 何时使用

`CustomerGalaxy` 把每个客户账户画成一个点：点的面积随账户的月度经常性收入（MRR）增长，颜色表示账户的健康度。在总览中，这些点形成一条倾斜且缓慢旋转的螺旋，最大的账户聚集在核心，大量的小账户则沿两条旋臂拖出长长的尾巴。选择一种分组后，每个点都会弹到各自所属值的一个紧凑簇中，簇上标明其 MRR 与账户数。舞台旁边（在窄容器中位于舞台下方）的合计面板会汇总全部账户，或你飞入的那个簇的数字，并列出各个簇，每个簇用一条按健康度分段的条形表示。当你要回答的问题是数千个账户中收入和风险分别集中在哪里时，就适合使用它。

- **客户成功首页**，团队每周一打开它，按健康度分组并飞入风险簇。红色的大点就是需要优先联系续约的客户；悬停在任一点上，即可看到其负责人和一句备注。
- **收入评审**，财务按套餐分组，查看最高一档承载了多少 MRR，然后把列表切换为账户，看看每一档有多少客户。
- **区域销售看板**，按区域分组，每位经理飞入自己的簇，再用方向键从最大的账户开始逐个浏览。
- **产品分析页面**，按行业对客户分组，看哪些市场带来大客户、哪些市场带来许多小客户，以及哪些市场在增长。
- **董事会演示屏幕**，停留在总览上：旋转的星系与合计一目了然，也会吸引人点进去探索。

## 何时不用

- **比较少量数字。** 使用 `BarChart` 或 `DonutChart`；星系只有在数百或数千个账户时才物有所值，五个数字时并不适合。
- **并排读取许多账户的精确数值。** 使用 `SortableDataTable` 或 `DataGrid`；星系一次只在提示卡中展示一个账户。
- **随时间变化的趋势。** 使用 `LineChart` 或 `Streamgraph`。星系是一个快照：它的簇不会随日期移动。
- **侧边栏或卡片中的小部件。** 该区块至少需要 360 px 的舞台高度才能看清；请改用 `MetricCard` 搭配 `Sparkline`。
- **仅按一个维度展示整体的一部分。** 使用 `Treemap` 或 `WaffleChart`，它们能保持精确的面积，没有堆叠留下的空隙。

## 用法

为控件提供账户数据，并把你想要的分组设为标签页。每个 `GalaxyAccount` 包含名称、MRR、健康度键，以及在 `Groups` 中它在每种分组下的取值。控件自己布局螺旋与簇，汇总合计，并构建列表。

```xml
<arc:CustomerGalaxy Title="Where the revenue sits"
                    ItemsSource="{Binding Accounts}">
    <arc:GalaxyGrouping Key="plan" Header="Plan">
        <arc:GalaxyGroupDefinition Key="solo" Label="Solo" />
        <arc:GalaxyGroupDefinition Key="team" Label="Team" />
        <arc:GalaxyGroupDefinition Key="business" Label="Business" />
        <arc:GalaxyGroupDefinition Key="corporate" Label="Corporate" />
    </arc:GalaxyGrouping>
    <arc:GalaxyGrouping Key="region" Header="Region" />
    <arc:GalaxyGrouping Key="health" Header="Health" />
</arc:CustomerGalaxy>
```

`CustomerGalaxy` 派生自 Avalonia 的 `TemplatedControl`。舞台由控件自己绘制（每个账户一个点，按颜色和暗化程度批量绘制），因此数千个账户时依然流畅；标签、提示卡、按钮和面板都是普通控件。控件会设置 `:grouped`、`:cluster-focused`、`:row-hover`、`:side` 和 `:empty` 这几个伪类，供样式使用。

### 结构

1. **标题与副标题**（`Title`、`Subtitle`、`SubtitleFormat`）：16 px 中等字重的标题，加上 14 px 的弱化副行。没有 `Subtitle` 时，副行显示“2,400 accounts. Dot size shows MRR, color shows health.”；设为空字符串则隐藏它。
2. **分组标签页**（`Groupings`、`AllText`）：一条弱化底色、34 px 高的轨道上，是“All”和每个分组各一个标签；一块表面色的药丸形滑块滑到所选标签上。当容器比标签页窄时，该行可以横向滚动。
3. **图例**（`ShowLegend`、`SizeLegendText`）：每种健康状态一个彩色点，一条分隔线，以及一个小的和一个大的弱化点，用来表示大小。两者都能放下时，它位于标签页右侧；否则移到标签页下方。
4. **舞台**：表面色上的点。在总览中，圆盘的高度被压缩为 68%，前排的点最多放大 12%，使其读起来像一个朝向你倾斜的圆盘。
5. **簇标签**：每个簇上方 10 px 处是簇名称、它的 MRR（滚动数字）和账户数。
6. **提示卡**（`AccountTooltipTemplate`）：账户名称、它在各分组中的取值、MRR、负责人、健康度和备注，位于指针旁边，或固定在所选点的旁边。
7. **操作提示**（`ShowHint`、`HintText`）：舞台左下角的一行弱化文字。聚焦簇或悬停列表行时，它会淡出。
8. **缩放栏**（`ShowZoomControls`）：缩小、放大和适应，位于右下角 12 px 处。当视图已经显示全部内容时，适应按钮会变暗。
9. **“显示全部”**（`ShowAllText`）：聚焦簇时，左上角出现的一个药丸形按钮。
10. **合计面板**（`ShowTotals`、`TotalsPlacement`）：范围名称、大号的 MRR、账户数、增长与风险的小计，以及带有 MRR / 账户数切换的簇列表。

### 账户

在 `Items` 中直接添加 `GalaxyAccount` 对象，或将 `ItemsSource` 设为一个列表；当该列表是可观察集合时，星系会跟随其变化。当所选账户和所聚焦的簇仍然存在时，它们在变化后会被保留。若你原地修改了某个账户，请调用 `Refresh()`。

- `Id` 是账户的稳定键；选中状态依此匹配。`Name` 显示在提示卡上，并会被朗读。
- `Mrr` 决定点的面积：在适配的缩放下，其半径为 `0.30 + 0.028 × √MRR` px，且至少为 0.74 px。
- `Health` 是某个 `HealthStates` 的键；键未知的账户以中性状态绘制。
- `Groups` 按分组的键保存账户在每种分组中的取值（`["plan"] = "business"`）。
- `Owner` 和 `Note` 填充提示卡；`Tag` 由你自由使用。

```csharp
var account = new GalaxyAccount
{
    Id = "acct-0042",
    Name = "Juniper Labs",
    Mrr = 18_400,
    Health = "growing",
    Owner = "Rhea Ingram",
    Note = "Upgrade call booked"
};
account.Groups["plan"] = "business";
account.Groups["region"] = "harbor";
accounts.Add(account);
```

### 分组

每个 `GalaxyGrouping` 添加一个标签页。它的 `Key` 指明读取 `Groups` 中的哪一项；键为 `health` 时按 `Health` 分组，并从 `HealthStates` 获取簇、标签和颜色。列出 `GalaxyGroupDefinition` 可以为取值设置标签，并为并列的情况指定固定顺序；账户带有但未列出的取值会排在它们之后；没有取值的账户组成一个“Other”簇（`OtherText`）。`Header` 是标签页的文字，`ListTitle` 是列表的标题（未设置时，由 `ListTitleFormat` 生成“By plan”）。

`SelectedGroupingKey` 为双向：为 null 时显示总览。它变化时，每个点都会从当前位置弹到新布局中对应的位置，镜头也会适配新布局；标签在 120 ms 之后淡入。簇按账户数从多到少排列：最大的簇位于中央，其余按 MRR 依次排在它的周围（左、右、左下、右上、左上……），并为它们的标签留出空间。总览的列表按健康度排列；选择其中一行会切换到健康度分组，并飞入该簇。

<Demo name="Clusters" />

### 健康状态

`HealthStates` 默认包含四种状态，按图例顺序为：增长（`Success`）、稳定（`TextMuted`，透明度 72%）、降温（`Warning`）和风险（`Danger`）。每种状态通过 `Tone` 指定其绘制所用的 ArcDesign 颜色（或使用固定的 `Color`），通过 `DotOpacity`、`BarOpacity` 指定其点和条形段的绘制强度，并通过 `Layer` 指定绘制顺序（稳定的点先画，风险的点在最上面）。带有 `IsGrowth` 的状态给出“增长”小计，带有 `IsRisk` 的状态给出“风险”小计，以及每个簇的风险占比。颜色会在每次主题或强调色变化时重新从主题读取。

```csharp
galaxy.HealthStates.Clear();
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "expanding", Label = "Expanding", Tone = GalaxyTone.Success, Layer = 1, IsGrowth = true });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "stable", Label = "Stable", Tone = GalaxyTone.Neutral, DotOpacity = 0.72, BarOpacity = 0.5 });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "churning", Label = "Churning", Tone = GalaxyTone.Danger, Layer = 2, IsRisk = true });
```

### 聚焦簇与选择账户

双击一个簇（或它的标签），选择列表中它的那一行，按 ← 或 →，或设置 `FocusedGroupKey`，都能飞入该簇：簇、它 10 px 的间距及其标签占满舞台高度的四分之三，其他每个簇淡化到原不透明度的七分之一，其标签淡化到 30%，出现“显示全部”，合计面板切换为该簇的数字。`FlyToGroup(key)`、`FocusNextGroup()` 和 `FocusPreviousGroup()` 可以从代码中做同样的事；`GroupFocused` 报告每一次变化。`ResetView()`、“显示全部”、适应、0 和 Home 会返回到所有簇。

将指针悬停在一个点上，即可在指针旁边看到它的提示卡；单击即可选中它（`SelectedAccount` 为双向，另有 `AccountSelected`）。被选中的点保留一个环，其提示卡保持打开：指针在舞台上时，提示卡位于指针旁边；指针离开时，则固定在该点右侧 12 px 处。单击空白处或按 Escape 会清除选择。↓ 与 ↑（或 `SelectNextAccount()` 和 `SelectPreviousAccount()`）会按账户数从多到少，依次浏览所聚焦簇中的账户；`FlyToAccount(account)` 会选中一个账户并缩放到它。

<Demo name="FocusedCluster" />

```csharp
galaxy.GroupFocused += (_, e) =>
{
    // e.Group is null when the view returned to every cluster.
    status.Text = e.Group is { } group ? $"{group.Label}: {group.AccountCount} accounts" : "All clusters";
};
```

### 相机

拖动即可平移：舞台以 1:1 跟随指针，超出内容边缘时会像橡皮筋一样拉伸，松开后弹回，没有滑行。鼠标滚轮以指针为中心每格缩放 1.246 倍；触控板滚动仍然滚动页面，而按住 Ctrl 或 ⌘ 再使用滚轮或捏合手势，则会缩放。缩放按钮以及 + 和 − 键以中心为基准，按 `ZoomStep`（1.6）缩放。视图不会缩小到适配视图之外，也不会放大到其 `MaxZoom`（9）倍之外。滚轮缩放之后，视图会等待 1.1 s，再滑回内容之内。`ZoomBy(factor, anchor)` 可在代码中缩放，`Zoom` 报告当前缩放值（1 表示适配）。

### 合计与列表

面板会标明其范围（“All accounts”，或所聚焦的簇），以大号滚动数字显示 MRR，并给出三项小计：账户数、增长 MRR 和风险 MRR。列表每个簇一行（总览中则每种健康状态一行）：名称、数值、一条 4 px 的条形和一行说明。将 `GroupSort` 设为 `Mrr` 时，各行按 MRR 排序，条形也按 MRR 测量，说明文字显示账户数；设为 `Accounts` 时，则按账户数排序和测量，说明文字显示 MRR。在健康度分组之外，说明还会加上该簇 MRR 中处于风险的占比，而条形则按健康状态分成若干段。悬停某一行会使其他所有簇变暗，但镜头不移动；选择它则会飞入该簇。`Summary` 提供同样的数字，供你自己的文字使用。

### 布局与容器宽度

布局跟随控件自身的宽度，而不是窗口的宽度：

| 控件宽度 | 合计 | 舞台高度 |
|---|---|---|
| 880 px 及以上 | 位于舞台旁边，宽 288 px | 舞台宽度的 16:9，最大 560，且不小于面板高度 |
| 620 至 880 px | 位于舞台下方，分两列 | `max(360, width × 0.52)` |
| 620 px 以下 | 位于舞台下方，单列 | 380 |

将 `TotalsPlacement` 设为 `End` 可在任何宽度下让面板保持在舞台旁边，设为 `Bottom` 则让它保持在舞台下方。适配缩放随舞台尺寸变化，因此宽的舞台会画出更大的星系；标签的大小保持不变。

<Demo name="SidePanel" />

<Demo name="Narrow" />

### 你自己的数据

任何客户列表都适用：工作区、门店、诊所。你可以带上自己的分组、健康状态和文案，并用 `CurrencySymbol` 与 `CultureName` 格式化金额，或使用你自己的 `MrrFormatter`。

<Demo name="YourData" />

```xml
<arc:CustomerGalaxy CurrencySymbol="€"
                    AllAccountsText="All workspaces"
                    AccountCountFormat="{}{0} workspaces"
                    OneAccountFormat="{}{0} workspace"
                    SizeLegendText="Size is revenue"
                    ShowHint="False" />
```

### 文案

每段文字都有英文默认值，并有一个属性可以修改它。格式字符串用 `{0}` 表示它所指代的值。

| 属性 | 默认值 | 用途 |
|---|---|---|
| `SubtitleFormat` | "{0} accounts. Dot size shows MRR, color shows health." | `Subtitle` 为 null 时的副标题 |
| `AllText` | "All" | 总览标签页 |
| `AllAccountsText` | "All accounts" | 未聚焦簇时面板的范围 |
| `MrrCaptionText` | "Monthly recurring revenue" | 大号数字上方的说明 |
| `MrrText` | "MRR" | 排序按钮和提示卡的第一行 |
| `AccountsText` | "Accounts" | 小计和排序按钮 |
| `OwnerText` | "Owner" | 提示卡的负责人行 |
| `HealthText` | "Health" | 提示卡的健康度行 |
| `ListTitleFormat` | "By {0}" | 列表标题；`{0}` 为小写的分组名称 |
| `HealthGroupingText` | "Health" | 健康度分组的名称（总览列表、播报） |
| `AccountCountFormat` | "{0} accounts" | 账户数 |
| `OneAccountFormat` | "{0} account" | 一个账户 |
| `AtRiskShareFormat` | "{0} at risk" | 行说明中的风险占比 |
| `HintText` | "Drag to move · scroll to zoom · double-click a cluster to open it" | 操作提示 |
| `ShowAllText` | "Show all" | 从簇返回的药丸按钮 |
| `ZoomInText`、`ZoomOutText`、`FitText` | "Zoom in"、"Zoom out"、"Show every account" | 缩放按钮的名称 |
| `SizeLegendText` | "Size shows MRR" | 图例中的大小项 |
| `GroupByText` | "Group accounts by" | 标签页的名称 |
| `SortByText` | "Order the list by" | MRR / 账户数切换的名称 |
| `OtherText` | "Other" | 没有取值的账户所在的簇 |
| `EmptyText` | "No accounts to show" | 没有账户时的舞台 |
| `FlyToClusterText`、`GroupAndFlyFormat` | "Fly to this cluster"、"Group by {0} and fly to this cluster" | 行对屏幕阅读器的操作 |
| `KeyboardHelpText`、`StageSummaryFormat`、`OverviewLayoutText`、`GroupedLayoutFormat` | （句子） | 舞台的屏幕阅读器名称与帮助 |
| `GroupAnnouncementFormat`、`AccountAnnouncementFormat`、`GroupingAnnouncementFormat`、`OverviewAnnouncementText` | （句子） | 实时播报 |

### 数据结构与性能

- 控件在数据变化时读取每个账户的 `Id`、`Name`、`Mrr`、`Health`、`Owner`、`Note` 以及 `Groups`，并在自己的一份副本上以平坦数组工作；旋转星系的每一帧都不分配内存。
- 在无头 Skia 测试环境中，于一台开发用笔记本电脑上，2,400 个点每帧绘制约需 3 ms；移动分组或镜头只多花费不到 0.1 ms。在浏览器（WebAssembly）中预计会是其数倍，因此请把数据控制在几千个账户以内。
- 只绘制舞台之内的点，因此放大后的视图绘制的点很少。
- 舞台仅在有东西在移动时才请求帧。指针停留在舞台上时、离开屏幕时，以及在减少动效模式下（此时星系是一张静止的图片）都会停止。

## API 参考

<!-- api: CustomerGalaxy -->

<!-- api: GalaxyAccount -->

<!-- api: GalaxyGrouping -->

<!-- api: GalaxyGroupDefinition -->

<!-- api: GalaxyHealthState -->

## 无障碍

- 舞台是一个可获得焦点的自定义控件，被描述为“交互式图表”。它的名称概括了视图（“Customer galaxy of 2,400 accounts and $6.05M in monthly recurring revenue, grouped by plan …”），其帮助文字列出了按键（`KeyboardHelpText`）。焦点环位于舞台外侧，宽 2 px，且只在键盘焦点时显示。
- 一个隐藏的表格代替画布：当前分组中每个簇一行，包含其账户、MRR，以及每种健康状态下的账户数。
- 一个礼貌式实时区域会播报聚焦的簇（“Corporate, 70 accounts, $2.80M MRR, $60K at risk.”，并带上你的数字）、用方向键选中的账户，以及新的分组或重置（“4 clusters by plan. Corporate is the largest …”）。
- 分组标签页是一个标签列表：只有被选中的标签是 Tab 停靠点，方向键、Home 和 End 可以改变选择。MRR / 账户数按钮会报告其是否处于按下状态；列表中的每一行都是一个按钮，报告其簇是否被聚焦，其名称为簇名、MRR、账户数和风险占比，其操作则作为帮助文字。
- 缩放按钮都有名称；当视图已显示全部内容时，适应按钮被禁用，并会被 Tab 跳过。
- 滚动数字只朗读一次，读出的是最终值。
- 颜色从来不是唯一的信息：提示卡和列表都会写出每种健康状态的名称，图例也为各颜色加了标签。

### 键盘

| 按键 | 位置 | 操作 |
|---|---|---|
| `Tab` | 任意位置 | 标签页、舞台、“显示全部”、缩放按钮、MRR / 账户数、列表行 |
| `ArrowLeft` / `ArrowRight` | 标签页 | 选择上一个或下一个分组（循环）；`Home` / `End` 选择第一个或最后一个 |
| `ArrowRight` / `ArrowLeft` | 舞台，已分组 | 按 MRR 飞入下一个或上一个簇（循环；先最大或最小的） |
| `ArrowDown` / `ArrowUp` | 舞台 | 在所聚焦的簇中按 MRR 选择下一个或上一个账户（否则在全部账户中选择）；提示卡固定在它旁边 |
| `+`、`=` / `−` | 舞台 | 以中心为基准，按 1.6 倍放大或缩小 |
| `0`、`Home` | 舞台 | 返回所有簇 |
| `Escape` | 舞台 | 清除选择；再按一次则返回所有簇 |
| `Enter`、`Space` | 行、按钮 | 选择该行（飞入其簇）或按下该按钮 |

## 动效

- 总览以及每个簇都以每秒 1.6° 顺时针旋转（约 224 s 转一圈），总览中的每个点还会在几分钟内漂移几个像素。指针停留在舞台上时，旋转会缓缓停止（约 0.4 s），舞台也停止绘制；指针离开后，旋转恢复。
- 分组变化时，每个点都会弹向它在新布局中的位置：约 70% 立即出发，其余的最多延迟三分之一秒；每个点先缓缓游荡，然后螺旋地飞入，飞行时略微膨胀。大多数点会在一秒之内到位。镜头以相同的节奏适配新布局，新标签则在 120 ms 之后升起就位。
- 飞入簇、“显示全部”以及适应，镜头沿一个缓慢的临界弹簧运动（约 1 s）；滚轮、按钮和键盘缩放则使用更快的弹簧（约 0.55 s），并保持指针下的点不动。拖动则严格跟随指针。
- 其他簇的变暗在约四分之一秒内淡入到七分之一，分 7 步完成；被悬停的点会放大到 1.84 倍，并在其周围显示一个 3 px 的环。
- 提示卡淡入，并从 96% 的大小放大；数字逐位滚动，从左到右相隔 26 ms；条形及其段缓动到新宽度；重新排序的行滑动到各自的位置；标签页的滑块滑动。
- 减少动效时，只绘制一张静止的星系：不旋转、不漂移，每次分组变化、飞行、缩放、变暗和悬停都立即到位；提示卡和标签只淡入淡出。关闭动效时，所有变化立即生效。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 总览与簇旋转，点漂移 | 1.6°/s，指针停留时缓停 | 静止 | 静止 |
| 分组变化 | 每个点以错开的起点弹向其位置 | 立即到位 | 立即 |
| 飞入簇、“显示全部”、适应 | 临界弹簧，约 1 s | 立即到位 | 立即 |
| 滚轮、按钮或键盘缩放 | 临界弹簧，约 0.55 s，以锚点为中心 | 立即到位 | 立即 |
| 拖动超出边缘后松开 | 橡皮筋式回弹 | 橡皮筋式回弹，直接回到内部 | 橡皮筋式回弹，直接回到内部 |
| 其他簇变暗 | 约 0.26 s 淡入，7 步 | 立即 | 立即 |
| 悬停的点 | 约 0.4 s 放大到 1.84× | 立即 | 立即 |
| 簇标签 | 上升 4 px 并在 120 ms 后淡入；240 ms 内变暗至 30% | 淡入淡出 | 立即 |
| 提示卡 | 淡入，并从 96% 放大 | 淡入淡出 | 立即 |
| 数字 | 数字逐位滚动，相隔 26 ms | 立即变化 | 立即 |
| 条形及其段 | 缓动到新宽度（580 ms） | 立即变化 | 立即 |
| 排序切换后重新排序的行 | 滑动到各自位置（约 0.55 s） | 直接跳转 | 立即 |
| 标签页滑块 | 在灵敏的弹簧上滑动 | 直接跳转 | 立即 |
| 操作提示 | 480 ms 内淡出 | 快速淡出 | 立即 |

## 相关组件

- `BarChart`、`DonutChart` 和 `Treemap`，用于少量精确数值或整体的各部分。
- `DataGrid` 和 `SortableDataTable`，用于逐行阅读和比较账户。
- `MetricCard` 和 `AnimatedCounter`，用于一个醒目的数字。
- `SegmentedControl`，分组标签页与它相似。

| 组件 | 适用场景 |
|---|---|
| `Treemap` | 需要整体各部分的精确面积，且不要堆叠的空隙 |
| `BarChart` | 按一个数值比较少量类别 |
| `DataGrid` | 人们需要自己阅读、排序和编辑账户 |
| `MetricCard` | 仪表板的一块磁贴，展示一个数字及其变化 |
| `ActivityHeatmap` | 按天和小时的活动比谁是客户更重要 |
