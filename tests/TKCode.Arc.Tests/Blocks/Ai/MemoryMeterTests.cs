using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The agent memory's capacity meter: the percentage, the warning, the spring and what screen readers get.</summary>
public class MemoryMeterTests
{
    private const double Capacity = 2000;

    // Headless text is one font size wide per character ("full" alone would be 48px): a one-letter word keeps the line
    // inside 96px the way Inter keeps "100% full" (about 55px) inside it.
    private const string Word = "f";

    private static MemoryMeter Create(double used = 1240, double capacity = Capacity) => new()
    {
        Used = used,
        Capacity = capacity,
        FullText = Word,
        AccessibleName = "Memory used",
        ValueText = percent => $"{percent}% full"
    };

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off, double width = 400)
    {
        var host = new Border { Width = width, Padding = new Thickness(20), Child = content, HorizontalAlignment = HorizontalAlignment.Left };
        var window = TestHelpers.Show(host, level);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static TextBlock Value(MemoryMeter meter) => TestHelpers.Descendant<TextBlock>(meter, MemoryMeter.ValuePart);

    private static TextBlock Full(MemoryMeter meter) => TestHelpers.Descendant<TextBlock>(meter, "PART_Full");

    private static ArcGlyph Glyph(MemoryMeter meter) => TestHelpers.Descendant<ArcGlyph>(meter, "PART_Warning");

    private static MemoryMeterBar Bar(MemoryMeter meter) => TestHelpers.Descendant<MemoryMeterBar>(meter, MemoryMeter.BarPart);

    private static Rect BoundsIn(Visual visual, Visual root) =>
        new(visual.TranslatePoint(default, root)!.Value, visual.Bounds.Size);

    #region Geometry and words

    [AvaloniaFact]
    public void Lays_Out_A_Right_Aligned_Line_Over_A_96_By_4_Track()
    {
        var meter = Create();
        var window = Show(meter);
        try
        {
            meter.Focusable.ShouldBeFalse();
            meter.Bounds.Width.ShouldBe(96);
            // 4px from the top, a 16px line, 7px, a 4px track.
            meter.Bounds.Height.ShouldBe(31);
            var line = TestHelpers.Descendant<StackPanel>(meter, "PART_Line");
            BoundsIn(line, meter).Top.ShouldBe(4);
            line.Bounds.Height.ShouldBe(16);
            BoundsIn(line, meter).Right.ShouldBe(96, 0.01);
            var bar = Bar(meter);
            BoundsIn(bar, meter).ShouldBe(new Rect(0, 27, 96, 4));
            // The number, then "full", 4px apart; 12 / 16 text tracked like the body.
            var value = Value(meter);
            var full = Full(meter);
            value.Text.ShouldBe("62%");
            full.Text.ShouldBe(Word);
            value.FontSize.ShouldBe(12);
            value.LineHeight.ShouldBe(16);
            full.FontSize.ShouldBe(12);
            (BoundsIn(full, meter).Left - BoundsIn(value, meter).Right).ShouldBe(4, 0.01);
            value.FontFeatures!.Select(feature => feature.Tag).ShouldContain("tnum");
            // The warning glyph is not there at rest.
            Glyph(meter).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Number_Sits_In_A_Slot_As_Wide_As_100_Percent()
    {
        var meter = Create(used: 0);
        var window = Show(meter);
        try
        {
            var slot = TestHelpers.Descendant<Panel>(meter, "PART_ValueSlot");
            var reserve = TestHelpers.Descendant<TextBlock>(meter, MemoryMeter.ValueReservePart);
            reserve.Text.ShouldBe("100%");
            reserve.Opacity.ShouldBe(0);
            var narrow = BoundsIn(slot, meter);
            var full = BoundsIn(Full(meter), meter);
            Value(meter).Text.ShouldBe("0%");
            // Right-aligned in the slot: the "%" stays put whatever the number.
            BoundsIn(Value(meter), meter).Right.ShouldBe(narrow.Right, 0.01);
            meter.Used = Capacity;
            window.UpdateLayout();
            Value(meter).Text.ShouldBe("100%");
            BoundsIn(slot, meter).ShouldBe(narrow);
            BoundsIn(Full(meter), meter).ShouldBe(full);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Never_Shrinks_Below_96()
    {
        var meter = Create();
        var window = Show(meter, width: 80);
        try
        {
            meter.Bounds.Width.ShouldBe(96);
            Bar(meter).Bounds.Width.ShouldBe(96);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Line_Wider_Than_96_Widens_The_Meter_Instead_Of_Clipping()
    {
        var meter = Create(used: 1860);
        meter.FullText = "full of memories";
        var window = Show(meter);
        try
        {
            var line = TestHelpers.Descendant<StackPanel>(meter, "PART_Line");
            line.DesiredSize.Width.ShouldBeGreaterThan(96);
            meter.Bounds.Width.ShouldBe(line.DesiredSize.Width, 0.01);
            BoundsIn(line, meter).Left.ShouldBe(0, 0.01);
            Bar(meter).Bounds.Width.ShouldBe(meter.Bounds.Width, 0.01);
            meter.ClipToBounds.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Missing_Words_Leave_The_Number_Alone()
    {
        var meter = new MemoryMeter { Used = 500, Capacity = 1000 };
        var window = Show(meter);
        try
        {
            Value(meter).Text.ShouldBe("50%");
            Full(meter).IsVisible.ShouldBeFalse();
            meter.ValueTextFor(50).ShouldBe("50%");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Percent and warning

    [AvaloniaTheory]
    [InlineData(0, 0, false)]
    [InlineData(1240, 62, false)]
    [InlineData(1780, 89, false)]
    [InlineData(1799, 89, false)]
    [InlineData(1800, 90, true)]
    [InlineData(1860, 93, true)]
    [InlineData(1999, 99, true)]
    [InlineData(2000, 100, true)]
    [InlineData(2600, 100, true)]
    public void Shows_The_Whole_Percent_And_Warns_From_90(double used, int percent, bool warning)
    {
        var meter = Create(used);
        var window = Show(meter);
        try
        {
            meter.DisplayedPercent.ShouldBe(percent);
            meter.TargetPercent.ShouldBe(percent);
            Value(meter).Text.ShouldBe($"{percent}%");
            meter.IsWarning.ShouldBe(warning);
            meter.Classes.Contains(MemoryMeter.WarningPseudoClass).ShouldBe(warning);
            Glyph(meter).IsVisible.ShouldBe(warning);
            // The fill covers the share, never more than the track.
            meter.FillRatio.ShouldBe(Math.Min(1, used / Capacity), 1e-9);
            Bar(meter).Bounds.Width.ShouldBe(96);
            Bar(meter).FillWidth.ShouldBe(96 * Math.Min(1, used / Capacity), 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rounding_Down_Keeps_90_For_The_Warning_And_100_For_Full()
    {
        MemoryMeter.ToPercent(0.29).ShouldBe(29);
        MemoryMeter.ToPercent(0.8999).ShouldBe(89);
        MemoryMeter.ToPercent(0.9).ShouldBe(90);
        MemoryMeter.ToPercent(0.9999).ShouldBe(99);
        MemoryMeter.ToPercent(1).ShouldBe(100);
        MemoryMeter.ToPercent(1.4).ShouldBe(100);
        MemoryMeter.ToPercent(-0.2).ShouldBe(0);
        MemoryMeter.ToPercent(double.NaN).ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Warning_Takes_The_Text_The_Number_The_Glyph_And_The_Fill()
    {
        var meter = Create();
        var window = Show(meter);
        try
        {
            var warning = ArcTokens.GetColor(meter, ArcDesignTokenKind.Warning);
            TestHelpers.ColorOf(Value(meter).Foreground).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(Full(meter).Foreground).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(Bar(meter).FillBrush).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.Accent));
            TestHelpers.ColorOf(Bar(meter).TrackBrush).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.SurfaceMuted));
            var right = BoundsIn(Full(meter), meter).Right;

            meter.Used = 1860;
            window.UpdateLayout();
            meter.IsWarning.ShouldBeTrue();
            var glyph = Glyph(meter);
            glyph.IsVisible.ShouldBeTrue();
            glyph.Size.ShouldBe(13);
            glyph.StrokeThickness.ShouldBe(2);
            TestHelpers.ColorOf(glyph.Foreground).ShouldBe(warning);
            TestHelpers.ColorOf(Value(meter).Foreground).ShouldBe(warning);
            TestHelpers.ColorOf(Full(meter).Foreground).ShouldBe(warning);
            TestHelpers.ColorOf(Bar(meter).FillBrush).ShouldBe(warning);
            // The glyph comes in front, 4px before the number's slot; the right-aligned line does not move.
            var slot = TestHelpers.Descendant<Panel>(meter, "PART_ValueSlot");
            (BoundsIn(slot, meter).Left - BoundsIn(glyph, meter).Right).ShouldBe(4, 0.01);
            BoundsIn(Value(meter), meter).Right.ShouldBe(BoundsIn(slot, meter).Right, 0.01);
            meter.Bounds.Width.ShouldBe(96);
            BoundsIn(Full(meter), meter).Right.ShouldBe(right, 0.01);

            meter.Used = 1240;
            window.UpdateLayout();
            meter.IsWarning.ShouldBeFalse();
            glyph.IsVisible.ShouldBeFalse();
            TestHelpers.ColorOf(Bar(meter).FillBrush).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.Accent));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Warning_Colors_Switch_At_Once_Even_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var window = Show(meter, ArcMotionLevel.Full);
        try
        {
            CoreFrames.Run(clock, 2);
            meter.Used = 1860;
            window.UpdateLayout();
            // The number is still on its way, but the colors are already the warning's: no transitions anywhere.
            meter.DisplayedPercent.ShouldBe(62);
            TestHelpers.ColorOf(Value(meter).Foreground).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.Warning));
            TestHelpers.ColorOf(Bar(meter).FillBrush).ShouldBe(ArcTokens.GetColor(meter, ArcDesignTokenKind.Warning));
            meter.Transitions.ShouldBeNull();
            Value(meter).Transitions.ShouldBeNull();
            Full(meter).Transitions.ShouldBeNull();
            Bar(meter).Transitions.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Warning_Ratio_Moves_The_Threshold()
    {
        var meter = Create(used: 1500);
        var window = Show(meter);
        try
        {
            meter.IsWarning.ShouldBeFalse();
            meter.WarningRatio = 0.75;
            meter.IsWarning.ShouldBeTrue();
            meter.Classes.ShouldContain(MemoryMeter.WarningPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Capacity_Moves_The_Fill_And_Zero_Shows_Nothing_Used()
    {
        var meter = Create();
        var window = Show(meter);
        try
        {
            meter.Capacity = 4000;
            meter.DisplayedPercent.ShouldBe(31);
            meter.FillRatio.ShouldBe(0.31, 1e-9);
            meter.Capacity = 1300;
            meter.DisplayedPercent.ShouldBe(95);
            meter.IsWarning.ShouldBeTrue();
            meter.Capacity = 0;
            meter.DisplayedPercent.ShouldBe(0);
            meter.FillRatio.ShouldBe(0);
            meter.IsWarning.ShouldBeFalse();
            Value(meter).Text.ShouldBe("0%");
            meter.Capacity = -5;
            meter.DisplayedPercent.ShouldBe(0);
            meter.IsWarning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void The_Fill_Follows_The_Smooth_Spring_And_Rests_On_The_Target()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var window = Show(meter, ArcMotionLevel.Full);
        try
        {
            CoreFrames.Run(clock, 2);
            meter.FillRatio.ShouldBe(0.62, 1e-9);
            meter.Used = 1320;
            meter.TargetRatio.ShouldBe(0.66, 1e-9);
            meter.IsMoving.ShouldBeTrue();
            meter.FillRatio.ShouldBe(0.62, 1e-9);
            var t = CoreFrames.Run(clock, 8);
            var expected = 0.66 + SpringSolver.Evaluate(ArcMotionPresets.Smooth, 0.62 - 0.66, 0, t).Displacement;
            meter.FillRatio.ShouldBe(expected, 1e-6);
            meter.FillRatio.ShouldBeGreaterThan(0.62);
            meter.FillRatio.ShouldBeLessThan(0.66);
            Bar(meter).FillWidth.ShouldBe(96 * meter.FillRatio, 1e-6);
            // Measured on Arc: about half the way after 132ms.
            var half = SpringSolver.Evaluate(ArcMotionPresets.Smooth, 1, 0, 0.132).Displacement;
            half.ShouldBe(0.5, 0.15);
            CoreFrames.Run(clock, 90);
            meter.IsMoving.ShouldBeFalse();
            meter.FillRatio.ShouldBe(0.66, 1e-9);
            meter.DisplayedPercent.ShouldBe(66);
            Value(meter).Text.ShouldBe("66%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Number_Changes_Only_When_The_Whole_Percent_Does()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var window = Show(meter, ArcMotionLevel.Full);
        try
        {
            CoreFrames.Run(clock, 2);
            var seen = new List<int>();
            var texts = new List<string?>();
            meter.PropertyChanged += (_, e) =>
            {
                if (e.Property == MemoryMeter.DisplayedPercentProperty)
                {
                    seen.Add(meter.DisplayedPercent);
                    texts.Add(Value(meter).Text);
                }
            };
            var frames = 0;
            meter.Used = 1320;
            while (meter.IsMoving && frames < 120)
            {
                CoreFrames.Run(clock, 1);
                frames++;
            }
            // Many frames, but only one change per whole percent, counted up one by one in step with the text.
            frames.ShouldBeGreaterThan(seen.Count);
            seen.ShouldBe([63, 64, 65, 66]);
            texts.ShouldBe(["63%", "64%", "65%", "66%"]);

            seen.Clear();
            meter.Used = 1240;
            CoreFrames.Run(clock, 120);
            seen.ShouldBe([65, 64, 63, 62]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Jump(ArcMotionLevel level)
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var window = Show(meter, level);
        try
        {
            var seen = new List<int>();
            meter.PropertyChanged += (_, e) =>
            {
                if (e.Property == MemoryMeter.DisplayedPercentProperty)
                {
                    seen.Add(meter.DisplayedPercent);
                }
            };
            meter.Used = 1860;
            meter.IsMoving.ShouldBeFalse();
            meter.FillRatio.ShouldBe(0.93, 1e-9);
            meter.DisplayedPercent.ShouldBe(93);
            seen.ShouldBe([93]);
            Value(meter).Text.ShouldBe("93%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_First_Value_Does_Not_Animate()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create(used: 1860);
        var window = Show(meter, ArcMotionLevel.Full);
        try
        {
            meter.IsMoving.ShouldBeFalse();
            meter.DisplayedPercent.ShouldBe(93);
            Value(meter).Text.ShouldBe("93%");
            Bar(meter).Ratio.ShouldBe(0.93, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Mid_Spring_Lands_On_The_Data()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var host = new Border { Child = meter };
        var window = Show(host, ArcMotionLevel.Full);
        try
        {
            CoreFrames.Run(clock, 2);
            meter.Used = 400;
            CoreFrames.Run(clock, 3);
            meter.IsMoving.ShouldBeTrue();
            host.Child = null;
            meter.IsMoving.ShouldBeFalse();
            meter.FillRatio.ShouldBe(0.2, 1e-9);
            meter.DisplayedPercent.ShouldBe(20);
            host.Child = meter;
            window.UpdateLayout();
            Value(meter).Text.ShouldBe("20%");
            Bar(meter).Ratio.ShouldBe(0.2, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void Reads_As_A_Read_Only_Meter_With_The_Data_Percent_And_Value_Text()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var meter = Create();
        var window = Show(meter, ArcMotionLevel.Full);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(meter);
            peer.GetName().ShouldBe("Memory used");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ProgressBar);
            peer.GetChildren().ShouldBeEmpty();
            peer.IsKeyboardFocusable().ShouldBeFalse();
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.IsReadOnly.ShouldBeTrue();
            range.Minimum.ShouldBe(0);
            range.Maximum.ShouldBe(100);
            range.Value.ShouldBe(62);
            var value = peer.GetProvider<IValueProvider>()!;
            value.IsReadOnly.ShouldBeTrue();
            value.Value.ShouldBe("62% full");

            // The data, not the spring: screen readers hear the new value at once.
            var changes = new List<AutomationPropertyChangedEventArgs>();
            peer.PropertyChanged += (_, e) => changes.Add(e);
            CoreFrames.Run(clock, 2);
            meter.Used = 1860;
            meter.DisplayedPercent.ShouldBe(62);
            range.Value.ShouldBe(93);
            value.Value.ShouldBe("93% full");
            changes.Count.ShouldBe(2);
            changes[0].Property.ShouldBe(RangeValuePatternIdentifiers.ValueProperty);
            changes[0].NewValue.ShouldBe(93.0);
            changes[1].Property.ShouldBe(ValuePatternIdentifiers.ValueProperty);
            changes[1].NewValue.ShouldBe("93% full");
            // Frames of the spring raise nothing more.
            CoreFrames.Run(clock, 90);
            changes.Count.ShouldBe(2);

            meter.AccessibleName = "Speicher belegt";
            AutomationProperties.GetName(meter).ShouldBe("Speicher belegt");
            meter.ValueText = percent => $"{percent} % belegt";
            value.Value.ShouldBe("93 % belegt");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
