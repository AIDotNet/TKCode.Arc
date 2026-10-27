using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.LineCharts;

/// <summary>
/// <see cref="LineReplay"/> on a synthetic day (a value rising by 10 every five minutes from 1,000, a dip to 960 for 20
/// minutes after 5 am, a flat baseline of 500, and three moments): the prepared data, the default positions, playback
/// and its events, the button states, the readout, the scrubber's keys and reports, jumps to moments, the axis labels
/// that give way to the clock, the comparison being optional and the responsive layouts.
/// </summary>
public class LineReplayTests
{
    private static LineReplayPoint[] Points(bool baseline = true)
    {
        var points = new LineReplayPoint[288];
        for (var i = 0; i < points.Length; i++)
        {
            var m = i * 5.0;
            var value = m is > 300 and <= 320 ? 960 : 1000 + 10 * i;
            points[i] = new LineReplayPoint(m, value, baseline ? 500 : null);
        }
        return points;
    }

    private static LineReplayMoment[] Moments() =>
    [
        new("launch", 60, "Doors open", "Everywhere at once", LineReplayMomentKind.Launch),
        new("dip", 300, "Checkout slowed", null, LineReplayMomentKind.Incident),
        new("goal", 600, "Record hour", "Best on record", LineReplayMomentKind.Milestone),
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 1200, ArcMotionLevel level = ArcMotionLevel.Off, Action<LineReplay>? configure = null)
        {
            Replay = new LineReplay { Title = "Test day", SeriesLabel = "Today", BaselineLabel = "Typical day", ValueLabel = "On site" };
            Replay.Points = Points();
            Replay.Moments.AddRange(Moments());
            configure?.Invoke(Replay);
            Replay.MomentReached += (_, e) => Reached.Add(e.Moment.Id!);
            Host = new Border { Width = width, Child = Replay, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = width + 40;
            Window.Height = 1400;
            Pump();
        }

        public LineReplay Replay { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<string> Reached { get; } = new();

        public Button PlayButton => TestHelpers.Descendant<Button>(Replay, LineReplay.PlayButtonPart);

        public TextBlock PlayText => TestHelpers.Descendant<TextBlock>(Replay, LineReplay.PlayTextPart);

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    // Data -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Data_Places_Moments_And_Measures_Their_Effects()
    {
        var data = new LineReplayData(Points(), Moments());
        data.Start.ShouldBe(0);
        data.End.ShouldBe(1435);
        data.MinStep.ShouldBe(5, 1e-9);
        data.AxisTop.ShouldBe(4000);
        data.HasBaseline.ShouldBeTrue();
        data.Moments[0].Effect.ShouldBe(120, 1e-9);
        data.Moments[1].Effect.ShouldBe(-0.4, 1e-9);
        data.Moments[2].Effect.ShouldBe(120, 1e-9);
    }

    [Fact]
    public void Data_Interpolates_Between_Points_And_Tracks_The_Running_Peak()
    {
        var data = new LineReplayData(Points(), Moments());
        data.ValueAt(2.5).ShouldBe(1005, 1e-9);
        data.PeakAt(310).ShouldBe((1600, 300));
        data.PeakAt(600).ShouldBe((2200, 600));
    }

    [Fact]
    public void Clock_And_Hour_Formats_Read_Like_A_Wall_Clock()
    {
        LineChartsFormat.Clock(580).ShouldBe("9:40 am");
        LineChartsFormat.Clock(720).ShouldBe("12:00 pm");
        LineChartsFormat.Clock(1435).ShouldBe("11:55 pm");
        LineChartsFormat.Hour(0).ShouldBe("12 am");
        LineChartsFormat.Hour(360).ShouldBe("6 am");
        LineChartsFormat.Count(2316).ShouldBe("2,316");
    }

    // Positions and the button ------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Rests_At_The_End_With_Every_Moment_By_Default()
    {
        using var rig = new Rig();
        rig.Replay.Position.ShouldBe(1435);
        rig.Replay.Stage.IsMarkerShown(2).ShouldBeTrue();
        rig.Replay.Stage.CalloutIndex.ShouldBe(2);
        rig.PlayText.Text.ShouldBe("Replay");
        AutomationProperties.GetName(rig.PlayButton).ShouldBe("Replay from the start");
        rig.Replay.MomentButtons.ShouldAllBe(b => b.IsReached && b.IsEffectShown);
        rig.Replay.MomentButtons[2].IsCurrent.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Can_Rest_At_The_Start()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Position.ShouldBe(0);
        rig.Replay.Stage.IsMarkerShown(0).ShouldBeFalse();
        rig.Replay.Stage.CalloutIndex.ShouldBe(-1);
        rig.PlayText.Text.ShouldBe("Play");
        AutomationProperties.GetName(rig.PlayButton).ShouldBe("Play replay");
    }

    [AvaloniaFact]
    public void Readout_Shows_Time_Value_Ratio_And_Peak_So_Far()
    {
        using var rig = new Rig();
        rig.Replay.Seek(600, animate: false);
        rig.Pump();
        rig.Replay.ReadoutTexts.ShouldBe(("10:00 am", "2,200", "4.4×", "2,200 at 10:00 am"));
    }

    [AvaloniaFact]
    public void Moment_Rows_Show_Effects_Only_Once_Their_Window_Has_Played()
    {
        using var rig = new Rig();
        rig.Replay.MomentButtons[0].EffectText.ShouldBe("+120 in the next hour");
        rig.Replay.MomentButtons[1].EffectText.ShouldBe("−40% in 20 minutes");
        rig.Replay.Seek(330, animate: false);
        rig.Pump();
        rig.Replay.MomentButtons[1].IsReached.ShouldBeTrue();
        rig.Replay.MomentButtons[1].IsCurrent.ShouldBeTrue();
        rig.Replay.MomentButtons[1].IsEffectShown.ShouldBeTrue();
        rig.Replay.Seek(310, animate: false);
        rig.Pump();
        rig.Replay.MomentButtons[1].IsEffectShown.ShouldBeFalse();
        AutomationProperties.GetName(rig.Replay.MomentButtons[1]).ShouldBe("Checkout slowed, 5:00 am");
    }

    // Playback ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Playing_Advances_At_The_Duration_Pace_And_Reports_Passed_Moments()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Play();
        rig.Replay.IsPlaying.ShouldBeTrue();
        rig.PlayText.Text.ShouldBe("Pause");
        rig.Replay.Advance(1);
        rig.Replay.Position.ShouldBe(1435 / 16.0, 1e-6);
        rig.Reached.ShouldBe(["launch"]);
        rig.Replay.LiveRegion!.LastAnnouncement.ShouldBe("1:00 am. Doors open.");
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Pace()
    {
        using var rig = new Rig(configure: r =>
        {
            r.DefaultPosition = LineReplayPosition.Start;
            r.Speed = 2;
        });
        rig.Replay.Play();
        rig.Replay.Advance(1);
        rig.Replay.Position.ShouldBe(1435 / 8.0, 1e-6);
    }

    [AvaloniaFact]
    public void Finishing_Stops_Announces_The_Peak_And_Offers_A_Replay()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Play();
        rig.Replay.Advance(30).ShouldBeFalse();
        rig.Replay.IsPlaying.ShouldBeFalse();
        rig.Replay.Position.ShouldBe(1435);
        rig.Reached.ShouldBe(["launch", "dip", "goal"]);
        rig.Replay.LiveRegion!.LastAnnouncement.ShouldBe("Replay finished. Peak 3,870 at 11:55 pm.");
        rig.PlayText.Text.ShouldBe("Replay");
    }

    [AvaloniaFact]
    public void Pressing_Replay_At_The_End_Rewinds_And_Plays()
    {
        using var rig = new Rig();
        rig.PlayButton.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Replay.IsPlaying.ShouldBeTrue();
        // Rewound to the start; the pumped frames may have played a few minutes since (at most 0.1 s of wall time each).
        rig.Replay.Position.ShouldBeLessThan(60);
    }

    [AvaloniaFact]
    public void Pausing_Announces_And_Updates_The_Scrubber_Value_Text()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Play();
        rig.Replay.Advance(1);
        // While it plays the slider keeps its last reported value, so screen readers are not flooded.
        rig.Replay.Scrubber.ReportedPosition.ShouldBe(0);
        rig.Replay.Pause();
        rig.Replay.Scrubber.ReportedPosition.ShouldBe(1435 / 16.0, 1e-6);
        rig.Replay.Scrubber.ValueText.ShouldBe("1:29 am, 1,179 on site");
        rig.Replay.LiveRegion!.LastAnnouncement.ShouldBe("Paused at 1:29 am.");
    }

    [AvaloniaFact]
    public void Scrubbing_And_Jumping_Do_Not_Raise_MomentReached()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Seek(900, animate: false);
        rig.Replay.ScrubTo(1000);
        rig.Replay.EndScrub();
        rig.Pump();
        rig.Reached.ShouldBeEmpty();
        rig.Replay.Stage.IsMarkerShown(2).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clicking_A_Moment_Moves_The_Playhead_To_It()
    {
        using var rig = new Rig();
        rig.Replay.MomentButtons[1].Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        rig.Replay.Position.ShouldBe(300);
        rig.Replay.LiveRegion!.LastAnnouncement.ShouldBe("5:00 am. Checkout slowed.");
        rig.Reached.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Scrubber_Keys_Step_Points_Hours_And_Ends_And_Toggle_Playback()
    {
        using var rig = new Rig(configure: r => r.DefaultPosition = LineReplayPosition.Start);
        rig.Replay.Scrubber.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(PhysicalKey.ArrowRight);
        rig.Replay.Position.ShouldBe(5, 1e-9);
        rig.Key(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        rig.Replay.Position.ShouldBe(65, 1e-9);
        rig.Key(PhysicalKey.PageDown);
        rig.Replay.Position.ShouldBe(125, 1e-9);
        rig.Key(PhysicalKey.End);
        rig.Replay.Position.ShouldBe(1435);
        rig.Key(PhysicalKey.Home);
        rig.Replay.Position.ShouldBe(0);
        rig.Key(PhysicalKey.K);
        rig.Replay.IsPlaying.ShouldBeTrue();
        rig.Key(PhysicalKey.Space);
        rig.Replay.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Axis_Labels_Under_The_Clock_Give_Way()
    {
        using var rig = new Rig();
        var stage = rig.Replay.Stage;
        stage.Ticks.ShouldBe([180, 360, 540, 720, 900, 1080, 1260]);
        rig.Replay.Seek(360, animate: false);
        rig.Pump();
        stage.TickTargetOpacity(1).ShouldBe(0);
        stage.TickTargetOpacity(5).ShouldBe(1);
    }

    // Variants and layout ----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Without_A_Baseline_The_Comparison_And_Ratio_Are_Hidden()
    {
        using var rig = new Rig(configure: r => r.Points = Points(baseline: false));
        rig.Replay.Classes.Contains(LineReplay.NoBaselinePseudoClass).ShouldBeTrue();
        TestHelpers.Descendant<StackPanel>(rig.Replay, "PART_RatioItem").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Wide_Blocks_Put_The_Moments_Beside_The_Chart()
    {
        using var rig = new Rig(width: 1200);
        rig.Replay.Classes.Contains(LineReplay.StackedPseudoClass).ShouldBeFalse();
        TestHelpers.Descendant<LineReplayMomentsPanel>(rig.Replay, LineReplay.MomentsPart).Columns.ShouldBe(1);
        rig.Replay.Stage.Bounds.Height.ShouldBe(380);
    }

    [AvaloniaFact]
    public void Medium_Blocks_Stack_The_Moments_In_Two_Columns()
    {
        using var rig = new Rig(width: 800);
        rig.Replay.Classes.Contains(LineReplay.StackedPseudoClass).ShouldBeTrue();
        rig.Replay.Classes.Contains(LineReplay.NarrowPseudoClass).ShouldBeFalse();
        TestHelpers.Descendant<LineReplayMomentsPanel>(rig.Replay, LineReplay.MomentsPart).Columns.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Narrow_Blocks_Use_One_Column_And_A_Shorter_Chart()
    {
        using var rig = new Rig(width: 420);
        rig.Replay.Classes.Contains(LineReplay.NarrowPseudoClass).ShouldBeTrue();
        TestHelpers.Descendant<LineReplayMomentsPanel>(rig.Replay, LineReplay.MomentsPart).Columns.ShouldBe(1);
        rig.Replay.Stage.Bounds.Height.ShouldBe(300);
        rig.Replay.Stage.Ticks.Count.ShouldBe(3);
    }
}
