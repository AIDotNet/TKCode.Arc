---
title: Market terminal
summary: 一个交易界面：可缩放、可平移的 K 线图，带迷你走势线与闪烁价格的实时自选列表，以及带订单列表的市价 / 限价下单票。
source-hash: e2f9fb2af588
---

<Demo name="Preview" />

## 何时使用

`MarketTerminal` 是"一次看一个代码、交易一个代码"的产品主界面。图表、自选列表和下单票读的是同一个实时价格，所以一次跳动会同时带动头部价格、自选行、K 线和预估金额。

- **券商或投资应用。** 在自选列表里选一个代码，研究它的 K 线，然后在旁边的下单票里买入或卖出。
- **模拟交易与教学。** 默认一切都在本地模拟：价格会动，限价单会挂着并自行成交，没有任何数据离开界面。
- **内部行情看板。** 用键盘逐根查看 K 线，对比 20 与 50 根均线，一键在今天与一年之间切换。

## 何时不用

- **盘口与深度。** 这个区块每个代码只显示一个实时价格，不显示每个价位的买卖挂单。
- **业务指标。** 收入、注册或用量没有开高低收；请用 `LineChart` 或 `BarChart`。
- **持仓总览。** 列出持仓、盈亏与占比需要表格，而不是单个代码的图表。
- **同时看很多代码。** 需要一格格小图时，请在表格里给每个代码放一个 `Sparkline`。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。把自选列表作为 `MarketSymbol` 传入；每个代码有代码、名称、当前价格、昨收、日波动率和平均成交量。每个范围的 K 线都由这些数字生成，同一个代码每次生成的结果都一样，所以图表在多次运行之间保持稳定。`Holdings`、`Orders` 和 `BuyingPower` 用来填充下单票。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MarketTerminal Ticker="ORBT" Range="SixMonths" BuyingPower="18420.50">
        <arc:MarketSymbol Ticker="ORBT" Name="Orbital Freight" Price="184.53" PreviousClose="182.97" Volatility="0.024" />
        <arc:MarketSymbol Ticker="KLVN" Name="Kelvin Grid" Exchange="NYSE" Price="64.12" PreviousClose="64.80" />
        <arc:MarketTerminal.Holdings>
            <arc:MarketHolding Ticker="ORBT" Shares="120" />
        </arc:MarketTerminal.Holdings>
    </arc:MarketTerminal>
</UserControl>
```

### 结构

- **头部**：代码、公司名称与交易所；逐位滚动的价格；带箭头的今日涨跌；今日开盘、最高、最低与成交量；以及 Live 按钮。
- **工具栏**：时间范围切换（1D、5D、1M、6M、1Y）、两个均线开关，以及放大、缩小与重置视图按钮。
- **图表**：读数行、价格网格上的 K 线、下方的成交量、均线、带标签的实时价格线，以及带价格与时间标签的十字线。
- **自选列表**：每个代码一行，包含名称、叠在昨收虚线上的今日迷你走势线、价格和今日涨跌。当前图表代码所在的行带填充。
- **下单票**：买入或卖出、市价或限价、股数、限价（仅限价单）、价格、预估成本或所得、购买力或持股数、状态行、提交按钮和订单列表。

### 范围与图表

`Range` 决定 K 线：`OneDay` 用今天的 5 分钟 K 线，`FiveDays` 用 30 分钟 K 线，`OneMonth`、`SixMonths` 与 `OneYear` 用日线。今天在每个范围里都是同一个交易时段：今天的 30 分钟线和日线都由 5 分钟线折叠而来，一次跳动会同时移动每个范围的最后一根 K 线。`ShowMa20` 和 `ShowMa50` 控制是否显示均线（工具栏开关与之双向绑定）。

默认窗口显示整个范围；在较窄的图表上会减少可见根数，让 K 线之间至少相隔五个像素。滚动滚轮以指针为中心缩放，拖动平移（甩动后会继续移动并逐渐减速），在触屏上可双指捏合，也可以用工具栏。指针在图表上移动时显示十字线，并把那根 K 线写进读数行。

<Demo name="Intraday" />

### 实时价格

`IsLive` 为 true 时，模拟每隔 `TickInterval`（默认 1.2 秒）前进一步：每个价格随机走一小步，头部价格滚动，自选行的价格闪一下绿色或红色，图表把最后一根 K 线、它的标签和均线平滑地移到新价格。Live 按钮可以暂停和恢复；区块不在窗口中时跳动也会停止。要显示真实行情，请保持 `IsLive` 为 false，并在每次报价时调用 `SetPrice(ticker, price)`；`Tick()` 让模拟前进一次。

### 订单

下单票会在按钮可提交之前检查每一笔订单：至少一股、限价大于零、买入时购买力足够、卖出时持股足够。原因写在按钮下方。

- **市价单**立即按实时价格成交。
- 当前就能成交的**限价单**立即按限价与市价中更优的价格成交；否则它会**挂单等待**，价格到达限价时自行成交。挂单带有撤单按钮。
- 成交会更新 `BuyingPower`（双向）和 `Holdings`。`OrderPlaced`、`OrderFilled` 与 `OrderCanceled` 报告每一步。

要通过真实服务下单，请设置 `PlaceOrderAction`。它接收一个 `MarketOrderRequest` 和一个取消令牌；任务完成之前按钮显示加载指示，抛出异常时订单不会进入列表，异常消息写在按钮下方。`CancelOrderAction` 对撤单做同样的事。`SubmitOrderAsync()` 和 `CancelOrderAsync(order)` 与按钮的作用相同。

```csharp
terminal.PlaceOrderAction = (request, cancellation) => broker.PlaceAsync(request.Ticker, request.Side, request.Shares,
    request.Type == MarketOrderType.Limit ? request.Price : null, cancellation);
terminal.OrderFilled += (_, e) => portfolio.Refresh();
```

<Demo name="OrderApi" />

### 窄容器

区块读取的是自身宽度。宽度不小于 1080 时，自选列表和下单票叠成图表旁边 340 宽的一列；不小于 720 时，它们在图表下方并排；更窄时所有卡片纵向堆叠。小于 640 时，统计数字单独占一行、分成四个等宽列，范围切换占满整行，工具栏按钮增大到 44 以便触控。读数行放不下时折到第二行。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（一个 `MarketTerminalLabels`）。替换它即可本地化区块：带参数的条目是接收已格式化值的函数，例如按钮用的 `Submit`（"Buy 10 ORBT"），或状态行用的 `NotEnoughShares`。

## API 参考

<!-- api: MarketTerminal -->

<!-- api: MarketSymbol -->

<!-- api: MarketHolding -->

<!-- api: MarketOrder -->

<!-- api: MarketOrderRequest -->

<!-- api: MarketRange -->

<!-- api: MarketOrderSide -->

<!-- api: MarketOrderType -->

<!-- api: MarketOrderStatus -->

<!-- api: MarketTerminalLabels -->

## 键盘

- **Tab** 依次经过 Live 按钮、范围切换、均线开关、缩放按钮、图表、自选行和下单票。
- 在图表上，**Left** 和 **Right** 把十字线移动一根（按住 **Shift** 时移动十根），十字线越过边缘时视图跟着滚动。**Home** 与 **End** 跳到第一根和最新一根。**+** 与 **-** 以十字线为中心缩放，**0** 重置视图，**Escape** 清除十字线。
- 在自选列表中，**Up** 与 **Down** 在代码之间移动，**Home** 与 **End** 跳到第一个和最后一个。**Enter** 或 **Space** 把焦点所在的代码显示到图表上。
- 范围切换和下单票里的切换遵循分段控件的规则：方向键改变选择。

## 无障碍

- 图表是一个可聚焦的元素，名称包含代码、范围和 K 线周期（"ORBT price chart, six months of daily candles"），帮助文本说明可用的按键。用键盘移动十字线时，会通过礼貌型实时区域读出那根 K 线的时间、开盘、最高、最低、收盘、涨跌和成交量。
- 每个自选行都是一个按钮，名称包含代码、名称、价格和涨跌（"up 0.85 percent today"）。
- 涨跌从不只靠颜色表达：它们带有正负号和箭头，订单状态以文字写出。
- 订单的问题和失败写在按钮下方的那一行，这一行是礼貌型实时区域；下单、成交与撤单都会播报。
- 指针聚焦不画焦点环；键盘聚焦时，图表、自选行、开关和按钮周围显示 Arc 的焦点环。

## 动效

- **缩放、平移与切换范围**：可见窗口和价格刻度以指数方式逼近目标，时长取自 smooth 弹簧，因此不会过冲，新的手势可以在中途让运动反向。拖动后甩出会保持速度，然后像抛出的卡片一样逐渐减速。
- **实时跳动**：最后一根 K 线、它的价格标签、价格线和均线平滑地移到新价格；头部价格滚动；每个变化的自选价格闪一下绿色或红色，并在 900 毫秒内淡出。选中行的填充在行之间滑动。
- **下单票**：限价输入框以高度与透明度一起展开，收起更快；新订单从列表顶部长出来。
- 价格流动时 Live 圆点会脉动；它是区块里唯一的循环动画，因为它报告的是一种状态。
- 图表只在有东西在动时才绘制帧。减弱动效时，视图、价格和面板直接到位，没有惯性和脉动，闪烁改为短暂的静止色块。关闭动效时所有变化立即生效。

## 相关组件

- `Budget variance`，Finance 分组的另一个区块。
- `SegmentedControl`、`NumberField`、`AnimatedCounter` 和 `Button`，构成这个区块的控件。
- 不需要 K 线时可用 `Sparkline` 和 `LineChart`。
