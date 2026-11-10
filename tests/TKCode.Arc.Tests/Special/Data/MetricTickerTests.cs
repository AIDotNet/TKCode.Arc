using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Special.Data;

/// <summary>The metric ticker: changes and trends, the two layouts, rolling values, scrolling and pausing.</summary>
public class MetricTickerTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static MetricTicker Ticker(MetricTickerMode mode = MetricTickerMode.Scroll)
    {
        var ticker = new MetricTicker { Mode = mode, Width = 360 };
        ticker.Items.Add(new MetricTickerItem("Requests / s", 1000));
        ticker.Items.Add(new MetricTickerItem("p95 latency", 200, suffix: " ms") { IsHigherBetter = false });
        ticker.Items.Add(new MetricTickerItem("Active users", 8000));
        ticker.Items.Add(new MetricTickerItem("Queue depth", 40) { ChangeFormat = MetricTickerChangeFormat.Absolute });
        return ticker;
    }

    [AvaloniaFact]
    public void The_First_Value_Is_A_Start_And_Later_Ones_Are_Changes()
    {
        var item = new MetricTickerItem("Orders", 200);
        double.IsNaN(item.Change).ShouldBeTrue();
        item.FormatChange().ShouldBeNull();
        item.Trend.ShouldBe(0);
        item.Value = 210;
        item.Change.ShouldBe(10);
        item.PreviousValue.ShouldBe(200);
        item.FormatChange().ShouldBe("+5.0%");
        item.Trend.ShouldBe(1);
        item.IsHigherBetter = false;
        item.Trend.ShouldBe(-1);
        item.Value = 200;
        item.FormatChange().ShouldBe("−4.8%");
        item.ChangeFormat = MetricTickerChangeFormat.Absolute;
        item.Suffix = " ms";
        item.FormatChange().ShouldBe("−10 ms");
        item.ChangeFormat = MetricTickerChangeFormat.None;
        item.FormatChange().ShouldBeNull();
    }

    [AvaloniaFact]
    public void Columns_Show_Each_Metric_Once_And_Wrap()
    {
        var ticker = Ticker(MetricTickerMode.Roll);
        var window = TestHelpers.Show(ticker);
        try
        {
            var row = ticker.Strip.ShouldNotBeNull().Row;
            row.Cells.Count.ShouldBe(4);
            row.Columns.ShouldBeLessThan(4, "360 px holds fewer than four 150 px columns");
            ticker.Strip.IsScrollLayout.ShouldBeFalse();
            TestHelpers.Descendant<AvaloniaButton>(ticker, MetricTicker.PauseButtonPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_Shows_Two_Copies_And_Reduced_Motion_Falls_Back_To_Columns()
    {
        var scrolling = Ticker();
        var window = TestHelpers.Show(scrolling, ArcMotionLevel.Full);
        try
        {
            var row = scrolling.Strip.ShouldNotBeNull().Row;
            row.Cells.Count.ShouldBe(8);
            row.CopyWidth.ShouldBeGreaterThan(0);
            AutomationProperties.GetAccessibilityView(row.Cells[5]).ShouldBe(AccessibilityView.Raw, "the second copy is not read twice");
            scrolling.Classes.Contains(MetricTicker.ScrollingPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
        var reduced = Ticker();
        window = TestHelpers.Show(reduced, ArcMotionLevel.Reduced);
        try
        {
            reduced.Strip.ShouldNotBeNull().Row.Cells.Count.ShouldBe(4);
            reduced.Classes.Contains(MetricTicker.ScrollingPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Update_Rolls_The_Value_And_Shows_The_Change_In_Its_Color()
    {
        var ticker = Ticker(MetricTickerMode.Roll);
        var window = TestHelpers.Show(ticker);
        try
        {
            var cells = ticker.Strip!.Row.Cells;
            ticker.Items[0].Value = 1100;
            ticker.Items[1].Value = 220;
            Dispatcher.UIThread.RunJobs();
            cells[0].ShownValue.ShouldBe(1100);
            cells[0].ChangeText.ShouldBe("+10.0%");
            cells[0].Trend.ShouldBe(1);
            cells[1].ChangeText.ShouldBe("+10.0%");
            cells[1].Trend.ShouldBe(-1, "a slower response is bad news");
            AutomationProperties.GetName(cells[1]).ShouldBe("p95 latency, 220 ms, up 10.0%");
            cells[2].ChangeText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Line_Scrolls_On_Frames_And_Stops_When_Paused()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var ticker = Ticker();
        ticker.Speed = 60;
        var window = TestHelpers.Show(ticker, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            Dispatcher.UIThread.RunJobs();
            var strip = ticker.Strip.ShouldNotBeNull();
            var start = strip.Offset;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            Dispatcher.UIThread.RunJobs();
            (strip.Offset - start).ShouldBe(60, 4);

            var pause = TestHelpers.Descendant<AvaloniaButton>(ticker, MetricTicker.PauseButtonPart);
            pause.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(pause).ShouldBe("Pause ticker");
            pause.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            ticker.IsPaused.ShouldBeTrue();
            AutomationProperties.GetName(pause).ShouldBe("Play ticker");
            var held = strip.Offset;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            Dispatcher.UIThread.RunJobs();
            strip.Offset.ShouldBe(held);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bound_List_Replaces_The_Items()
    {
        var ticker = Ticker(MetricTickerMode.Roll);
        var window = TestHelpers.Show(ticker);
        try
        {
            var source = new System.Collections.ObjectModel.ObservableCollection<MetricTickerItem> { new("Only", 1) };
            ticker.ItemsSource = source;
            ticker.Strip!.Row.Cells.Count.ShouldBe(1);
            source.Add(new MetricTickerItem("Second", 2));
            ticker.Strip.Row.Cells.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
