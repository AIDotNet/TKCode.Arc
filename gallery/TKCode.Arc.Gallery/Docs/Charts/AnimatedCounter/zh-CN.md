---
title: 动画计数器
summary: 让变化的总数具有清晰的动感。
source-hash: 2c57888c6b93
---

<Demo name="RollingDigits" />

## 何时使用

`AnimatedCounter` 单独展示一个数字，字号大到一眼就能读清，并在数字变化时把各位数字滚动到新值。它的原理类似汽车的里程表：只有发生变化的数位列会转动，因此读者能看出数字变动了，大致变化多少，页面也不会闪烁，数字也不会突然跳到新的宽度。

- **账单概览中的主要总数**：SaaS 管理控制台顶部的月度经常性收入或活跃订阅数。滚动动画告诉财务团队数字已经刷新，即使他们没有看下方的图表。
- **运维控制台中的实时计数**：页面打开期间不断变化的未解决事件、排队任务或已登录会话。数据到达时设置新值，数位就会从旧值滚动到新值。
- **首次查看时的计数统计**：在落地页或季度报告中，“128,400 支团队”会在读者滚动到它时从零开始累加，由 `AnimateOnView` 完成这一效果。
- **金融科技与结账中的余额和价格**：付款后变化的钱包余额，或购物车中的累计金额。固定 `Decimals`，数字在变化时保持宽度，布局就不会跳动。
- **增长仪表板中的比率与百分比**：转化率或留存率，使用 `Suffix="%"`，并显示在它所属的总数旁边，让读者同时看到主要数字与比率。

## 何时不用

- **需要卡片、标签和变化指示的数字**：用 `MetricCard`，它把这样的计数器放进带趋势的卡片中。
- **变化的是文字而非数字**：用 `TextMorph`，它为词语和短语做动画，而不是数位列。
- **图表的主要数字**：`BarChart`、`LineChart` 等图表控件会自行更新其主要数值，因此在图表旁边另放计数器只会重复同一个数字。
- **表格行中的大量数字**：用 `SortableDataTable`。每个计数器都为自己的数位列做动画，适合少量主要数字，而不适合让人扫视和排序的一列数值。
- **由人输入或编辑的值**：用 `NumberField` 或 `MoneyInput`。计数器只负责显示数字，没有输入、解析和校验。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnimatedCounter Label="收入"
                         Prefix="$"
                         Value="{Binding Revenue}" />
</UserControl>
```

`AnimatedCounter` 派生自 Avalonia 的 `TemplatedControl`。其模板由一个标签和位于其上的 `RollingDigits` 组成，后者是 Arc 核心中负责数位滚动的基础件。计数器本身加入标签、前后缀以及首次查看时的显现效果；它的主题会设置数字的字体、字号、字重和前景色，因此你很少需要自行设置这些属性。由于它使用模板，你可以像对待其他 Arc 控件一样，用 `ControlTheme` 重新设计计数器的外观。

`Value` 接收原始数字，而不是格式化后的字符串。计数器会用 `Decimals`、`CultureName`、`Prefix` 和 `Suffix` 对它进行格式化，因此同一个值在 en-US 下可显示为“$48,250”，在 de-DE 下可显示为“48.250”，而数据本身无需改变。

### 结构

1. **标签**（`Label`）：位于数字上方的一行文字，使用弱化的文字颜色、超小字号和常规字重。它位于数字上方 8 px。`Label` 为 null 时隐藏；文字变化时会以短暂的模糊切换。
2. **数位**（`Value`）：数字本身，由 `RollingDigits` 绘制，使用展示字体、3XL 字号和中等字重，颜色为前景色。计数器默认左对齐。
3. **前缀**（`Prefix`）：数字前的文字，样式相同，例如货币符号。它是滚动文字的一部分，因此会随数字一起移动，并在宽度变化时保持自己的位置。
4. **后缀**（`Suffix`）：数字后的文字，例如百分号或单位。与前缀一样，它也是滚动文字的一部分。

计数器本身没有边框、背景或内边距。把它放在卡片、行或页头中，由布局提供底色，使它与所在页面相契合。

### 小数位与后缀

`Decimals` 固定小数点后的位数（默认为 0）。负值按 0 处理。请将其固定为读者需要的精度：转化率每次更新都显示两位小数，因此宽度保持不变，数位原地滚动。

```xml
<arc:AnimatedCounter Label="转化率" Value="{Binding Conversion}" Decimals="2" Suffix="%" />
```

使用 `Suffix` 表示跟在数字后面的单位或符号，并保持简短。较长的短语应放在标签中，那里可以换行，并且不会随数位滚动。

### 更新数值

将 `Value` 设为新的总数。只有发生变化的数位列会转动，且转动方向与数字的变化方向一致，因此增加显示为向上滚动，减少显示为向下滚动。

```csharp
revenue.Value += random.Next(100, 4000);
rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
```

保持数值与读者看到的单位一致。传入 48250 以显示“$48,250”，并让 `Prefix` 添加货币符号，而不要传入格式化后的字符串，因为计数器无法滚动字符串。

### 首次进入视图时显示

`AnimateOnView` 会在计数器首次有 60% 可见时，让每个数位从零向上滚动。之后，计数器显示其数值并照常更新，因此之后发生变化的数字只会从旧值滚动到新值。

<Demo name="RevealOnView" />

```xml
<arc:AnimatedCounter Label="首次进入视图时滚动显示" Value="{Binding Visitors}" AnimateOnView="True" />
```

显现效果适合用于作为某个区段存在理由的统计，例如落地页上的客户数量。对于读者每天都会看到的工具栏计数器，它则毫无价值，因为每次加载页面都会重复滚动。

### 由数据驱动

计数器只显示所给的数字，因此数据源决定数字、刷新计划和取整方式。视图模型保存原始值，计数器对其进行单向绑定。

```csharp
public sealed record BillingSummary(double Mrr, double Subscriptions, double Conversion);

public interface IBillingReports
{
    Task<BillingSummary> GetSummaryAsync(DateOnly asOf);
}

public partial class BillingOverviewViewModel : ObservableObject
{
    private readonly IBillingReports _reports;

    public BillingOverviewViewModel(IBillingReports reports) => _reports = reports;

    [ObservableProperty]
    private double _recurringRevenue;

    [ObservableProperty]
    private double _activeSubscriptions;

    [ObservableProperty]
    private double _conversionPercent;

    public async Task RefreshAsync()
    {
        var summary = await _reports.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today));
        RecurringRevenue = summary.Mrr;
        ActiveSubscriptions = summary.Subscriptions;
        ConversionPercent = Math.Round(summary.Conversion * 100, 2);
    }
}
```

```xml
<StackPanel Orientation="Horizontal" Spacing="32">
    <arc:AnimatedCounter Label="月度经常性收入" Value="{Binding RecurringRevenue}" Prefix="$" />
    <arc:AnimatedCounter Label="活跃订阅" Value="{Binding ActiveSubscriptions}" />
    <arc:AnimatedCounter Label="转化率" Value="{Binding ConversionPercent}" Decimals="2" Suffix="%" />
</StackPanel>
```

- 当业务规则需要取整数字时，在视图模型中取整，因为计数器只在显示时取整。读者看到的就是你的规则产生的确切数字。
- 按照数据的节奏安排刷新，例如每次加载页面刷新一次，或对实时计数每 30 秒刷新一次。变化一秒多次的计数器很难阅读，因此应批量更新。
- 将数字的来源保留在视图模型中，便于测试读取，让计数器只负责显示。

### 格式与区域性

`CultureName` 设置分组分隔符与小数分隔符所用的区域性。默认为 `en-US`，因此在服务端与客户端的输出一致，这对出现在导出报告中的数字很重要。当页面已本地化时，将其设为读者的区域性；固定格式的报告则保持默认值。

```xml
<arc:AnimatedCounter Label="用户数" Value="{Binding Users}" CultureName="de-DE" />
```

区域性只改变计数器绘制的分隔符和数字。前缀与后缀是你提供的文字，因此请为所用区域书写货币符号，例如为德语读者在数字后面加上“€”，并检查符号与数字的顺序是否符合该区域的习惯。

### 克制地使用动效

当数字因读者关心的事件而变化时，滚动是很好的信号：付款到账、会话开始、费率变动。若数字只是因计时器而变化，而没有任何东西依赖它，滚动就会造成干扰。同一屏幕上若有多个计数器同时滚动，眼睛就得在它们之间挑选，因此应只为最重要的那一个做动画，其余保持静态数字。首次显示的动效应每个页面只出现一次，而不是每次切换标签都出现。

减弱动效时，每个新值会立即替换，因此关闭动画的读者仍能读清数字。信息相同，只是去掉了滚动。

### 示例：账单概览的主要数字

2026 年 10 月管理控制台中账单概览的页头。三个数字并排显示：收入、活跃订阅和转化率。收入带有货币前缀，比率保留两位小数并带有百分号后缀，每个数字上方都有一个简短的标签。计数器就是页面的主要数字；详细信息位于下方的表格中，因此计数器只保留三个。

![账单概览的三个主要数字：收入、订阅与转化率](images/BillingFigures.png)

```xml
<StackPanel Width="620" Spacing="16">
    <TextBlock Text="Billing overview, October 2026"
               FontSize="14"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Orientation="Horizontal" Spacing="32">
        <arc:AnimatedCounter Label="Recurring revenue" Value="48250" Prefix="$" />
        <arc:AnimatedCounter Label="Active subscriptions" Value="1284" />
        <arc:AnimatedCounter Label="Conversion" Value="3.42" Decimals="2" Suffix="%" />
    </StackPanel>
    <TextBlock Text="Updated 4 minutes ago"
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### 文案规范

- **标签**：用句子式大小写的简短名词短语说明度量，例如“经常性收入”或“活跃订阅”。标签是数字的语境，主要数字计数器上不要省略它。
- **前缀与后缀**：一个符号或简短单位，如“$”“€”“%”“ms”。较长的单位应放在标签中。
- **小数位**：由业务规则决定精度，而不是由原始数据决定。金额通常显示 0 或 2 位小数，比率通常显示 1 或 2 位。
- **周期**：在标签或计数器附近的说明中写明周期，例如“最近 30 天”或“截至 2026 年 10 月 21 日”，因为计数器只显示数值，不显示日期。

| 不要写 | 改为 |
|---|---|
| 收入数字 | 经常性收入 |
| $ 48250 | $48,250（使用 `Prefix="$"` 并给出格式化后的数值） |
| 转化 | 转化率，最近 30 天 |
| 用户（计数） | 活跃订阅 |
| 3.4212 % | 3.42%（使用 `Decimals="2"` 和 `Suffix="%"`） |

### 最佳实践

**推荐**

- 固定 `Decimals` 为数字的精度。每次更新时宽度若发生变化，相邻内容就会跟着移动，读者会把它看作布局错误。
- 每个主要数字保留一个计数器，把细节放进表格或图表中。一行放三到四个计数器是读者能够扫视的上限。
- 为每个计数器设置 `Label`。标签是无障碍名称的第一部分，因此屏幕阅读器读出的是数字是什么，而不只是数字本身。
- 只为作为区段存在理由的统计设置 `AnimateOnView`；对于每次刷新都会变化的数值，则不要开启。

**避免**

- 在 `Value` 中传入格式化的字符串。计数器需要数字才能滚动；“48,250”不是它能做动画的值，分组会由它自动应用。
- 用比数据变化还快的计时器让计数器滚动。缺少新事实的持续运动会让读者学会忽略这个数字。
- 把计数器放在密集的表格列或包含许多行的列表中。每个计数器都为自己的数位列做动画，大量计数器带来的成本远超读者从动效中获得的收益。
- 把计数器当作输入控件。它没有键盘焦点，读者也无法修改其值；由人编辑的值应使用输入控件。

### 常见问题

**为什么计数器第一次会从零开始？** 只有 `AnimateOnView` 会从零开始。计数器会先就位，当其首次有 60% 可见时，让每个数位从零向上滚动。没有 `AnimateOnView` 时，第一个值会立即出现，之后的变化则从旧值滚动到新值。

**为什么我滚动回来时 `AnimateOnView` 不再运行？** 显现效果对每个计数器只运行一次。第一次之后，计数器停留在它的数值上，因此之后发生的变化只为该变化滚动，而不再重复显现效果。

**如何让屏幕阅读器播报这个数字？** 计数器本身不会播报它的变化。请在包含计数器的元素上设置 `AutomationProperties.LiveSetting="Polite"`，这样变化会在当前语音之后被读出。只对因读者需要知道的事件而变化的数字使用它。

**为什么数字在我的区域下显示不对？** 计数器使用 `CultureName` 进行格式化，其默认值为 `en-US`。请将它设为读者所在的区域性，并自行书写货币符号及其位置，因为前缀和后缀都是纯文字。

**能否在计数器内部对数值取整？** 只能用于显示。`Decimals` 控制计数器显示多少位数字，它会对显示的数字取整，但存储的 `Value` 不会改变。当业务规则需要取整后的数字本身时，请在数据代码中取整。

## API 参考

<!-- api: AnimatedCounter -->

## 无障碍

- 计数器不可聚焦，也没有定义任何键盘交互，因此不会响应任何按键。
- 完整的格式化文字（包括前缀、分隔符、小数位和后缀）就是计数器的名称，因此读者一次就能听到整个数字。有 `Label` 时，名称读作“标签：数值”。
- 滚动的数位对辅助技术隐藏。计数器本身不播报其变化。若要播报，请在外层元素上设置 `AutomationProperties.LiveSetting="Polite"`。
- 标签是数字上方可见的文字，因此每位读者都能看到数字的可见语境，而不仅是那些听到名称的人。

## 动效

- 每个数位列都朝整个数字的变化方向转动，并在 9 与 0 之间循环。
- 当数位的数量发生变化时，新增的数位列和分隔符会滑入或滑出，宽度平滑地调整以适应。相邻内容会随之轻微移动。
- 使用 `AnimateOnView` 时，计数器有 60% 可见后滚动开始，各数位以轻微的错落依次启动。
- 减弱动效时，新值立即替换，不进行滚动。关闭动效则直接跳到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| `Value` 变化 | 数位列朝变化方向转动 | 新数位立即出现 | 立即完成 |
| 数位数量变化 | 新数位列和分隔符滑入，宽度平滑调整 | 立即完成 | 立即完成 |
| 使用 `AnimateOnView` 的首次显示 | 数位从零向上滚动，有轻微错落 | 直接显示最终值 | 立即完成 |

## 相关组件

- `MetricCard` 用于在卡片中展示数字，并附带其标签与变化。
- `Sparkline` 用于展示数字及其趋势。
- `BarChart` 与 `LineChart` 会自行更新其主要数值，因此不需要这个计数器。
- `TextMorph` 用于变化的是文字而非数字的场景。

| 组件 | 适用场景 |
|---|---|
| `MetricCard` | 数字需要卡片、标题和变化指示 |
| `Sparkline` | 数字旁边需要一个紧凑的趋势 |
| `TextMorph` | 变化的内容是词语或短语，而不是数字 |
| `NumberField` / `MoneyInput` | 由人输入或编辑数字 |
| `SortableDataTable` | 大量数字以行的形式呈现，供人扫视和排序 |
