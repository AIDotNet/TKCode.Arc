---
title: Market terminal
summary: A trading screen with a candlestick chart you zoom and pan, a live watchlist with sparklines and flashing prices, and a market and limit order ticket with its order list.
---

<Demo name="Preview" />

## When to use

`MarketTerminal` is the main screen of a product where one symbol is charted and traded at a time. The chart, the watchlist
and the ticket all read the same live price, so a tick moves the header, the row, the candle and the estimated cost
together.

- **A brokerage or investing app.** Pick a symbol from the watchlist, study its candles, then buy or sell from the ticket
  beside it.
- **Paper trading and teaching.** Everything runs on a local simulation by default: prices move, limit orders wait and fill
  on their own, and nothing leaves the screen.
- **An internal markets dashboard.** Inspect candles one by one with the keyboard, compare the 20 and 50 candle averages,
  and switch between today and a year in one click.

## When not to use

- **Order books and depth.** The block shows one live price per symbol, not bids and asks at each level.
- **Business metrics.** Revenue, sign-ups or usage have no open, high, low and close; use `LineChart` or `BarChart`.
- **Portfolio overviews.** A list of positions with gains and allocation needs a table, not a chart of one symbol.
- **Many symbols at once.** For a grid of small charts, use a `Sparkline` per symbol in a table.

## Usage

The block derives from Avalonia's `TemplatedControl`. Give it the watchlist as `MarketSymbol`s; each one has a ticker, a
name, the price now, yesterday's close, a daily volatility and an average volume. The candles of every range are generated
from those numbers, the same way every time for the same ticker, so the chart is stable across runs. `Holdings`,
`Orders` and `BuyingPower` fill the ticket.

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

### Anatomy

- **Header**: the ticker, the company name and its exchange; the price, which rolls digit by digit; today's change with an
  arrow; today's open, high, low and volume; and the Live button.
- **Toolbar**: the range switch (1D, 5D, 1M, 6M, 1Y), the two moving average toggles and the zoom in, zoom out and reset
  buttons.
- **Chart**: a readout line, the candles over a price grid, volume under them, the averages, the live price line with its
  tag, and the crosshair with its price and time tags.
- **Watchlist**: one row per symbol with its name, today's sparkline over a dotted line at yesterday's close, the price and
  today's change. The charted symbol's row is filled.
- **Ticket**: buy or sell, market or limit, the share count, the limit price (limit orders only), the price, the estimated
  cost or proceeds and the buying power or the shares held, a status line, the submit button and the order list.

### Ranges and the chart

`Range` picks the candles: `OneDay` uses today's 5 minute candles, `FiveDays` 30 minute candles, and `OneMonth`,
`SixMonths` and `OneYear` daily candles. Today is the same session on every range: the 30 minute and daily candles of today
are folded from the 5 minute ones, and a tick moves the live candle of every range together. `ShowMa20` and `ShowMa50`
show the moving averages (the toolbar toggles them, two-way).

The default window shows the whole range, thinned on narrow charts so candles stay at least five pixels apart. Scroll the
wheel to zoom around the pointer, drag to pan (a flick keeps moving and slows down), pinch on a touch screen, or use the
toolbar. Moving the pointer over the chart shows the crosshair and writes that candle into the readout.

<Demo name="Intraday" />

### Live prices

While `IsLive` is true the simulation steps every `TickInterval` (1.2 seconds by default): each price takes a small random
step, the header price rolls, the row's price flashes green or red, and the chart glides the live candle, its tag and the
averages to the new price. The Live button pauses and resumes the stream; ticks also stop while the block is not in a
window. To show real prices, keep `IsLive` false and call `SetPrice(ticker, price)` for each quote; `Tick()` advances
the simulation once.

### Orders

The ticket checks every order before the button can submit it: at least one share, a limit price above zero, enough buying
power for a buy and enough shares for a sell. The reason is written under the button.

- A **market order** fills at once at the live price.
- A **limit order** that can trade now fills at once at the better of the limit and the market; otherwise it is
  **working** and fills by itself when the price reaches the limit. A working order has a cancel button.
- A fill updates `BuyingPower` (two-way) and `Holdings`. `OrderPlaced`, `OrderFilled` and `OrderCanceled` report each step.

To place orders with a real service, set `PlaceOrderAction`. It receives a `MarketOrderRequest` and a cancellation token;
the button shows a spinner until the task finishes, and an exception keeps the order off the list and writes its message
under the button. `CancelOrderAction` does the same for a cancel. `SubmitOrderAsync()` and `CancelOrderAsync(order)` do
what the buttons do.

```csharp
terminal.PlaceOrderAction = (request, cancellation) => broker.PlaceAsync(request.Ticker, request.Side, request.Shares,
    request.Type == MarketOrderType.Limit ? request.Price : null, cancellation);
terminal.OrderFilled += (_, e) => portfolio.Refresh();
```

<Demo name="OrderApi" />

### Narrow containers

The block reads its own width. From 1080 the watchlist and the ticket stack in a 340 wide column beside the chart; from 720
they sit side by side under it; below that every card stacks. Under 640 the stats get their own row of four equal columns,
the range switch takes the full width and the toolbar buttons grow to 44 for touch. The readout wraps onto a second line
when it does not fit.

<Demo name="Narrow" />

### Text

Every word comes from `Labels`, a `MarketTerminalLabels`. Replace it to localize the block: parameterized entries are
functions that receive formatted values, such as `Submit` for the button ("Buy 10 ORBT") or `NotEnoughShares` for the
status line.

## API reference

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

## Keyboard

- **Tab** moves through the Live button, the range switch, the average toggles, the zoom buttons, the chart, the watchlist
  rows and the ticket.
- On the chart, **Left** and **Right** move the crosshair one candle (with **Shift**, ten), and the view scrolls when the
  crosshair passes an edge. **Home** and **End** go to the first and the latest candle. **+** and **-** zoom around the
  crosshair, **0** resets the view and **Escape** clears the crosshair.
- In the watchlist, **Up** and **Down** move between symbols and **Home** and **End** jump to the first and the last.
  **Enter** or **Space** charts the focused symbol.
- The range switch and the ticket's switches follow the segmented control: the arrows change the choice.

## Accessibility

- The chart is a focusable element named after the symbol, the range and the candle size ("ORBT price chart, six months of
  daily candles"), with its keys as help text. Moving the crosshair with the keyboard reads the candle's time, open, high,
  low, close, change and volume through a polite live region.
- Each watchlist row is a button whose name carries the ticker, the name, the price and the change ("up 0.85 percent
  today").
- Changes never rely on color alone: they carry a sign and an arrow, and order status is written as text.
- Problems with an order and failures are written in the line under the button, which is a polite live region; placing,
  filling and canceling an order are announced.
- Pointer focus draws no ring; keyboard focus shows Arc's focus ring around the chart, the rows, the toggles and the
  buttons.

## Motion

- **Zoom, pan and range changes**: the visible window and the price scale approach their targets exponentially, timed from
  the smooth spring, so nothing overshoots and a new gesture can reverse the motion mid-way. A drag flick keeps its speed
  and slows down like a thrown card.
- **Live ticks**: the last candle, its price tag, the price line and the averages glide to the new price; the header price
  rolls; each changed watchlist price flashes a green or red tint that fades out in 900 ms. The selected row's fill glides
  between rows.
- **Ticket**: the limit field opens with height and opacity together and closes faster; a new order grows into the top of
  the list.
- The Live dot pulses while prices stream; it is the block's only loop, because it reports a state.
- The chart draws frames only while something moves. Reduced motion places the view, the prices and the panels at once,
  drops the momentum and the pulse, and shows the flash as a short still tint. Off applies every change at once.

## Related

- `Budget variance`, the other Finance block.
- `SegmentedControl`, `NumberField`, `AnimatedCounter` and `Button`, the controls the block is made of.
- `Sparkline` and `LineChart` for charts without candles.
