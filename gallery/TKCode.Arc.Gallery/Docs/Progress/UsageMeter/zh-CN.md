---
title: 用量表
summary: 显示什么占用了配额，以及距离上限还有多少。
source-hash: dfff86c647a4
---

<Demo name="Storage" />

## 何时使用

`UsageMeter` 显示一组类别如何占满一个固定的配额，以及总量离上限有多远。它适用于用量或账单页面：人可以一眼看清剩余额度、状态以及哪些内容占用了套餐，无需查看表格。它不用来表示朝完成方向推进的任务，也不用来比较没有上限的类别。

- **账单页面中的存储**：媒体、文档、备份和其他文件相对于套餐内包含的存储空间。图例列出每个类别，人在删除任何内容之前就能看到媒体占用了最多的空间。
- **用量报告中的流量或 API 调用**：月度配额，例如 500 GB 的出站流量，当徽标在 90% 时切换为“即将占满”，团队可以在触及上限前采取行动。
- **套餐中的席位或项目**：一个团队有 20 个席位，分布在工程、设计和销售中。总量超过上限时，超出部分以数字和斜纹条形显示，因此套餐升级提示有一个明确的金额可以参考。
- **管理控制台中的用量提醒**：工作区所有者打开用量面板，就能看到哪个工作区超出了配额，超出量直接写在徽标里。
- **内部平台中每个租户的配额**：在租户详情页上，把每个租户的构建分钟数或容器小时数与其配额对比显示，并与修改配额的控件放在一起。

## 何时不用

- **朝完成状态推进的工作**，例如导入或迁移：用 `Progress`。仪表没有“完成”状态，它只有一个总量可能超过的上限。
- **没有上限、彼此比较的类别**，例如各地区的收入：用 `BarChart`。用量表始终以配额为基准进行度量。
- **单个核心指标**，例如本月的活跃用户数：用于刻度上某个值时用 `Gauge`，用于带趋势的数字时用 `MetricCard`。
- **类别超过少数几个**，例如一份包含五十个成本中心的台账：用 `Treemap` 或排好序的 `SortableDataTable`。条形在大约四个强调色阶内仍然易读，五十条图例对人的决策没有帮助。
- **没有配额的整体占比**，例如流量来源的构成：用 `DonutChart`，它只显示比例，没有上限线。

## 用法

为仪表设置 `Label`、`Limit` 和各个类别。每个类别是一个 `UsageSegment`，包含 `Id`、`Label` 和 `Value`，`Value` 与上限使用相同的单位。保持 `Id` 稳定，这样列表变化时每个类别都能保留自己的动效。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UsageMeter Label="Workspace storage" Limit="50" Unit="GB">
        <arc:UsageSegment Id="media" Label="Media" Value="14.2" />
        <arc:UsageSegment Id="docs" Label="Documents" Value="9.6" />
        <arc:UsageSegment Id="backups" Label="Backups" Value="6.1" />
        <arc:UsageSegment Id="other" Label="Other" Value="2.4" />
    </arc:UsageMeter>
</UserControl>
```

`UsageMeter` 派生自 Avalonia 的 `TemplatedControl`。它的模板绘制标题、状态徽标、总量、说明文字、条形和图例，仪表根据类别与上限计算状态。它本身不可获得焦点；其中的图例项接收键盘输入，因此仪表可以放在卡片里，而不占用自己的 Tab 停靠点。

### 结构

1. **标题**（`Label`）：被度量对象的名称，16 px、中等字重。需要时会换为两行，位于至少 28 px 高的标题行中。
2. **状态徽标**：28 px 高的胶囊形标签，水平内边距 12 px，图标前有 6 px 间距。总量低于预警比例时显示“{free} 剩余”；从预警比例直到上限之间显示带预警图标的“即将占满”；超过上限后显示“超出 {overage}”。静止时背景为弱化的表面色；即将占满时为预警色晕染（预警色 14% 混入表面色）；超出时为危险色晕染（危险色 12%）。
3. **数值**：显示字体、36 px 的滚动总量，使用前景色。它显示总量，或正在预览或固定的类别的数值。
4. **说明文字**：14 px 的文字，使用次要文字色，位于数值旁边并与其基线对齐。它说明数值的含义：“已用 50 GB 中的 GB”、“媒体中的 GB · 占套餐 28%”或“剩余 GB”。
5. **条形**：14 px 的轨道，两端为完整圆角，颜色为前景色以 7% 强度叠加在表面色上，因此空白部分在两种主题下都可见。已用部分是一段段强调色阶，段与段之间留 2 px 间隙。
6. **图例**：由切换项组成的网格，每个类别一项，外加一项剩余量（`FreeLabel`）或超出量（`OverLabel`）。每列至少 108 px 宽，列数铺满整个宽度。每项有 8 px 内边距、12 px 圆角、12 px 色块、14 px 标签，以及 16 px 的滚动数值，数值旁边显示单位。
7. **焦点环**：2 px 的焦点环轮廓，绘制在图例项外侧 4 px 处，圆角为 16 px。

标题行、数值、条形和图例之间的垂直间距为 16 px。图例向左偏移 8 px，使色块与标题对齐。条形与图例的宽度与仪表相同。

### 即将占满与超出上限

徽标显示剩余量。上限的 90%（默认 `WarnThreshold` 为 0.9）时，它变为带预警图标的“即将占满”；超过上限后，它显示超出量。`WarnThreshold` 决定触发预警的比例，同一比例也驱动代码中的 `Near` 状态。

<Demo name="AlmostFull" />

<Demo name="OverTheLimit" />

```xml
<arc:UsageMeter Label="API calls this month"
                Limit="100"
                Unit="K"
                WarnThreshold="0.8" />
```

超过上限后，条形上会叠加斜纹，并在旁边显示上限标记，因此不依赖颜色也能读出超出部分。图例的最后一项从剩余量变为超出量，标签由 `OverLabel` 指定。徽标和图例的措辞要与套餐保持一致：“超出 8.1 GB”告诉人他们将为什么付费，或必须删除什么。

### 绑定到视图模型

在 MVVM 应用中，视图模型负责管理类别、上限以及套餐允许的规则，仪表只负责显示。使用 CommunityToolkit.Mvvm 时，下面的视图模型把类别保存在 `ObservableCollection<T>` 中，由用量数据源更新每个类别的 `Value`。仪表同时监听集合和每个类别，因此条形、总量、徽标和图例都会随每次变化更新。

```csharp
public partial class StorageUsageViewModel : ObservableObject
{
    public StorageUsageViewModel()
    {
        Categories = new ObservableCollection<UsageSegment>
        {
            new UsageSegment("media", "Media", 14.2),
            new UsageSegment("docs", "Documents", 9.6),
            new UsageSegment("backups", "Backups", 6.1),
            new UsageSegment("other", "Other", 2.4)
        };
    }

    public ObservableCollection<UsageSegment> Categories { get; }

    [ObservableProperty]
    private double _limit = 50;

    [ObservableProperty]
    private string _unit = "GB";

    public void ApplyUsage(IReadOnlyDictionary<string, double> usage)
    {
        foreach (var category in Categories)
        {
            if (category.Id is not null && usage.TryGetValue(category.Id, out var value))
            {
                category.Value = value;
            }
        }
    }
}
```

```xml
<arc:UsageMeter Label="Workspace storage"
                ItemsSource="{Binding Categories}"
                Limit="{Binding Limit}"
                Unit="{Binding Unit}" />
```

- `ItemsSource` 优先于内联的 `Segments`。绑定的是 `UsageSegment` 对象的集合，而不是数字列表，这样每个类别都保有自己的身份。
- `Limit` 和 `Unit` 是单向绑定。仪表不会向视图模型写回任何值。
- 类别的 `Value` 是应用程序唯一写入的值。每次 `Value` 变化都会重新布局条形，并重新计算总量和状态。应在数字变化时设置 `Value`，而不是在数据源每个计时器周期都设置（即使数值没有变化）。
- `Status` 和 `Total` 是只读的。页面需要做出反应时（例如显示升级提示）在代码中读取它们。
- 编译绑定需要在视图上设置 `x:DataType`；这里的示例为简洁起见省略了它。

### 数据结构与性能

每个类别是一个 `UsageSegment`，包含 `Id`、`Label` 和 `Value`。`Id` 是类别在多次更新之间的身份标识：数值变化时保持它不变，这样色段会重新排布，而不是被替换。`Id` 应来自你的数据（类别键或数据库标识符），而不要用标签，因为人重命名类别时标签可能会变。

- 四个类别便于阅读。强调色阶共四个色调（完整的强调色，然后是混入表面色 62%、36% 和 22% 的颜色），因此第五个类别很难与相邻类别区分。把最小的类别合并为“其他”后再传给仪表。
- 图例是网格，类别越多只会增加行数，而不会挤压条形。标签应保持简短，因为每个图例项都在至少 108 px 宽的列中显示完整标签。
- 总量按 `Decimals` 取整（默认 1，允许 0 到 6）。当某个类别需要仪表不显示的单位（例如货币）时，在数据层完成格式化。仪表在所有文化中都使用句点作为小数分隔符，因此应把本地化的单位放在 `Unit` 中，把本地化的标签放在 `Label` 中。
- 每次 `Value` 变化都会刷新整个仪表。只写入发生变化的数值，而不是在数据源的每个周期都写入所有类别。

### 单位与小数位

`Unit` 显示在每个数值之后。`Decimals` 设置小数点后的位数。对于席位等整数计数，使用 `Decimals="0"`。

```xml
<arc:UsageMeter Label="Seats" Limit="20" Decimals="0">
    <arc:UsageSegment Id="eng" Label="Engineering" Value="12" />
    <arc:UsageSegment Id="design" Label="Design" Value="6" />
    <arc:UsageSegment Id="sales" Label="Sales" Value="5" />
</arc:UsageMeter>
```

每个仪表选择一种精度。以 GB 为单位的存储仪表保留一位小数读起来最好，这样 0.1 GB 的上传是可见的；席位数带小数位则会让人以为是错误。上限文字会去掉末尾的零，因此上限为 50.0 时显示为“50 GB”。

### 剩余与超出金额的标签

`FreeLabel` 指定图例中剩余配额的名称（默认为“Free”），`OverLabel` 指定总量超过上限后超出量的名称（默认为“Over limit”）。使用与套餐用语一致的普通名词。

```xml
<arc:UsageMeter Label="Projects"
                Limit="10"
                FreeLabel="Remaining"
                OverLabel="Above plan" />
```

### 在代码中读取状态

读取 `Status` 获取当前状态（`Ok`、`Near` 或 `Over`），读取 `Total` 获取所有类别的总和。状态与徽标使用相同的规则：达到上限的预警比例时为 `Near`，总量高于上限本身时为 `Over`。

```csharp
if (meter.Status == UsageMeterStatus.Over)
{
    ShowUpgradePrompt();
}
meter.ClearPin(); // clears a category pinned from the legend
```

### 示例：账单页面中的套餐用量

SaaS 管理控制台中的一个账单页面展示本月的三项配额：工作区存储在套餐范围内，剩余 17.7 GB；本月的出站流量即将占满，已达 91.5%，因此在触及上限前发出预警；团队沙盒的存储超出上限 2.2 GB。每个标题都写明了被度量的对象，单位与套餐表格中使用的单位一致，说明文字写明了上限，因此人无需打开套餐详情就能判断每种状态。三种状态使用三种不同的徽标，超出部分有斜纹，因此即使没有颜色页面也能读懂。

![账单页面上三个用量表，分别处于正常、即将占满和超出状态](images/PlanUsage.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="28">
        <TextBlock Text="Plan usage, October 2026"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:UsageMeter Label="Workspace storage" Limit="50" Unit="GB">
            <arc:UsageSegment Id="media" Label="Media" Value="14.2" />
            <arc:UsageSegment Id="docs" Label="Documents" Value="9.6" />
            <arc:UsageSegment Id="backups" Label="Backups" Value="6.1" />
            <arc:UsageSegment Id="other" Label="Other" Value="2.4" />
        </arc:UsageMeter>
        <arc:UsageMeter Label="Egress this month" Limit="500" Unit="GB">
            <arc:UsageSegment Id="dashboards" Label="Dashboards" Value="280.4" />
            <arc:UsageSegment Id="exports" Label="Exports" Value="176.9" />
        </arc:UsageMeter>
        <arc:UsageMeter Label="Team sandbox storage" Limit="20" Unit="GB">
            <arc:UsageSegment Id="designs" Label="Designs" Value="12.4" />
            <arc:UsageSegment Id="recordings" Label="Recordings" Value="9.8" />
        </arc:UsageMeter>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Manage plan" Variant="Secondary" />
            <arc:Button Content="Upgrade plan" />
        </StackPanel>
    </StackPanel>
</Border>
```

把两个操作放在人需要的位置：徽标说明配额处于什么状态，页脚提供下一步操作。团队沙盒仪表以数字显示超出量（“超出 2.2 GB”），而升级决策正依赖这个数字。

### 文案规范

- **标题**：被度量对象的简短名词短语，采用句式大小写：“Workspace storage”“Egress this month”。只有当上限按此周期重置时才加句号。
- **单位**：套餐所用的简短单位，例如“GB”。它显示在每个数值之后，因此要保持简短。
- **剩余与超出标签**：除非套餐使用其他措辞，否则保留默认的“Free”和“Over limit”。同一页面中使用相同的名词。
- **说明文字与徽标**：用数字写出数值和上限。“剩余 17.7 GB”比“还有很多”更清楚。

| 不要写 | 改为 |
|---|---|
| 存储用量 | 工作区存储 |
| 空间快用完了！！ | 即将占满。剩余 4.3 GB。 |
| 严重超出限制 | 超出 2.2 GB |
| 可用空间 | 剩余 |
| 超出量：2.2 | 超出限制：2.2 GB |

### 最佳实践

**推荐**

- 让 `Value` 与 `Limit` 使用相同的单位，并在 `Unit` 中显示该单位，使每个数字都带有单位。同一个仪表中混用 GB 和 MB 会让条形失去意义。
- 选择足够早的 `WarnThreshold`，让预警来得及采取行动：对于需要一周时间才能提高的配额，像 0.8 这样较低的比例更合适。
- 当横幅必须与徽标保持一致时，在代码中读取 `Status`，而不是把同一规则再计算一遍。
- 在仪表旁边写出超出量所需的操作，例如“升级套餐”，让看到“超出”的人知道该怎么做。

**避免**

- 把仪表用于没有上限的数值。上限是作为参照绘制的，因此无限制套餐在这里没有意义；计数应使用 `MetricCard`。
- 传入许多小类别。强调色阶只有四个色调，超过四段后条形就难以辨认；应把最小的类别合并为“其他”。
- 依赖条形的颜色来表示超出。斜纹、上限标记和徽标文字承载了状态。
- 显示超出套餐所用精度的小数位。两位小数的存储仪表暗示了一种没有人能据此行动的精度。

### 常见问题

**为什么徽标显示“即将占满”而不是剩余量？** 达到预警比例时状态变为 `Near`，徽标会标明这一状态。剩余量仍显示在图例中，并保留在辅助技术读取的文字摘要里。

**为什么总量与我预期的总和不一致？** 总量是在求和之后按 `Decimals` 取整的。小数位多于 `Decimals` 的值会先相加再取整，因此应把 `Decimals` 设为套餐所用的精度，并以相同方式在数据中取整。

**如何清除从图例中固定的类别？** 再次按下已固定的项，在图例获得焦点时按 `Escape`，或在代码中调用 `ClearPin()`。

**修改上限会更新状态吗？** 会。修改 `Limit`、`Unit`、`Decimals`、`WarnThreshold` 或标签属性都会刷新仪表，因此状态、徽标和图例会立即跟随新的配额。

## API 参考

<!-- api: UsageMeter -->

<!-- api: UsageSegment -->

`UsageMeter.Status` 的状态值：

| 成员 | 含义 |
|---|---|
| `Ok` | 总量低于上限的预警比例（`WarnThreshold`） |
| `Near` | 总量达到或超过预警比例，但尚未超过上限 |
| `Over` | 总量超过上限 |

## 无障碍

- 仪表是一个以 `Label` 命名的分组。条形是一幅图像，其名称是对每个类别、总量和状态的完整文字摘要，例如“Workspace storage: 32.3 GB of 50 GB used. Media 14.2 GB, Documents 9.6 GB, Backups 6.1 GB, Other 2.4 GB. 17.7 GB free.”。
- 滚动的数字不计入控件视图，辅助技术改为朗读文字摘要，因此数值变化时不会逐位朗读。
- 状态变化以礼貌方式播报：总量越过预警比例时播报“即将占满。剩余 4.3 GB。”，超过上限时播报“超出套餐上限 8.1 GB。”。超出量增长时也会播报；同一状态内的常规变化保持安静。
- 每个图例项都是切换按钮，固定对应类别时处于按下状态，名称由类别和数值组成，例如“Media, 14.2 GB”。剩余与超出项的命名方式相同。
- 鼠标悬停或焦点落在图例项上时，会预览该类别：数值和说明文字显示其占套餐的比例，直到指针或焦点移开。
- 颜色从来不是唯一的信号：徽标同时带有文字和图标，超出部分以斜纹和上限标记显示，因此即使是灰度状态也能读出。

### 键盘

图例是一个带漫游焦点的 Tab 停靠点：上次使用的图例项持有 Tab 停靠点，方向键在各项之间移动。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 进入图例并停在上次使用的项上，或离开图例 |
| `Right` / `Down`、`Left` / `Up` | 移到下一个或上一个图例项；焦点从最后一项循环回到第一项，反之亦然 |
| `Home` / `End` | 移到第一个或最后一个图例项 |
| `Enter` / `Space` | 固定或取消固定当前焦点所在的类别；被固定的类别会在条形、数值和说明文字上保持显示 |
| `Escape` | 清除固定的类别 |

## 动效

- 仪表第一次有 40% 可见时，各类别从左到右依次生长出现。
- 之后，数值变化时每个类别会滑动到新的宽度，数字也会滚动到新值。
- 超过上限后，超出部分的斜纹和上限标记淡入。
- 悬停或聚焦某个类别时，其他类别变暗；按下则固定该类别。
- 减弱动效时，直接设置数值和条形，文字以短暂淡入的方式切换。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 仪表首次显示 | 各类别从左到右生长，0.58 s，每项间隔 70 ms，仅一次 | 直接以完整尺寸显示 | 直接以完整尺寸显示 |
| 类别数值变化 | 其色段以平滑弹簧滑动到新宽度；其后的色段在同一帧跟随 | 直接设置 | 直接设置 |
| 数字变化 | 数字朝新值滚动，方向与变化方向一致 | 直接设置，文字以短暂淡入切换 | 立即完成 |
| 类别被预览或固定 | 其他色段以快速补间变暗至 28%，图例项变暗至 45% | 立即完成 | 立即完成 |
| 用量越过上限 | 超出部分的斜纹和上限标记淡入 | 立即完成 | 立即完成 |
| 状态徽标变化 | 其宽度通过变形弹簧适配新文字；颜色晕染交叉淡化 | 文字以短暂淡入切换 | 立即完成 |

## 相关组件

- `Progress`：工作朝完成方向推进，且没有配额时使用。
- `BarChart`：没有上限、彼此比较的类别。
- `Gauge` 或 `MetricCard`：一个核心数字。

| 组件 | 适用场景 |
|---|---|
| `Progress` | 任务朝完成方向推进，且没有可超出的配额 |
| `BarChart` | 类别没有上限，并且彼此比较 |
| `DonutChart` | 只关心每个类别在整体中的占比，没有上限 |
| `Gauge` | 一个值需要对照刻度读取，例如分数或负载 |
| `MetricCard` | 一个核心数字需要标签、变化量和趋势 |
