using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Controls and motion of Blocks/Analytics/_Shared (headless).</summary>
public class AnalyticsSharedControlTests
{
    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 800;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Segment_Switch_Moves_With_Wrapping_Arrows_And_Keeps_One_Tab_Stop()
    {
        var segments = new AnalyticsSegmentSwitch
        {
            Items = ["7D", "30D", "90D"], AccessibleNames = ["Last 7 days", "Last 30 days", "Last 90 days"], SelectedIndex = 1,
            Look = AnalyticsSegmentLook.Small
        };
        var changes = new List<AnalyticsSegmentChangedEventArgs>();
        segments.SelectionChanged += (_, e) => changes.Add(e);
        var window = Show(segments);
        try
        {
            segments.Buttons.Count.ShouldBe(3);
            segments.Buttons[1].IsChecked.ShouldBe(true);
            segments.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            AutomationProperties.GetName(segments.Buttons[0]).ShouldBe("Last 7 days");
            segments.Buttons[0].Bounds.Height.ShouldBe(26);
            segments.Buttons[0].Bounds.Width.ShouldBeGreaterThanOrEqualTo(40);
            segments.Bounds.Height.ShouldBe(32);
            segments.Thumb.HasTarget.ShouldBeTrue();
            segments.Thumb.CurrentRect.X.ShouldBe(segments.Buttons[1].Bounds.X, 0.01);

            segments.Buttons[1].Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            segments.SelectedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            segments.SelectedIndex.ShouldBe(0);
            segments.Buttons[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            segments.SelectedIndex.ShouldBe(2);
            changes.Count.ShouldBe(3);
            changes.ShouldAllBe(c => c.ByUser);
            segments.Buttons.Count(b => b.IsTabStop).ShouldBe(1);
            segments.Buttons[2].IsTabStop.ShouldBeTrue();

            segments.SelectedIndex = -1;
            Dispatcher.UIThread.RunJobs();
            changes[^1].ByUser.ShouldBeFalse();
            segments.Thumb.HasTarget.ShouldBeFalse();
            segments.Buttons[0].IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segment_Switch_Click_Selects_And_Stretch_Shares_The_Width()
    {
        var segments = new AnalyticsSegmentSwitch { Items = ["A", "B", "C"], SelectedIndex = 0, IsStretched = true, Width = 300 };
        var window = Show(segments);
        try
        {
            segments.Buttons[0].Bounds.Width.ShouldBe(segments.Buttons[2].Bounds.Width, 0.01);
            segments.Buttons[2].Bounds.Right.ShouldBe(300 - 2 - 6, 0.01);
            var center = segments.Buttons[2].TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            segments.SelectedIndex.ShouldBe(2);
            segments.Buttons[2].IsChecked.ShouldBe(true);
            segments.Buttons[0].IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Segmented_Control_Takes_The_Smooth_Spring()
    {
        var control = new SegmentedControl { ItemsSource = new[] { "One", "Two" }, SelectedIndex = 0 };
        AnalyticsSegmented.SetSpring(control, ArcSpring.Smooth);
        var window = Show(control);
        try
        {
            var indicator = control.GetVisualDescendants().OfType<GlidingIndicator>().First(i => i.Name == SegmentedControl.SelectionPart);
            indicator.Spring.ShouldBe(ArcSpring.Smooth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delta_Speaks_The_Direction_And_Takes_Its_Tone()
    {
        var delta = new AnalyticsDelta { Context = "since Jul" };
        delta.Show(-0.7, AnalyticsNumberFormat.Points(1), lowerIsBetter: true);
        var window = Show(delta);
        try
        {
            delta.Tone.ShouldBe(AnalyticsTone.Good);
            delta.Direction.ShouldBe(AnalyticsDirection.Down);
            delta.Text.ShouldBe("0.7 pts");
            AutomationProperties.GetName(delta).ShouldBe("down 0.7 pts since Jul");
            delta.Classes.Contains(":good").ShouldBeTrue();
            var success = ArcTokens.GetColor(delta, DesignTokens.ArcDesignTokenKind.Success);
            ((ISolidColorBrush)delta.Foreground!).Color.ShouldBe(success);
            var arrow = delta.GetVisualDescendants().OfType<ArcGlyph>().Single();
            arrow.Data.ShouldBe(ArcIcons.ArrowUpRight);

            delta.Show(0.04, AnalyticsNumberFormat.Percent(1));
            delta.Tone.ShouldBe(AnalyticsTone.Flat);
            arrow.Data.ShouldBe(ArcIcons.ArrowRight);
            delta.Show(-3.4, AnalyticsNumberFormat.Percent(1, signed: true));
            delta.Text.ShouldBe("−3.4%");
            delta.Tone.ShouldBe(AnalyticsTone.Bad);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kpi_Tile_Reads_As_One_Sentence_And_Moves_Its_Delta_Inline()
    {
        var tile = new AnalyticsKpiTile
        {
            Label = "Sessions", Value = 36686, Change = 11.7, NumberMotion = AnalyticsNumberMotion.CountUp
        };
        var window = Show(tile);
        try
        {
            tile.SpokenText.ShouldBe("Sessions, 36,686, up 11.7%");
            AutomationProperties.GetName(tile).ShouldBe("Sessions, 36,686, up 11.7%");
            tile.CountingValue.DisplayText.ShouldBe("36,686");
            tile.RollingValue.IsVisible.ShouldBeFalse();
            tile.Delta.GetVisualParent().ShouldBeOfType<StackPanel>().Orientation.ShouldBe(Avalonia.Layout.Orientation.Vertical);
            tile.IsDeltaInline = true;
            tile.Delta.GetVisualParent().ShouldBeOfType<StackPanel>().Orientation.ShouldBe(Avalonia.Layout.Orientation.Horizontal);
            tile.Change = null;
            tile.Delta.IsVisible.ShouldBeFalse();
            tile.NumberMotion = AnalyticsNumberMotion.Roll;
            tile.Format = AnalyticsNumberFormat.Compact(3, "$");
            tile.Value = 2_561_000;
            tile.SpokenText.ShouldBe("Sessions, $2.56M");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rolling_Number_Splits_Sign_Symbol_And_Unit()
    {
        var number = new AnalyticsRollingNumber { Format = AnalyticsNumberFormat.Currency(0, "$", signed: true), Amount = -13_800 };
        number.Prefix.ShouldBe("−$");
        number.Value.ShouldBe(13_800);
        number.Suffix.ShouldBe(string.Empty);
        number.FullText.ShouldBe("−$13,800");
        number.Falloff.ShouldBe(RollingDigitsFalloff.Linear);
        number.Format = AnalyticsNumberFormat.Compact(3, "$");
        number.Amount = 63_300;
        number.Prefix.ShouldBe("$");
        number.Value.ShouldBe(63.3);
        number.Decimals.ShouldBe(1);
        number.Suffix.ShouldBe("k");
        AutomationProperties.GetName(number).ShouldBe("$63.3k");
    }

    [AvaloniaFact]
    public void Count_Up_Tweens_480ms_And_Starts_At_Rest()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var count = new AnalyticsCountUp { Value = 100 };
        var window = Show(count, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            count.DisplayText.ShouldBe("100");
            count.Value = 200;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(count).ShouldBe("200");
            clock.Advance(TimeSpan.FromMilliseconds(120));
            count.DisplayValue.ShouldBeInRange(101, 199);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            count.DisplayText.ShouldBe("200");

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            count.Value = 300;
            count.DisplayText.ShouldBe("300");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Swap_Rises_Under_Full_Motion_And_Crossfades_Under_Reduced()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var swap = new AnalyticsTextSwap { Text = "Last 30 days", FontSize = 14 };
        var window = Show(swap, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            swap.CurrentOpacity.ShouldBe(1);
            swap.Text = "Last 90 days";
            Dispatcher.UIThread.RunJobs();
            swap.LeavingCount.ShouldBe(1);
            swap.CurrentOffset.ShouldBe(6.0 / 14, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            swap.CurrentOpacity.ShouldBeInRange(0.05, 0.99);
            swap.CurrentOffset.ShouldBeInRange(0.001, 6.0 / 14);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            swap.CurrentOpacity.ShouldBe(1);
            swap.CurrentOffset.ShouldBe(0);
            swap.LeavingCount.ShouldBe(0);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            swap.Text = "Last 7 days";
            Dispatcher.UIThread.RunJobs();
            swap.CurrentOffset.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            swap.CurrentOpacity.ShouldBeInRange(0.05, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            swap.CurrentOpacity.ShouldBe(1);

            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            swap.Text = "Today";
            swap.LeavingCount.ShouldBe(0);
            swap.CurrentOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spring_Array_Morphs_Reshapes_And_Settles()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var host = new Border { Width = 100, Height = 100 };
        var window = Show(host, ArcMotionLevel.Full);
        using var springs = new AnalyticsSpringArray(host);
        try
        {
            var settled = 0;
            springs.Settled += () => settled++;
            springs.SetTargets([0, 0, 0]);
            springs.Values.ToArray().ShouldBe([0, 0, 0]);
            springs.SetTargets([10, 20, 30]);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            springs[1].ShouldBeInRange(0.5, 19.5);
            springs.SetTargets([0, 0, 0, 0, 0]);
            springs.Count.ShouldBe(5);
            clock.Advance(TimeSpan.FromSeconds(2));
            springs.IsMoving.ShouldBeFalse();
            springs.Values.ToArray().ShouldAllBe(v => v == 0);
            settled.ShouldBeGreaterThan(0);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            springs.SetTargets([5, 5, 5]);
            springs.Values.ToArray().ShouldBe([5, 5, 5]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Share_Bar_Tweens_And_Veil_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var bar = new AnalyticsShareBar { Height = 6, Width = 200, Value = 0.2, Motion = AnalyticsBarMotion.Tween };
        var veil = new AnalyticsVeil { Width = 200, Height = 50 };
        var stack = new StackPanel();
        stack.Children.Add(bar);
        stack.Children.Add(veil);
        var window = Show(stack, ArcMotionLevel.Full);
        try
        {
            bar.DisplayValue.ShouldBe(0.2);
            bar.Value = 0.8;
            clock.Advance(TimeSpan.FromMilliseconds(120));
            bar.DisplayValue.ShouldBeInRange(0.21, 0.79);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            bar.DisplayValue.ShouldBe(0.8);

            veil.IsVisible.ShouldBeFalse();
            veil.IsShown = true;
            veil.IsVisible.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            veil.Progress.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            veil.Progress.ShouldBe(1);
            veil.IsShown = false;
            clock.Advance(TimeSpan.FromMilliseconds(400));
            veil.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ranked_Row_Reads_Name_And_Value()
    {
        var row = new AnalyticsRankedRow { Label = "/features", Value = 5912, Share = 0.55 };
        var window = Show(row);
        try
        {
            row.Bounds.Height.ShouldBe(30);
            AutomationProperties.GetName(row).ShouldBe("/features, 5,912");
            row.Bar.DisplayValue.ShouldBe(0.55);
            row.ValueText.DisplayText.ShouldBe("5,912");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Announcer_Repeats_And_Palette_Reads_Tokens()
    {
        var announcer = new AnalyticsAnnouncer();
        var host = new StackPanel();
        host.Children.Add(announcer);
        var window = Show(host);
        try
        {
            announcer.Announce("Range: last 30 days");
            announcer.Announce("Range: last 30 days");
            AutomationProperties.GetName(announcer).ShouldBe("Range: last 30 days​");
            announcer.History.Count.ShouldBe(2);
            var palette = new AnalyticsPalette();
            palette.Refresh(host);
            palette.Accent.A.ShouldBe((byte)255);
            palette.Version.ShouldBe(1);
            palette.Brush(palette.Accent, 0.5).Color.A.ShouldBeInRange((byte)126, (byte)129);
            palette.Pen(palette.TextMuted, 1.25, dash: 3, gap: 3).DashStyle.ShouldNotBeNull();
            palette.ToneColor(AnalyticsTone.Bad).ShouldBe(palette.Danger);
        }
        finally
        {
            window.Close();
        }
    }
}
