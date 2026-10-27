---
title: 指标卡
summary: 为需要上下文说明的数字提供紧凑的摘要。
source-hash: 999dbea75977
---

<Demo name="Default" />

## 何时使用

`MetricCard` 是仪表板上回答“我们做得怎么样”的磁贴。它包含一个数字、为其命名的标签、赋予其意义的对比信息，以及一个可选的变化标签，位于与其他指标并排的表面中。一排指标卡是大多数运营与业务页面的顶部：在打开表格或图表之前，用户最先读到的就是它们。

- **SaaS 概览页中的 KPI 行**：月度经常性收入、活跃账号与流失率，各自以“对比上月”作为上下文，并附带一个显示变化的标签。
- **运营控制台中的可用率与服务健康度**：“可用率 99.98%”配“近 30 天”，让数字与它所覆盖的时间段一起被解读。
- **订单或支持仪表板上的实时计数**：未结订单或工单积压，随着新工作到来数字滚动更新，上下文说明时间范围。
- **开发者工具或 API 门户中的延迟与吞吐量**：以毫秒为单位的 p95 数值，与昨天对比，当最近几个数据点的方向很重要时附带趋势线。
- **计费与用量摘要**：金融科技或计费区域中的月初至今总额或平均余额，显示在它所指的时间段旁边。

## 何时不用

- **趋势比最新数值更重要**，例如一周的每日注册量需要以形状来解读：用 `Sparkline` 或 `LineChart`，它们能为整个数据序列留出空间。
- **运行文字或标题中的单独数字**，没有外框也没有上下文：用 `AnimatedCounter`。
- **配额或达到上限的进度**，例如 50 个席位已用的数量：用 `UsageMeter`，它会绘制比例。
- **包含媒体、所有者与操作的卡片**：用 `Card`。指标卡承载的是数字，而不是内容磁贴。
- **需要按属性比较的大量数值**，例如每个地区与每个套餐的收入：用 `Table` 或 `DataGrid`，它们的每一行都可以带有自己的列。
- **距离截止日期的时间**，例如续费或试用结束日期：用 `Countdown`，它会自行倒计时。

## 用法

`Label`、`Value` 与 `Context` 是常用部分。`Change` 为可选项；不设置时变化标签隐藏。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MetricCard Label="可用率"
                    Value="99.98"
                    Decimals="2"
                    Suffix="%"
                    Context="近 30 天"
                    Change="+0.02%"
                    Width="280" />
</UserControl>
```

`MetricCard` 派生自 Avalonia 的 `TemplatedControl`。它是一个展示型控件：不可获得焦点，没有按下行为，也不引发任何事件。它的数字是模板中的一个 `AnimatedCounter`，因此滚动的数位、小数位以及前缀和后缀都是计数器的行为，而卡片在其周围添加标签、变化标签和趋势线。通过代码或绑定设置数值后，卡片会就地更新。

### 结构

1. **标签**（`Label`）：14 px，位于顶行左侧，使用次要文字颜色。卡片较窄时以省略号截断，它也是卡片的无障碍名称。
2. **变化标签**（`Change`）：位于顶行右侧的胶囊形标签，左右内边距 8 px，上下内边距 3 px。它包含变化文字，并且在设置了 `IsChangeGlyphVisible` 时，还包含一个 12 px 的趋势图标。`Change` 为空时隐藏。
3. **数值**（`Value`，配合 `Prefix`、`Suffix` 与 `Decimals`）：大号计数器中的数字，底部对齐。`Decimals` 的默认值为 0。`Prefix` 位于数字之前，例如“$”，`Suffix` 位于数字之后，例如“%”或“ ms”。
4. **趋势线**（`Sparkline`）：与数值位于同一行、底边对齐的 96 × 36 px 折线。仅在有两个或更多数据点时显示。
5. **上下文**（`Context`）：14 px，使用弱化的文字颜色，位于数值行下方 16 px。它说明数字与什么进行比较：“对比上周”“近 30 天”。
6. **表面**：24 px 内边距，1 px 边框使用表面颜色，表面圆角以及静态阴影。顶行位于数值行上方 32 px。

### 数值、前缀、后缀与小数位

数字是 `double` 类型，因此一个属性即可覆盖整数、百分比和平均值。小数位应根据数字的含义来选择，而不是根据卡片的宽度：99.98% 的可用率需要两位小数，订单数则不需要。

```xml
<arc:MetricCard Label="本月收入"
                Value="48210"
                Prefix="$"
                Decimals="0"
                Context="对比上月"
                Change="+6.4%"
                Width="280" />
```

- 货币符号使用 `Prefix`，跟在数字后面的单位使用 `Suffix`。单位要简短；过长的后缀会把数位挤向左侧，使数值行显得不稳定。
- 按小数变动的百分比使用一位小数（“1.8%”），需要与服务等级目标对比的可用率使用两位小数（“99.98%”）。
- 当数值本身已经带有单位时，不要把单位写进 `Label`。“延迟”加上后缀 “ ms” 只读一次；“延迟（ms）”加上后缀 “ ms” 则会读两次。

### 变化标签

以 “+” 开头的 `Change` 表示上升，以 “-” 或 “−” 开头的表示下降。其他文字为中性变化。方向可通过 `ChangeDirection` 读回，标签的颜色也由它决定。

| `Change` 文字 | `ChangeDirection` | 标签外观 |
|---|---|---|
| “+12%” | `Up` | 成功色底、成功色文字 |
| “−8%” 或 “-8%” | `Down` | 危险色底、危险色文字 |
| “flat”、“0%” 或空文本 | `None` | 带描边的中性标签 |

<Demo name="PlainNumber" />

```xml
<arc:MetricCard Label="p95 延迟"
                Value="182"
                Suffix=" ms"
                Context="对比昨天"
                Change="−8%"
                IsChangeGlyphVisible="True"
                Width="280" />
```

正负号要写在文字中。颜色是第二重提示，正负号是第一重提示，因此无法分辨颜色差异的用户以及屏幕阅读器用户也能看清方向。`IsChangeGlyphVisible` 会为带符号的标签添加趋势箭头，默认关闭。

标签颜色表示的是方向，而不是这个方向是否良好。延迟下降是好事，但 “−8%” 的标签仍然使用危险色底色。请在 `Context` 中说明含义（“越低越好”），或者当好的方向与正负号不一致时，把变化显示为不带颜色的中性数值。将 `Change` 设为 `null` 可隐藏标签，标签会淡出。

### 趋势线

`Sparkline` 接收一组数值，在数字旁绘制一条小趋势线。趋势线首次进入视野时自行绘制，数据变化时重新绘制。它至少需要两个数据点：只有一个值或没有值时，趋势线隐藏。趋势线的颜色取决于变化方向：没有变化时使用强调色，上升变化使用成功色，下降变化使用危险色。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <arc:MetricCard Label="订单" Value="1204" Context="对比上周" Change="+12%" Width="280">
        <arc:MetricCard.Sparkline>
            <generic:List x:TypeArguments="x:Double">
                <x:Double>820</x:Double>
                <x:Double>910</x:Double>
                <x:Double>1204</x:Double>
            </generic:List>
        </arc:MetricCard.Sparkline>
    </arc:MetricCard>
</UserControl>
```

```csharp
orders.Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 };
```

传入较短的序列，例如最近 7 天或 30 天的每日数值，使趋势线能显示近期变化的形状。数百个数据点会让 96 px 的区域变成杂乱的线团；这种情况请使用 `LineChart`。

### 绑定到视图模型

指标行将每张卡片绑定到仪表板视图模型上的一个属性。使用 CommunityToolkit.Mvvm 时，视图模型引发属性变更通知，卡片就地更新，数位滚动到新值。

```csharp
public partial class OverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private double _orderCount;

    [ObservableProperty]
    private string? _orderContext;

    [ObservableProperty]
    private string? _orderChange;

    [ObservableProperty]
    private IList<double>? _orderTrend;

    public void ShowOrders(int openCount, int previousCount, IList<double> dailyCounts)
    {
        OrderCount = openCount;
        OrderContext = "对比上周";
        OrderChange = openCount >= previousCount
            ? $"+{openCount - previousCount}"
            : $"−{previousCount - openCount}";
        OrderTrend = dailyCounts;
    }
}
```

```xml
<arc:MetricCard Label="订单"
                Value="{Binding OrderCount}"
                Context="{Binding OrderContext}"
                Change="{Binding OrderChange}"
                Sparkline="{Binding OrderTrend}"
                Width="280" />
```

- `Value` 使用单向绑定。卡片从不把它写回。
- 在同一个方法（上面的 `ShowOrders`）中一起更新 `Context`、`Change` 与 `Value`，使卡片始终显示一致的状态。
- 编译绑定需要在视图上设置 `x:DataType`。示例为简洁起见省略了它。

每次更新时，数字都会滚动到新值，文案则从数字变化的方向滚入，因此每隔几秒到达一次的更新会让这一行持续处于动态之中。对于实时数据流，请以固定间隔更新视图模型，例如每 30 秒一次。

### 窄窗口

在宽度为 380 px 或更窄的窗口中，卡片使用更紧凑的内边距（16 px），顶行位于数值行上方 24 px，变化标签移到标签下方，两者之间留出 12 px 的间距。该规则读取的是窗口宽度而不是卡片宽度，因此窗口中的所有卡片会一起切换到窄版布局。此行为无需任何代码。

### 示例：月度概览行

SaaS 管理后台在月初的概览页面。三个指标回答人们最先提出的问题：收入进账多少、留存了多少账号以及有多少活跃账号。每张卡片都在上下文中注明其时间段，变化标签在文字中显示方向。流失率卡片以危险色底显示下降的变化。流失率下降是好消息，因此它的上下文写着“越低越好”，这正是变化标签一节中的规则：颜色跟随正负号，文字承载含义。

![十月的三张指标卡：收入、流失率与活跃账号](images/MonthlyOverview.png)

```xml
<StackPanel Width="740" Spacing="16">
    <TextBlock Text="Overview, October 2026" FontSize="18" FontWeight="SemiBold" />
    <WrapPanel ItemSpacing="12" LineSpacing="12">
        <arc:MetricCard Label="Revenue"
                        Value="48210"
                        Prefix="$"
                        Decimals="0"
                        Context="vs last month"
                        Change="+6.4%"
                        Width="236"
                        VerticalAlignment="Top" />
        <arc:MetricCard Label="Churn"
                        Value="1.8"
                        Decimals="1"
                        Suffix="%"
                        Context="Lower is better"
                        Change="−0.3 pts"
                        Width="236"
                        VerticalAlignment="Top" />
        <arc:MetricCard Label="Active accounts"
                        Value="1284"
                        Context="Across 3 regions"
                        Change="+42"
                        Width="236"
                        VerticalAlignment="Top" />
    </WrapPanel>
</StackPanel>
```

### 文案规范

- **标签**：句子式大小写的简短名词短语，用来说明度量的名称，例如“收入”“可用率”“p95 延迟”。它是无障碍名称，因此即使没有数值也要能看懂。
- **数值与单位**：单位只写一次，放在 `Prefix` 或 `Suffix` 中，不要在标签中重复。数字使用阿拉伯数字书写。
- **变化**：一个正负号加上带单位的数字，例如 “+6.4%”“−0.3 pts”“+42”。下降的变化使用减号 “−”；卡片同时识别 “−” 和 “-”。
- **上下文**：时间段或比较对象，置于数字之后，使用小写：“对比上月”“近 30 天”。当正负号本身无法说明好坏时，写出什么是好的：“越低越好”。

| 不要写 | 改为 |
|---|---|
| 收入（$） | 收入，并将 `Prefix` 设为 “$” |
| 上涨 6 个百分点！ | +6.4% |
| 流失率下降（好） | −0.3 pts，越低越好 |
| 数据来自上月 | 对比上月 |
| 延迟（ms），后缀为 “ ms” | 延迟，后缀为 “ ms” |

### 最佳实践

**推荐**

- 在每张卡片的 `Context` 中写明时间段，使数字永远不会脱离它的时间范围被解读。
- 将卡片排成一行并保持相同宽度，使数值对齐，便于视线比较。
- 在 `Context` 中说明好的方向与正负号不一致的变化。
- 对于实时数据，以稳定的间隔更新视图模型。卡片会在两次更新之间归于平静，视线读取的是变化本身，而不是动态。

**避免**

- 一行中展示超过四到五张卡片。超过这个数量，数字就不再是用户最先读到的内容。
- 同一行的卡片之间小数位数不一致，这会让整行看起来未完成。
- 当 `Change` 已经承载变化时，又在 `Context` 中写上它（“+6.4% 对比上月”）。文字会重复，标签与句子也会重复。
- 过长的 `Value` 文字，例如带有文字的格式化字符串。计数器用于数字；“健康”之类的词应放在状态元素中。

### 常见问题

**变化是好的，但标签显示危险色。我该如何修正？** 标签按 `Change` 的正负号着色，而不是按变化是否良好着色。请在 `Context` 中写明含义（“越低越好”），并保持正负号的真实性。按照设计，颜色跟随正负号，因此卡片不需要第二个属性来翻转它。

**如何在不显示颜色的情况下表示变化？** 写不带正负号的文字（例如“稳定”或“无变化”），这会使方向为 `None`，标签呈中性。卡片会以描边显示标签，不带底色。

**为什么数字从零开始？** 卡片第一次滚动进入视野时，计数器会从零滚动到数值。之后，每次更新都从上一个数值滚动。

**为什么 `Change` 为空时标签隐藏？** 空的 `Change` 会移除标签，使顶行只显示标签。标签会淡出。

**用户可以编辑数值或打开指标吗？** 不可以。卡片仅用于展示，不获得焦点，也不引发任何事件。若要打开详情视图，请把卡片放在你的页面所拥有的控件中，并在那里处理按下操作。

## API 参考

<!-- api: MetricCard -->

## 无障碍

- 卡片是一个由 `Label` 命名的分组。其帮助文本为变化与上下文，例如 “+12% 对比上周”；没有变化时仅为上下文。
- 数字由计数器朗读。变化标签是纯文本，并以 “+” 或 “−” 表明方向，因此方向不依赖颜色。
- 数值变化本身不会被播报。如果需要播报，请在卡片或其外层容器上设置 `AutomationProperties.LiveSetting`。
- 卡片不接收键盘焦点。Tab 会跳过它，因此一排指标不会变成一排停靠点。
- 为每张卡片设置一个能独立成立的 `Label`。屏幕阅读器首先读取的就是标签，因此“可用率”比“指标 1”更好，也比只读数值更好。

## 动效

- 数字在卡片首次滚动进入视野时从零开始滚动，之后逐步转向每个新数值。
- 标签、上下文与变化标签的文字从数字移动的方向滚入。
- 变化标签的宽度通过弹簧适应新文字，标签以缩放与淡入淡出的方式出现或消失。
- 趋势线首次显示时在 0.9 s 内绘制完成，数据变化时交叉淡化。
- 减弱动效时，数字直接跳到新值，文字淡入；变化标签与趋势线的变化不带动画。关闭动效时，所有变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 数字变化 | 逐位滚动，变大向上、变小向下 | 直接跳到新值 | 立即完成 |
| 标签、上下文或变化标签文字变化 | 从数字移动的方向滚入 | 瞬时淡入 | 立即完成 |
| 标签出现或消失 | 以快速弹簧从 96% 缩放并淡入淡出 | 无动效 | 立即完成 |
| 变化标签宽度 | 以弹簧适应新文字 | 无动效 | 立即完成 |
| 趋势线首次显示 | 在 0.9 s 内绘制完成 | 立即显示 | 立即完成 |
| 趋势线数据变化 | 交叉淡化 | 无动效 | 立即完成 |

## 相关组件

- `AnimatedCounter`：不需要卡片外框的单独数字。
- `Sparkline`：趋势比最新数值更重要时使用。
- `Card`：围绕数字的一组内容与操作。
- `UsageMeter`：用于与上限对比的数值。

| 组件 | 适用场景 |
|---|---|
| `AnimatedCounter` | 数字位于运行文字或标题中，不需要卡片 |
| `Sparkline` | 数据序列的形状就是要传达的信息 |
| `LineChart` | 数据序列需要坐标轴、多条线或更长的历史 |
| `UsageMeter` | 数值是某个上限的一部分，例如已用席位 |
| `Card` | 磁贴在数字旁边包含媒体、所有者或操作 |
| `Countdown` | 数字是距离截止日期的剩余时间 |
| `Table` | 数值是需要按属性排序与比较的行 |
