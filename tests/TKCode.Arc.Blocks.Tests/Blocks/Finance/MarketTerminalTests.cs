using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Finance;

/// <summary>
/// <see cref="MarketTerminal"/> on an invented market (three made-up symbols): the deterministic simulation, ticks across
/// ranges, the watchlist, the chart's view and keyboard, the ticket's validation, fills and cancel, the async callbacks and
/// the responsive layouts.
/// </summary>
public class MarketTerminalTests
{
    private static readonly DateTime s_session = new(2026, 9, 24);

    private static MarketSymbol[] Symbols() =>
    [
        new("ORBT", "Orbital Freight", 184.53, 182.97, 0.024, 140_000_000),
        new("KLVN", "Kelvin Grid", 64.12, 64.80, 0.018, 22_000_000, "NYSE"),
        new("TIDE", "Tidewater Robotics", 91.36, 88.85, 0.032, 60_000_000),
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 1300, Action<MarketTerminal>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Terminal = new MarketTerminal { IsLive = false, SessionDate = s_session, BuyingPower = 10_000 };
            Terminal.Symbols.AddRange(Symbols());
            Terminal.Holdings.Add(new MarketHolding("ORBT", 20));
            configure?.Invoke(Terminal);
            Terminal.TickerChanged += (_, e) => Tickers.Add(e.NewTicker);
            Terminal.OrderPlaced += (_, e) => Placed.Add(e.Order);
            Terminal.OrderFilled += (_, e) => Filled.Add(e.Order);
            Terminal.OrderCanceled += (_, e) => Canceled.Add(e.Order);
            Host = new Border { Width = width, Child = Terminal, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1400;
            Window.Height = 1300;
            Pump();
        }

        public MarketTerminal Terminal { get; }
        public Border Host { get; }
        public Window Window { get; }
        public List<string?> Tickers { get; } = new();
        public List<MarketOrder> Placed { get; } = new();
        public List<MarketOrder> Filled { get; } = new();
        public List<MarketOrder> Canceled { get; } = new();
        public MarketChart Chart => Terminal.Chart!;

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }

        public void Dispose() => Window.Close();
    }

    #region Simulation

    [Fact]
    public void The_Same_Ticker_Always_Generates_The_Same_Candles()
    {
        var a = new MarketSimulation(Symbols(), DateOnly.FromDateTime(s_session));
        var b = new MarketSimulation(Symbols(), DateOnly.FromDateTime(s_session));
        foreach (var range in Enum.GetValues<MarketRange>())
        {
            var x = a.Series("ORBT", range);
            var y = b.Series("ORBT", range);
            x.Count.ShouldBe(y.Count);
            x.Candles.Select(c => c.Close).ShouldBe(y.Candles.Select(c => c.Close));
        }
        a.Series("ORBT", MarketRange.SixMonths).Last.Close.ShouldNotBe(a.Series("KLVN", MarketRange.SixMonths).Last.Close);
    }

    [Fact]
    public void Yesterday_Closes_At_The_Previous_Close_And_Today_At_The_Price()
    {
        var simulation = new MarketSimulation(Symbols(), DateOnly.FromDateTime(s_session));
        var daily = simulation.Series("ORBT", MarketRange.OneYear);
        daily.Count.ShouldBe(252);
        daily.Candles[^2].Close.ShouldBe(182.97, 0.005);
        daily.Last.Close.ShouldBe(184.53, 0.005);
        var intraday = simulation.Series("ORBT", MarketRange.OneDay);
        intraday.Count.ShouldBe(MarketSimulation.ElapsedBars);
        intraday[0].Time.ShouldBe(s_session.AddHours(9.5));
        intraday.Last.Close.ShouldBe(184.53, 0.005);
        // Today's daily candle is folded from the 5 minute candles.
        daily.Last.Open.ShouldBe(intraday[0].Open, 0.005);
        daily.Last.High.ShouldBe(Enumerable.Range(0, intraday.Count).Max(i => intraday[i].High), 0.005);
        simulation.Series("ORBT", MarketRange.SixMonths).Count.ShouldBe(126);
        simulation.Series("ORBT", MarketRange.OneMonth).Count.ShouldBe(22);
        simulation.Series("ORBT", MarketRange.FiveDays).Count.ShouldBe(4 * 13 + 11);
    }

    [Fact]
    public void Moving_Averages_Are_Defined_From_The_First_Visible_Candle()
    {
        var simulation = new MarketSimulation(Symbols(), DateOnly.FromDateTime(s_session));
        foreach (var range in Enum.GetValues<MarketRange>())
        {
            var series = simulation.Series("ORBT", range);
            double.IsNaN(series.Ma50At(0)).ShouldBeFalse(range.ToString());
            var expected = Enumerable.Range(series.Warmup + series.Count - 20, 20).Average(i => series.Candles[i].Close);
            series.Ma20At(series.Count - 1).ShouldBe(expected, 1e-9);
        }
    }

    [Fact]
    public void A_Tick_Moves_The_Live_Candle_Of_Every_Range_Together()
    {
        var simulation = new MarketSimulation(Symbols(), DateOnly.FromDateTime(s_session));
        var ranges = Enum.GetValues<MarketRange>().Select(r => simulation.Series("ORBT", r)).ToList();
        simulation.SetPrice("ORBT", 190);
        foreach (var series in ranges)
        {
            series.Last.Close.ShouldBe(190, 1e-9);
            series.Last.High.ShouldBeGreaterThanOrEqualTo(190);
        }
        var ma = ranges[0].Ma20At(ranges[0].Count - 1);
        ma.ShouldBe(Enumerable.Range(ranges[0].Candles.Length - 20, 20).Average(i => ranges[0].Candles[i].Close), 1e-9);
        simulation.Today("ORBT").Close.ShouldBe(190);
    }

    #endregion

    #region Watchlist and chart

    [AvaloniaFact]
    public void The_First_Symbol_Is_Charted_Until_A_Row_Is_Chosen()
    {
        using var rig = new Rig();
        rig.Terminal.EffectiveSymbol!.Ticker.ShouldBe("ORBT");
        var rows = rig.Terminal.WatchRows;
        rows.Count.ShouldBe(3);
        rows[0].IsCurrent.ShouldBeTrue();
        AutomationProperties.GetName(rows[1]).ShouldBe("KLVN, Kelvin Grid, 64.12, down 1.05 percent today");
        rows[2].Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Terminal.Ticker.ShouldBe("TIDE");
        rig.Tickers.ShouldBe(["TIDE"]);
        rows[2].IsCurrent.ShouldBeTrue();
        rig.Chart.Series!.Count.ShouldBe(126);
        AutomationProperties.GetName(rig.Chart).ShouldBe("TIDE price chart, six months of daily candles");
    }

    [AvaloniaFact]
    public void Arrows_Move_Between_Watchlist_Rows()
    {
        using var rig = new Rig();
        var rows = rig.Terminal.WatchRows;
        rows[0].Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rows[1].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rows[2].IsFocused.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        rows[0].IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Range_Picks_The_Candles()
    {
        using var rig = new Rig(configure: t => t.Range = MarketRange.OneDay);
        rig.Chart.Series!.Range.ShouldBe(MarketRange.OneDay);
        var switcher = TestHelpers.Descendant<SegmentedControl>(rig.Terminal, "PART_Ranges");
        switcher.SelectedValue = MarketRange.FiveDays;
        rig.Pump();
        rig.Terminal.Range.ShouldBe(MarketRange.FiveDays);
        rig.Chart.Series!.Range.ShouldBe(MarketRange.FiveDays);
    }

    [AvaloniaFact]
    public void The_Default_Window_Keeps_Candles_Five_Pixels_Apart()
    {
        using var rig = new Rig(width: 560, configure: t => t.Range = MarketRange.OneYear);
        var (start, end) = rig.Chart.View;
        var plot = rig.Chart.Bounds.Width - rig.Chart.Gutter;
        (plot / (end - start)).ShouldBeGreaterThanOrEqualTo(4.9);
        end.ShouldBe(252.5, 1e-9);
    }

    [AvaloniaFact]
    public void Keys_Move_The_Crosshair_Zoom_And_Reset()
    {
        using var rig = new Rig();
        rig.Chart.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        rig.Chart.CrosshairIndex.ShouldBe(125);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        rig.Chart.CrosshairIndex.ShouldBe(124);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
        rig.Chart.CrosshairIndex.ShouldBe(114);
        rig.Terminal.Announcer!.Announcement!.ShouldContain("Open ");
        rig.Window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        rig.Chart.CrosshairIndex.ShouldBe(0);
        rig.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rig.Chart.CrosshairIndex.ShouldBe(125);

        var before = rig.Chart.TargetView;
        rig.Window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
        var zoomed = rig.Chart.TargetView;
        (zoomed.End - zoomed.Start).ShouldBeLessThan(before.End - before.Start);
        rig.Window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
        rig.Chart.TargetView.ShouldBe(before);
        rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        rig.Chart.CrosshairIndex.ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Moving_Past_The_Edge_Scrolls_The_View()
    {
        using var rig = new Rig(configure: t => t.Range = MarketRange.OneYear);
        rig.Chart.ZoomIn();
        rig.Chart.ZoomIn();
        rig.Chart.ZoomIn();
        var (start, _) = rig.Chart.TargetView;
        rig.Chart.MoveCrosshair((int)start - 5, announce: false);
        rig.Chart.TargetView.Start.ShouldBeLessThan(start);
        rig.Chart.TargetView.Start.ShouldBeLessThanOrEqualTo(rig.Chart.CrosshairIndex);
    }

    [AvaloniaFact]
    public void A_Tick_Updates_The_Header_The_Rows_And_The_Chart()
    {
        using var rig = new Rig();
        rig.Terminal.SetPrice("ORBT", 190.25);
        rig.Pump();
        rig.Terminal.PriceOf("ORBT").ShouldBe(190.25);
        TestHelpers.Descendant<AnimatedCounter>(rig.Terminal, "PART_Price").Value.ShouldBe(190.25);
        rig.Terminal.WatchRows[0].PriceText.ShouldBe("190.25");
        rig.Chart.ShownClose.ShouldBe(190.25);
        TestHelpers.Descendant<TextBlock>(rig.Terminal, "PART_ChangeText").Text.ShouldBe("+7.28 (+3.98%)");
    }

    [AvaloniaFact]
    public void Live_Ticks_Run_Only_While_Live()
    {
        using var rig = new Rig(configure: t => t.TickInterval = TimeSpan.FromMilliseconds(20));
        var price = rig.Terminal.PriceOf("ORBT");
        Thread.Sleep(60);
        rig.Pump();
        rig.Terminal.PriceOf("ORBT").ShouldBe(price);
        rig.Terminal.IsLive = true;
        TestHelpers.PumpUntil(() => rig.Terminal.PriceOf("ORBT") != price).ShouldBeTrue();
        var live = TestHelpers.Descendant<MarketLiveButton>(rig.Terminal, "PART_Live");
        AutomationProperties.GetName(live).ShouldBe("Pause live prices");
        live.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Terminal.IsLive.ShouldBeFalse();
        AutomationProperties.GetName(live).ShouldBe("Resume live prices");
    }

    #endregion

    #region Ticket

    [AvaloniaFact]
    public void The_Ticket_Explains_Why_An_Order_Cannot_Be_Placed()
    {
        using var rig = new Rig();
        rig.Terminal.OrderShares = 0;
        rig.Terminal.StatusText.ShouldBe("Enter at least one share.");
        rig.Terminal.SubmitButton!.IsEnabled.ShouldBeFalse();
        rig.Terminal.OrderShares = 100;
        rig.Terminal.StatusText.ShouldBe("Not enough buying power. You have $10,000.00.");
        rig.Terminal.OrderSide = MarketOrderSide.Sell;
        rig.Terminal.StatusText.ShouldBe("You hold only 20 ORBT.");
        rig.Terminal.OrderShares = 20;
        rig.Terminal.StatusText.ShouldBeNull();
        rig.Terminal.SubmitButton.IsEnabled.ShouldBeTrue();
        rig.Terminal.SubmitButton.Content.ShouldBe("Sell 20 ORBT");
    }

    [AvaloniaFact]
    public async Task A_Market_Order_Fills_At_The_Live_Price()
    {
        using var rig = new Rig();
        rig.Terminal.OrderShares = 10;
        var order = await rig.Terminal.SubmitOrderAsync();
        order.ShouldNotBeNull();
        order.Status.ShouldBe(MarketOrderStatus.Filled);
        order.Price.ShouldBe(184.53);
        rig.Terminal.BuyingPower.ShouldBe(10_000 - 1_845.30, 0.001);
        rig.Terminal.SharesHeld("ORBT").ShouldBe(30);
        rig.Terminal.Orders[0].ShouldBe(order);
        rig.Placed.ShouldBe([order]);
        rig.Filled.ShouldBe([order]);
        rig.Pump();
        rig.Terminal.OrderRows.First().Title.ShouldBe("Buy 10 ORBT");
        rig.Terminal.Announcer!.Announcement.ShouldBe("Buy 10 ORBT: Filled.");
    }

    [AvaloniaFact]
    public async Task A_Limit_Order_Waits_Until_The_Price_Reaches_It()
    {
        using var rig = new Rig();
        rig.Terminal.OrderType = MarketOrderType.Limit;
        rig.Terminal.LimitPrice.ShouldBe(184.53);
        rig.Terminal.LimitPrice = 180;
        rig.Terminal.OrderShares = 5;
        var order = (await rig.Terminal.SubmitOrderAsync())!;
        order.Status.ShouldBe(MarketOrderStatus.Working);
        rig.Terminal.SharesHeld("ORBT").ShouldBe(20);
        rig.Terminal.SetPrice("ORBT", 181);
        order.Status.ShouldBe(MarketOrderStatus.Working);
        rig.Terminal.SetPrice("ORBT", 179.5);
        order.Status.ShouldBe(MarketOrderStatus.Filled);
        order.Price.ShouldBe(180);
        rig.Terminal.SharesHeld("ORBT").ShouldBe(25);
        rig.Filled.ShouldBe([order]);
    }

    [AvaloniaFact]
    public async Task A_Marketable_Limit_Fills_At_Once_At_The_Better_Price()
    {
        using var rig = new Rig();
        rig.Terminal.OrderType = MarketOrderType.Limit;
        rig.Terminal.LimitPrice = 190;
        rig.Terminal.OrderShares = 2;
        var order = (await rig.Terminal.SubmitOrderAsync())!;
        order.Status.ShouldBe(MarketOrderStatus.Filled);
        order.Price.ShouldBe(184.53);
    }

    [AvaloniaFact]
    public async Task A_Working_Order_Can_Be_Canceled_From_The_List()
    {
        using var rig = new Rig();
        rig.Terminal.OrderSide = MarketOrderSide.Sell;
        rig.Terminal.OrderType = MarketOrderType.Limit;
        rig.Terminal.LimitPrice = 200;
        rig.Terminal.OrderShares = 5;
        var order = (await rig.Terminal.SubmitOrderAsync())!;
        rig.Pump();
        var row = rig.Terminal.OrderRows.First();
        row.CancelButton!.IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(row.CancelButton).ShouldBe("Cancel sell 5 ORBT at $200.00");
        (await rig.Terminal.CancelOrderAsync(order)).ShouldBeTrue();
        order.Status.ShouldBe(MarketOrderStatus.Canceled);
        rig.Canceled.ShouldBe([order]);
        row.StatusText.ShouldBe("Canceled");
        row.CancelButton.IsVisible.ShouldBeFalse();
        rig.Terminal.SetPrice("ORBT", 210);
        order.Status.ShouldBe(MarketOrderStatus.Canceled);
        (await rig.Terminal.CancelOrderAsync(order)).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task A_Failed_Callback_Leaves_The_Order_Unplaced_And_Says_Why()
    {
        using var rig = new Rig(configure: t => t.PlaceOrderAction = (_, _) => Task.FromException(new InvalidOperationException("Market is closed.")));
        var order = await rig.Terminal.SubmitOrderAsync();
        order.ShouldBeNull();
        rig.Terminal.Orders.ShouldBeEmpty();
        rig.Terminal.StatusText.ShouldBe("Market is closed.");
        rig.Terminal.OrderShares = 3;
        rig.Terminal.StatusText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task The_Button_Stays_Busy_While_The_Callback_Runs()
    {
        var gate = new TaskCompletionSource();
        MarketOrderRequest? seen = null;
        using var rig = new Rig(configure: t => t.PlaceOrderAction = (request, _) =>
        {
            seen = request;
            return gate.Task;
        });
        var pending = rig.Terminal.SubmitOrderAsync();
        rig.Terminal.SubmitButton!.IsEnabled.ShouldBeFalse();
        ((Button)rig.Terminal.SubmitButton).IsLoading.ShouldBeTrue();
        seen.ShouldBe(new MarketOrderRequest(MarketOrderSide.Buy, MarketOrderType.Market, "ORBT", 10, 184.53));
        gate.SetResult();
        var order = await pending;
        order!.Status.ShouldBe(MarketOrderStatus.Filled);
        ((Button)rig.Terminal.SubmitButton).IsLoading.ShouldBeFalse();
    }

    #endregion

    #region Layout and motion

    [AvaloniaFact]
    public void The_Cards_Rearrange_With_The_Width()
    {
        using var rig = new Rig(width: 1300);
        rig.Terminal.Classes.Contains(MarketTerminal.WidePseudoClass).ShouldBeTrue();
        var watch = TestHelpers.Descendant<Border>(rig.Terminal, MarketTerminal.WatchCardPart);
        var chart = TestHelpers.Descendant<Border>(rig.Terminal, MarketTerminal.ChartCardPart);
        watch.Bounds.X.ShouldBeGreaterThan(chart.Bounds.Right);
        watch.Bounds.Width.ShouldBe(340, 0.5);

        rig.Host.Width = 900;
        rig.Pump();
        rig.Terminal.Classes.Contains(MarketTerminal.MediumPseudoClass).ShouldBeTrue();
        var ticket = TestHelpers.Descendant<Border>(rig.Terminal, MarketTerminal.TicketCardPart);
        watch.Bounds.Y.ShouldBeGreaterThan(chart.Bounds.Bottom);
        ticket.Bounds.Y.ShouldBe(watch.Bounds.Y, 0.5);
        ticket.Bounds.X.ShouldBeGreaterThan(watch.Bounds.Right);

        rig.Host.Width = 600;
        rig.Pump();
        rig.Terminal.Classes.Contains(MarketTerminal.NarrowPseudoClass).ShouldBeTrue();
        rig.Terminal.Classes.Contains(MarketTerminal.CompactPseudoClass).ShouldBeTrue();
        ticket.Bounds.Y.ShouldBeGreaterThan(watch.Bounds.Bottom);
        rig.Chart.Bounds.Height.ShouldBe(340, 0.5);
    }

    [AvaloniaFact]
    public void Zooming_Eases_The_View_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(level: ArcMotionLevel.Full);
        var (start, end) = rig.Chart.View;
        rig.Chart.ZoomIn();
        rig.Chart.View.ShouldBe((start, end));
        clock.Advance(TimeSpan.FromMilliseconds(48));
        var mid = rig.Chart.View;
        var target = rig.Chart.TargetView;
        mid.Start.ShouldBeInRange(Math.Min(start, target.Start) + 1e-6, Math.Max(start, target.Start) - 1e-6);
        clock.Advance(TimeSpan.FromMilliseconds(800));
        rig.Chart.View.Start.ShouldBe(target.Start, 0.01);
        rig.Chart.IsMoving.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Price_Flashes_Fade_Under_Full_Motion_And_Skip_Without_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using (var rig = new Rig(level: ArcMotionLevel.Full))
        {
            rig.Terminal.SetPrice("KLVN", 65);
            var row = rig.Terminal.WatchRows[1];
            row.IsFlashUp.ShouldBeTrue();
            row.FlashOpacity.ShouldBe(1, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(1000));
            row.FlashOpacity.ShouldBe(0, 0.01);
        }
        using (var rig = new Rig())
        {
            rig.Terminal.SetPrice("KLVN", 60);
            rig.Terminal.WatchRows[1].FlashOpacity.ShouldBe(0);
            rig.Terminal.WatchRows[1].IsFlashUp.ShouldBeFalse();
        }
    }

    #endregion
}
