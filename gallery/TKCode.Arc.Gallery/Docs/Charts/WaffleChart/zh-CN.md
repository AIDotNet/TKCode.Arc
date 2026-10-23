---
title: 华夫图
summary: 十乘十的单位图表，每个方格代表百分之一；数据变化时，方格会飞到各自的新分组。
source-hash: 8db60ae92ac0
---

<Demo name="EnergyMix" />

## 何时使用

华夫图把整体显示为可以逐个数清的单位网格，让读者一眼看出 100 个方格中每个分组各占多少。它适用于汇总卡片或仪表板磁贴，关心的问题是“这占多少份额”，并且即使份额很小也必须清楚可见。默认情况下每个方格代表百分之一，网格下方或旁边的图例给出精确数字。

- **账单摘要中的发票状态**：在 Acme Analytics 账号页中，本月发票中已付款、待处理、逾期和失败各占的份额。读者数方格比比较饼图的扇形更快。
- **运营仪表板中的支持工单构成**：未处理工单按优先级或渠道划分的占比。小份额（例如 4% 的紧急工单）仍会显示为一个可见的方块，而不是一条细缝。
- **报告中的能源或预算构成**：按来源划分的发电量占比，或按成本中心划分的部门预算占比，并与上一年的同一图表并排显示。保持分组不变的方格原位不动，因此两组数据之间的变化会被读作移动。
- **管理控制台中的配额与席位使用**：某个工作区的 500 个席位中已用 412 个。5 乘 20 的网格让每个方格代表一个席位，当单位是席位、许可证或积分时，它比百分比更有用。
- **产品调研中的问卷答案**：某个问题的“是、否、不确定”划分，份额保留一位小数。每个答案都是一个可读的分组，网格不需要坐标轴。

## 何时不用

- **随时间变化的占比**：趋势用 `LineChart`，多个占比同时变化时用 `Streamgraph`。华夫图只展示某一时刻，因此没有地方容纳一系列日期。
- **类别很多且必须精确比较**：用 `BarChart`。超过约六个类别后，较小分组的方格会少到难以比较，而且颜色和形状开始重复。
- **需要下钻的嵌套整体中的某一部分**：用 `Treemap`，它能展示嵌套关系，并让读者从分组进入其组成部分。
- **一个占比且中间显示总数**：用 `DonutChart`，它把总数放在圆环中心，在狭窄的空间中也很清晰。
- **单个任务的完成度或配额与上限的对比**：任务用 `Progress`，带套餐上限的配额用 `UsageMeter`。两者都只给出一个值和一个清晰的上限，这是方格网格无法同样直接展示的。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WaffleChart Label="发电量"
                     Unit="TWh"
                     ItemsSource="{Binding Mix}" />
</UserControl>
```

`WaffleChart` 派生自 Avalonia 的 `Control`，由它自己绘制方格，包含一个可聚焦的网格以及每个类别一行的图例。它不是项控件：类别来自 `ItemsSource` 或 `Items`，由图表负责绘制。你可以像对待其他控件一样设置尺寸和布局，图表自带键盘模型和无障碍摘要。

类别来自 `ItemsSource` 或内联的 `Items` 集合。设置了 `ItemsSource` 时，`Items` 会被忽略。

```csharp
var neutral = new WaffleChart { Label = "浏览器占比", AccentKey = WaffleChart.NoAccent };
neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
neutral.Items.Add(new WaffleCategory("other", "其他", 16.0));
```

图表本身不绘制标题。请在它上方的 `TextBlock` 中放置标题，并把同样的文字传给 `Label`，后者是辅助技术朗读的名称。

### 结构

1. **网格**（`Rows` 乘 `Columns`）：方格。每个方格代表整体的一个单位，默认的 10 乘 10 使每个方格对应一个百分点。10 乘 10 时网格为正方形，方格之间间隔 3 px，最宽 300 px。
2. **图例**（`IsLegendVisible`）：每个类别一行，包含 10 px 的色块、类别名称和占比。占比使用中等字重和主文字颜色，名称使用次要文字颜色。每行至少 36 px 高，行与行之间相隔 2 px。
3. **图例位置**：图表宽 420 px 或以上时，图例位于网格旁边，网格最宽 232 px，两者间距 24 px。较窄时图例位于网格下方，网格居中，图例最宽 320 px，与网格之间相隔 24 px。
4. **光标**：指针或键盘读数所在的方格。它有 2 px 的表面色间隙，以及 55% 不透明度、1.5 px 的前景色描边环。
5. **工具提示**：一张卡片，第一行是色块、类别名称和占比，第二行是格式化后的数值（带 `Unit`），使用弱化文字颜色。它显示在方格上方；上方没有空间时显示在下方。
6. **空状态文字**（`EmptyLabel`）：没有数据时，在网格的位置居中显示弱化文字。空图表高 160 px。

方格的形状在颜色之外还承载了第二个通道。强调类别是圆角半径为 22% 的方形方格。中性类别依次循环使用方形、圆形和带细内圈的方形，因此色调相近的两个类别在屏幕上以及灰度下仍然可以区分。

### 选择网格

网格容纳 `Rows` 乘 `Columns` 个方格，每个类别都按其数值的比例获得整数个方格。请选择让每个方格都有可数意义的尺寸。

| 单位 | 网格 | 原因 |
|---|---|---|
| 整体的百分比 | 10 乘 10（默认） | 一个方格代表一个百分点，读者无需帮助即可数清。 |
| 套餐中的席位或许可证 | 5 乘 20 或 10 乘 50 | 一个方格代表一个席位或一个配额单位。网格保持宽而矮。 |
| 保留一位小数的问卷答案 | 10 乘 10，并设置 `Decimals="1"` | 方格仍代表百分比，精确数字由图例给出。 |

方格按最大余数法分配。分配结果总是加总为网格的总格数，因此三个类别的值都为 1 时，会得到 34、33 和 33 个方格。数值之和不必为 100：图表会根据总数计算每个占比。负值按零计。

### 强调色与颜色

`AccentKey` 指定使用第一个系列颜色的类别。未设置时，第一个类别即为强调类别。接下来的三个类别依次使用次要系列颜色，之后的类别使用中性色调。

<Demo name="Neutral" />

```xml
<arc:WaffleChart Label="配额使用情况" Rows="5" Columns="20" AccentKey="used" ItemsSource="{Binding Quota}" />
```

将 `AccentKey` 设为你希望突出的类别，例如逾期的发票或失败的付款。拥有自己 `Color` 的类别会保持该颜色，与其排位无关。运行时修改 `AccentKey` 会原地重新着色：方格不会移动，而是以与数据变化相同的错落节奏改变颜色。

`WaffleChart.NoAccent` 取消强调色。前三个类别仍依次使用次要系列颜色，只有第四个及之后的类别使用中性色调。当各类别是地位对等的项时使用它，例如上面的浏览器占比示例，并预期前三个类别会有颜色。

调色板共有八种可区分的色调：强调色、三种次要系列颜色和四个中性色阶。超过八个类别后色调会重复，形状每三个中性类别循环一次。在达到这一点之前，请把小类别合并为一个“其他”。

### 数据集

替换 `ItemsSource` 即可切换数据集。保留所在分组的方格原位不动，其余方格飞到各自的新区块。类别由其 `Key` 标识，因此请在各数据集之间保持键稳定：2015 年的 "coal" 与 2023 年的 "coal" 是同一批方格，而 "Coal (2015)" 则是一个新分组。

```csharp
energy.ItemsSource = Mix2015;
```

将 `ItemsSource` 设为新集合会让图表重建一次。若集合会发出变更通知，例如 `ObservableCollection<T>`，那么在它被挂接期间，每次添加、移除或替换都会更新图表。

### 选择器与非类别数据

当数据源不是 `WaffleCategory` 时，设置 `KeySelector` 与 `ValueSelector`。`LabelSelector` 读取每个项的标签，`Decimals` 设置占比显示的小数位数。

<Demo name="Selectors" />

```xml
<arc:WaffleChart Label="问卷答案"
                 KeyMemberBinding="{Binding Name}"
                 ValueMemberBinding="{Binding Votes}"
                 ItemsSource="{Binding Answers}" />
```

选择器优先于对应的成员绑定，成员绑定又优先于 `WaffleCategory` 项的属性。项没有标签时，标签即为其键。项没有键时，控件使用项的 `ToString()`，因此请为每个项提供键。

### 固定类别

固定的类别会保持其方格高亮，并让其他类别变暗。点击图例行可固定该类别，再次点击则释放。`ActiveKey` 保存被固定的键，默认为双向绑定，因此视图模型既可以读取固定状态，也可以通过代码设置它。

```csharp
energy.ActiveKeyChanged += (_, e) => pinned.Text = $"已固定：{e.Category?.Label ?? "无"}";
```

固定某个类别后，悬停或聚焦到另一个类别会改为显示那个类别；释放固定后，高亮会回到被固定的类别。

### 空状态

没有类别，或总数为零时，网格会以 `EmptyLabel` 取代方格。空状态下网格不可聚焦，图例也会隐藏。

<Demo name="Empty" />

### 绑定到视图模型

带有项和固定键的图表属于视图模型的职责。视图模型拥有类别和固定状态，图表负责展示它们。最关键的是 `ItemsSource` 与 `ActiveKey` 两个绑定：`ActiveKey` 默认为双向绑定，因此点击图例行会把键写回视图模型，视图模型也可以通过设置它从代码中固定某个类别。

```csharp
public sealed partial class InvoiceSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<WaffleCategory> _statuses = [];

    [ObservableProperty]
    private string? _pinnedStatusKey;

    [RelayCommand]
    private void PinOverdue() => PinnedStatusKey = "overdue";
}
```

```xml
<arc:WaffleChart Label="发票状态，2026 年 3 月"
                 Unit="张发票"
                 ItemsSource="{Binding Statuses}"
                 ActiveKey="{Binding PinnedStatusKey}" />
```

数据变化时请整体替换 `Statuses` 列表，而不是逐项编辑。集合的每次变化都会重建图表，因此逐项变化的列表每个项都要付出一次重建的代价。只有当列表需要原地编辑、并且每次编辑都应立即显示时，才使用 `ObservableCollection<T>`。

### 数据结构与性能

每个项都会成为一个 `WaffleCategory`，包含 `Key`、`Label`、`Value` 以及可选的 `Color`。每次变化都会重新读取项、分配方格并移动网格，因此开销随方格数（默认 100）和类别数增长。图表是一个控件、一个绘制表面，而不是每个方格一个控件。请按读者能跟上的节奏刷新数据，例如仪表板上每分钟一次；如果某个值每秒变化多次，请先将其平滑后再交给图表。

占比由数值的总和计算，因此请传入原始计数或金额，而不是已经取整的百分比。请保证源数据中的值为有限的非负数：`NaN` 或无穷大会破坏总和，负值则按零计。

### 示例：账号页上的账单摘要

Acme Analytics 账号页上的一张账单摘要卡片，展示 2026 年 3 月开出的发票状态。逾期分组是强调色，因为它是财务团队需要处理的那一组。卡片较宽，因此图例放在网格旁边。标题是图表上方的 `TextBlock`，图表的 `Label` 则为辅助技术重复该标题。占比保留一位小数，使 6.9% 的逾期占比与 8.4% 的待处理占比可以区分。

视图模型以不可变列表保存各状态。每个新月份都会替换整个列表，因此图表只重建一次，方格飞到各自的新分组。

```csharp
public sealed record InvoiceStatusCount(string Key, string Label, double Count);

public interface IInvoiceService
{
    Task<InvoiceStatusCount[]> CountByStatusAsync(DateOnly month);
}

public sealed partial class InvoiceSummaryViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceSummaryViewModel(IInvoiceService invoices) => _invoices = invoices;

    [ObservableProperty]
    private IReadOnlyList<WaffleCategory> _statuses = [];

    [ObservableProperty]
    private string? _pinnedStatusKey;

    [RelayCommand]
    private async Task LoadAsync(DateOnly month)
    {
        var counts = await _invoices.CountByStatusAsync(month);
        Statuses = counts.Select(c => new WaffleCategory(c.Key, c.Label, c.Count)).ToList();
    }
}
```

![2026 年 3 月的发票状态，逾期分组为强调色，图例位于网格旁边](images/InvoiceStatus.png)

```xml
<Border Width="620"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Invoices, March 2026"
                       FontSize="16"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Acme Analytics, 1,250 invoices issued this month"
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:WaffleChart Label="Invoice status, March 2026"
                         Unit="invoices"
                         Decimals="1"
                         AccentKey="overdue">
            <arc:WaffleChart.Items>
                <arc:WaffleCategory Key="paid" Label="Paid" Value="1012" />
                <arc:WaffleCategory Key="pending" Label="Pending" Value="105" />
                <arc:WaffleCategory Key="overdue" Label="Overdue" Value="86" />
                <arc:WaffleCategory Key="failed" Label="Failed" Value="47" />
            </arc:WaffleChart.Items>
        </arc:WaffleChart>
        <TextBlock Text="Updated 31 March 2026, 09:00 UTC"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</Border>
```

### 文案规范

标签和单位会被朗读，也会显示在工具提示中，因此请像书写同一数据的表格那样书写。

| 不要写 | 改为 |
|---|---|
| `No data`（账单卡片上的默认空状态文字） | `No invoices in this period` |
| `Chrome Browser Application Version 124` | `Chrome`（屏幕上会缩短过长的名称，完整名称保留在工具提示中） |
| `Category A`、`Group 2` | 真实的类别：`Paid`、`Overdue`、`Wind and solar` |
| 单位写作 TWH 或 twh | TWh，按源数据的写法书写单位 |
| 只用 Chart 之类的泛称作为标签 | `Invoice status, March 2026`，说明整体是什么 |
| 每个图表都用 `Decimals="2"` | `Decimals="0"`，只有两个占比四舍五入后相同时才用一位小数 |

类别名称使用标题式大小写，图表的 `Label` 使用句子式大小写。类别名称要简短：图例会缩短放不下的名称，而被缩短的名称更难与工具提示对应。

### 最佳实践

**推荐**

- 类别数保持在六个或以内。超过八个后色调会重复，读者无法仅凭颜色或形状区分各分组。
- 在各数据集之间使用稳定的 `Key` 值，使方格原位不动，让两组数据之间的变化读作移动。
- 把 `AccentKey` 设为读者应当采取行动的那一个类别。单一强调色为视线提供了一个起点。
- 在 `TextBlock` 中为图表加上标题，并在 `Label` 中使用相同的文字，让看得见的用户和辅助技术用户看到同一个问题。
- 当原始值需要特定文化或 `Unit` 无法表达的单位时，传入 `ValueFormatter`。原因是：默认的数值文字使用 en-US 的千位分隔符。

**避免**

- 用图表做类别之间的精确比较。华夫图能很好地读出占比，但柱状图更擅长读出两组之间的差异，而且方格会把每个占比四舍五入到最近的一个方格。
- 负值，图表会将其按零计。净额（例如抵消一笔付款的退款）在进入图表之前需要有自己的值。
- 在实时仪表板上于运行时修改 `Rows` 或 `Columns`。方格数量会变化，因此每个方格都被一次性重新分配，数据变化的动效也就失去了。
- 没有标题的图表。图表只绘制网格和图例，读者无法知道这些方格在数什么。
- 在没有其他元素显示数字时隐藏图例（`IsLegendVisible="False"`）。方格显示的是整体的形状，只有图例给出每个类别的名称和占比。

### 常见问题

**我设置了 `Items`，图表却显示 "No data"。** `ItemsSource` 优先。设置了 `ItemsSource` 时，内联的 `Items` 集合会被忽略，因此请只设置 `ItemsSource`，或只向 `Items` 添加，不要两者都用。

**一个类别的方格为什么比我预期的少一个？** 方格按最大余数法分配。每个类别先获得其占比的整数个方格，然后剩余的方格分给小数部分最大的类别。三个相等的类别会得到 34、33 和 33 个方格，因为第一个类别拿到了剩余的那个方格。

**如何让某个类别无论位置如何都保持颜色？** 为该类别设置它自己的 `Color`，或把 `AccentKey` 设为它的键。拥有 `Color` 的类别会保留该颜色；强调色无论出现在数据的哪个位置，都取第一个系列颜色。

**如何在代码中读取被固定的类别？** 处理 `ActiveKeyChanged`，读取 `e.Key` 或 `e.Category`，释放固定时它们为 null。或者像上面的示例那样，将 `ActiveKey` 双向绑定到视图模型的属性。

**为什么在我的德语应用中，占比用点号作为小数分隔符？** 控件按 en-US 规则格式化占比，默认的数值文字也是如此。请设置 `ValueFormatter`，用你自己的文化格式化原始值。图表没有用于占比的格式化器，因此当你需要本地化的占比时，请读取 `Categories` 与 `CellCounts`，并在图表旁边显示你自己的文字。

## API 参考

<!-- api: WaffleChart -->

<!-- api: WaffleCategory -->

| 成员 | 类型 | 说明 |
|---|---|---|
| `Key` | `string?` | 被固定的类别键；释放固定时为 null |
| `Category` | `WaffleCategory?` | 被固定的类别；释放固定时为 null |

## 无障碍

- `Label` 为图表命名。网格是一个可聚焦的组，带有角色描述和按键说明。
- Tab 键先将焦点移到网格，然后依次移到每个图例行。每个图例行都是带文字名称的切换按钮。
- 在网格中，方向键每次将读数移动一个方格。PageUp 与 PageDown 跳到上一个或下一个类别的第一个方格，Home 与 End 分别跳到第一个和最后一个已填充的方格。Escape 清除读数。
- 在图例行上按 Enter 或空格键，可固定或释放其类别。
- 礼貌型实时区域会朗读读数下方的类别、占比和数值。一个隐藏的数据表列出每个类别的数值、占比和方格数。
- 方格对辅助技术隐藏。屏幕上会缩短较长的图例名称，但完整名称保留在工具提示、实时区域和数据表中。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移入网格，读数从第一个方格开始，然后移到每个图例行。空图表会被跳过 |
| 方向键 | 将读数上、下、左或右移动一个方格；读数在网格边缘处停止 |
| `PageDown` / `PageUp` | 跳到下一个或上一个类别的第一个方格。在类别中间时，`PageUp` 会跳到该类别的第一个方格 |
| `Home` / `End` | 跳到第一个方格，或最后一个已填充的方格 |
| `Escape` | 清除读数 |
| `Enter` / `Space`（图例行） | 固定该类别；若已固定则释放 |

读数会播报类别名称、占比和数值，例如 "煤炭，35%，35 TWh"。

## 动效

- 方格尽量保留原位。其余方格以错落的波浪形飞到新区块，飞行中的方格会略微缩小并改变颜色。
- 图例占比以临界阻尼弹簧平滑计数到新值。
- 首次显示时，方格从左下角依次出现。
- 减弱动效时，方格和占比立即到位，并取消缩小、出现和错落效果。关闭动效的表现与减弱动效相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 数据集替换 | 保持类别不变的方格原位不动；其余方格以错落的波浪飞入，缩小到 62% 的比例并改变颜色 | 立即就位 | 立即就位 |
| 图例占比 | 以平滑弹簧滚动到新值 | 立即设置 | 立即设置 |
| 首次显示 | 方格从左下角逐列出现，从 40% 放大，并带有错落的淡入 | 立即显示 | 立即显示 |
| 被固定或聚焦的类别 | 其他类别在标准时长内淡化到 26% 不透明度 | 立即变化 | 立即变化 |
| 强调色变化 | 方格以与数据变化相同的错落节奏改变颜色 | 立即变化 | 立即变化 |

## 相关组件

- `DonutChart`：单个圆环中的占比，中心显示总数。
- `BarChart`：类别之间的精确比较。
- `Progress`：单个任务的完成进度；`UsageMeter`：配额相对上限的用量。
- `Treemap`：类别呈嵌套结构且需要下钻时使用。

| 组件 | 适用场景 |
|---|---|
| `DonutChart` | 一个占比，中心显示总数，空间较窄 |
| `BarChart` | 读者必须精确比较类别的大小 |
| `LineChart` / `Streamgraph` | 占比随时间变化 |
| `Treemap` | 类别呈嵌套结构且需要下钻 |
| `Progress` / `UsageMeter` | 一个任务的完成度，或一个配额相对其上限 |
