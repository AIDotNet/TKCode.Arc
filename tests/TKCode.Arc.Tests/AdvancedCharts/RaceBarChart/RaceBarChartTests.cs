using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Xunit;

namespace TKCode.Arc.Tests.AdvancedCharts;

public class RaceBarChartTests
{
    private static RaceBarFrame Frame(string period, params (string Id, double Value)[] values)
        => new(period, values.Select(v => new KeyValuePair<string, double>(v.Id, v.Value)));

    private static TKCode.Arc.RaceBarChart Sample(int barCount = 8) => new()
    {
        Label = "Test race",
        Width = 640,
        BarCount = barCount,
        IsAutoPlay = false,
        Items = [new RaceBarItem("a", "Alder"), new RaceBarItem("b", "Birch"), new RaceBarItem("c", "Cedar"), new RaceBarItem("d", "Dogwood")],
        Frames =
        [
            Frame("P1", ("a", 40), ("b", 30), ("c", 20), ("d", 10)),
            Frame("P2", ("a", 50), ("b", 60), ("c", 25), ("d", 15)),
            Frame("P3", ("a", 55), ("b", 70), ("c", 90), ("d", 20)),
            Frame("P4", ("a", 60), ("b", 75), ("c", 100), ("d", 200))
        ]
    };

    private static Window Show(TKCode.Arc.RaceBarChart chart, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = chart, Width = 700, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] Ranking(TKCode.Arc.RaceBarChart chart) => chart.Engine.Order.Select(i => chart.Engine.Items[i].Id).ToArray();

    private static void Press(Window window, TKCode.Arc.RaceBarChart chart, PhysicalKey key)
    {
        chart.Stage!.Focus();
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Ranks_By_Value_At_The_Current_Period()
    {
        var chart = Sample();
        Show(chart);
        Ranking(chart).ShouldBe(["a", "b", "c", "d"]);
        chart.FrameIndex = 1;
        Ranking(chart).ShouldBe(["b", "a", "c", "d"]);
        chart.FrameIndex = 3;
        Ranking(chart).ShouldBe(["d", "c", "b", "a"]);
    }

    [AvaloniaFact]
    public void Ties_Keep_The_Order_Of_Items_Whichever_Way_The_Timeline_Moves()
    {
        var chart = new TKCode.Arc.RaceBarChart
        {
            Width = 640,
            IsAutoPlay = false,
            Items = [new RaceBarItem("x", "X"), new RaceBarItem("y", "Y"), new RaceBarItem("z", "Z")],
            Frames = [Frame("1", ("x", 5), ("y", 5), ("z", 5)), Frame("2", ("x", 1), ("y", 9), ("z", 5)), Frame("3", ("x", 7), ("y", 7), ("z", 7))]
        };
        Show(chart);
        Ranking(chart).ShouldBe(["x", "y", "z"]);
        chart.FrameIndex = 1;
        Ranking(chart).ShouldBe(["y", "z", "x"]);
        chart.FrameIndex = 2;
        Ranking(chart).ShouldBe(["x", "y", "z"]);
        chart.FrameIndex = 0;
        Ranking(chart).ShouldBe(["x", "y", "z"]);
    }

    [AvaloniaFact]
    public void A_Missing_Id_Counts_As_Zero_And_Unknown_Ids_And_Bad_Numbers_Are_Ignored()
    {
        var chart = new TKCode.Arc.RaceBarChart
        {
            Width = 640,
            IsAutoPlay = false,
            Items = [new RaceBarItem("a", "A"), new RaceBarItem("b", "B")],
            Frames = [Frame("1", ("a", 4), ("ghost", 99)), Frame("2", ("a", double.NaN), ("b", 3))]
        };
        Show(chart);
        chart.Engine.ValueAt(1, 0).ShouldBe(0);
        Ranking(chart).ShouldBe(["a", "b"]);
        chart.Engine.Domain.ShouldBe(4);
        chart.FrameIndex = 1;
        chart.Engine.ValueAt(0, 1).ShouldBe(0);
        Ranking(chart).ShouldBe(["b", "a"]);
    }

    [AvaloniaFact]
    public void Bar_Count_Cuts_Off_The_Rows_Below_It()
    {
        var chart = Sample(barCount: 2);
        Show(chart);
        chart.ShownBars.ShouldBe(2);
        var engine = chart.Engine;
        engine.Presence.ShouldBe([1, 1, 0, 0]);
        engine.Row.ShouldBe([0, 1, 2, 2]);
        chart.FrameIndex = 3;
        // d and c take the two places; a and b wait below.
        engine.Presence.ShouldBe([0, 0, 1, 1]);
        engine.Row[3].ShouldBe(0);
        chart.BarCount = 0;
        chart.BarCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Values_Interpolate_Between_Periods()
    {
        var chart = Sample();
        Show(chart);
        var engine = chart.Engine;
        engine.ValueAt(0, 0.5).ShouldBe(45, 1e-9);
        engine.ValueAt(1, 1.25).ShouldBe(62.5, 1e-9);
        engine.ValueAt(3, 3).ShouldBe(200);
        engine.ValueAt(3, 99).ShouldBe(200);
    }

    [AvaloniaFact]
    public void Full_Motion_Playback_Interpolates_And_Overtakes_On_Springs()
    {
        var chart = Sample();
        Show(chart, ArcMotionLevel.Full);
        chart.FrameDuration = TimeSpan.FromSeconds(1);
        chart.IsPlaying = true;
        // Just past halfway: a = 45.x and b = 45.x + more, so b has just overtaken a.
        for (var i = 0; i < 33; i++)
        {
            chart.Tick(1 / 60.0);
        }
        chart.Engine.Position.ShouldBe(33 / 60.0, 1e-6);
        chart.Engine.Shown[0].ShouldBe(40 + 10 * 33 / 60.0, 1e-6);
        chart.Engine.Rank[1].ShouldBe(0);
        chart.Engine.Row[1].ShouldBeGreaterThan(0.0, "the bar is still sliding towards the top");
        chart.Engine.RowVelocity[1].ShouldBeLessThan(0.0, "and it is moving up");
        chart.FrameIndex.ShouldBe(0);
        for (var i = 0; i < 40 && chart.FrameIndex == 0; i++)
        {
            chart.Tick(1 / 60.0);
        }
        chart.FrameIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Retarget_Keeps_A_Rows_Velocity()
    {
        var chart = Sample();
        Show(chart, ArcMotionLevel.Full);
        chart.FrameIndex = 2;
        for (var i = 0; i < 40; i++)
        {
            chart.Tick(1 / 60.0);
        }
        var velocity = chart.Engine.RowVelocity[2];
        velocity.ShouldBeLessThan(0.0);
        chart.FrameIndex = 3;
        chart.Tick(1 / 240.0);
        Math.Sign(chart.Engine.RowVelocity[2]).ShouldBe(Math.Sign(velocity), "a new target must not restart the row from rest");
        chart.Engine.IsSettled.ShouldBeFalse();
        for (var i = 0; i < 400; i++)
        {
            chart.Tick(1 / 60.0);
        }
        chart.Engine.IsSettled.ShouldBeTrue();
        chart.Engine.Position.ShouldBe(3);
        chart.Engine.Row[3].ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Steps_Without_Interpolation_And_Places_At_Once()
    {
        var chart = Sample();
        Show(chart, ArcMotionLevel.Reduced);
        chart.FrameDuration = TimeSpan.FromSeconds(1);
        chart.IsPlaying = true;
        chart.Tick(0.5);
        chart.FrameIndex.ShouldBe(0);
        chart.Engine.Position.ShouldBe(0);
        chart.Tick(0.6);
        chart.FrameIndex.ShouldBe(1);
        chart.Engine.Position.ShouldBe(1);
        chart.Engine.Row[1].ShouldBe(0, "placed immediately");
        chart.Engine.RowVelocity[1].ShouldBe(0);
    }

    [AvaloniaFact]
    public void Auto_Play_Is_Ignored_Without_Full_Motion()
    {
        var chart = Sample();
        chart.IsAutoPlay = true;
        Show(chart, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        chart.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Auto_Play_Starts_On_First_View_With_Full_Motion_Unless_Turned_Off()
    {
        var chart = Sample();
        chart.IsAutoPlay = true;
        Show(chart, ArcMotionLevel.Full);
        TestHelpers.PumpUntil(() => chart.IsPlaying, 2000).ShouldBeTrue();

        var off = Sample();
        Show(off, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        off.IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Playback_Stops_On_The_Last_Period_And_Play_Replays_From_The_Start()
    {
        var chart = Sample();
        Show(chart);
        chart.FrameDuration = TimeSpan.FromSeconds(1);
        chart.FrameIndex = 2;
        chart.IsPlaying = true;
        chart.Tick(1);
        chart.FrameIndex.ShouldBe(3);
        chart.IsPlaying.ShouldBeFalse();

        chart.IsPlaying = true;
        chart.FrameIndex.ShouldBe(0);
        chart.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Looping_Wraps_To_The_First_Period()
    {
        var chart = Sample();
        chart.IsLooping = true;
        Show(chart);
        chart.FrameDuration = TimeSpan.FromSeconds(1);
        chart.FrameIndex = 2;
        chart.IsPlaying = true;
        chart.Tick(1);
        chart.FrameIndex.ShouldBe(3);
        chart.IsPlaying.ShouldBeTrue();
        chart.Tick(1);
        chart.FrameIndex.ShouldBe(0);
        chart.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Frame_Index_Is_Clamped_To_The_Frames()
    {
        var chart = Sample();
        Show(chart);
        chart.FrameIndex = 99;
        chart.FrameIndex.ShouldBe(3);
        chart.FrameIndex = -4;
        chart.FrameIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Keyboard_Plays_Pauses_And_Steps()
    {
        var chart = Sample();
        var window = Show(chart);
        Press(window, chart, PhysicalKey.Space);
        chart.IsPlaying.ShouldBeTrue();
        Press(window, chart, PhysicalKey.Enter);
        chart.IsPlaying.ShouldBeFalse();
        Press(window, chart, PhysicalKey.ArrowRight);
        chart.FrameIndex.ShouldBe(1);
        Press(window, chart, PhysicalKey.ArrowLeft);
        chart.FrameIndex.ShouldBe(0);
        Press(window, chart, PhysicalKey.ArrowLeft);
        chart.FrameIndex.ShouldBe(0);
        Press(window, chart, PhysicalKey.End);
        chart.FrameIndex.ShouldBe(3);
        Press(window, chart, PhysicalKey.ArrowRight);
        chart.FrameIndex.ShouldBe(3);
        Press(window, chart, PhysicalKey.Space);
        chart.FrameIndex.ShouldBe(0, "Space on the last period replays");
        chart.IsPlaying.ShouldBeTrue();
        Press(window, chart, PhysicalKey.ArrowRight);
        chart.IsPlaying.ShouldBeFalse("stepping pauses");
        chart.FrameIndex.ShouldBe(1);
        Press(window, chart, PhysicalKey.Home);
        chart.FrameIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Up_And_Down_Read_The_Bars_By_Rank_And_Escape_Clears()
    {
        var chart = Sample(barCount: 3);
        var window = Show(chart);
        chart.ReadingId.ShouldBeNull();
        Press(window, chart, PhysicalKey.ArrowDown);
        chart.ReadingId.ShouldBe("a");
        chart.ReadingText.ShouldBe("Alder, rank 1 of 4, 40");
        Press(window, chart, PhysicalKey.ArrowDown);
        chart.ReadingId.ShouldBe("b");
        chart.LiveText.ShouldBe("Birch, rank 2 of 4, 30");
        chart.Engine.Dim[0].ShouldBeLessThan(1);
        chart.Engine.Dim[1].ShouldBe(1);
        Press(window, chart, PhysicalKey.ArrowDown);
        Press(window, chart, PhysicalKey.ArrowDown);
        chart.ReadingId.ShouldBe("c", "the reading stays inside the bars on screen");
        Press(window, chart, PhysicalKey.ArrowUp);
        chart.ReadingId.ShouldBe("b");
        Press(window, chart, PhysicalKey.Escape);
        chart.ReadingId.ShouldBeNull();
        chart.Engine.Dim[0].ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Reading_That_Falls_Below_The_Top_Set_Is_Cleared()
    {
        var chart = Sample(barCount: 2);
        var window = Show(chart, ArcMotionLevel.Full);
        Press(window, chart, PhysicalKey.ArrowDown);
        chart.ReadingId.ShouldBe("a");
        chart.FrameIndex = 3;
        for (var i = 0; i < 200; i++)
        {
            chart.Tick(1 / 60.0);
        }
        chart.ReadingId.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Events_Report_The_Frame_And_The_Playing_State()
    {
        var chart = Sample();
        var frames = new List<RaceBarFrameChangedEventArgs>();
        var playing = new List<RaceBarPlayingChangedEventArgs>();
        chart.FrameChanged += (_, e) => frames.Add(e);
        chart.PlayingChanged += (_, e) => playing.Add(e);
        Show(chart);
        chart.FrameIndex = 1;
        frames.Count.ShouldBe(1);
        frames[0].PreviousIndex.ShouldBe(0);
        frames[0].Index.ShouldBe(1);
        frames[0].Period.ShouldBe("P2");
        frames[0].Leader!.Id.ShouldBe("b");
        chart.Play();
        playing.Single().IsPlaying.ShouldBeTrue();
        playing.Single().Index.ShouldBe(1);
        chart.Pause();
        playing.Count.ShouldBe(2);
        playing[1].IsPlaying.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Announces_The_Period_And_Leader_And_Reports_The_Slider_Value()
    {
        var chart = Sample();
        var window = Show(chart);
        chart.LiveText.ShouldBe("P1. Leader: Alder, 40");
        Press(window, chart, PhysicalKey.ArrowRight);
        chart.LiveText.ShouldBe("P2. Leader: Birch, 60");
        chart.Timeline!.ValueText.ShouldBe("P2, period 2 of 4");
        var peer = ControlAutomationPeer.CreatePeerForElement(chart.Timeline);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
        var range = peer.GetProvider<IRangeValueProvider>().ShouldNotBeNull();
        range.Maximum.ShouldBe(3);
        range.Value.ShouldBe(1);
        range.SetValue(2);
        chart.FrameIndex.ShouldBe(2);
        var group = ControlAutomationPeer.CreatePeerForElement(chart.Stage!);
        group.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        group.GetLocalizedControlType().ShouldBe("race bar chart");
        group.GetHelpText().ShouldBe("P3. Leader: Cedar, 90");
    }

    [AvaloniaFact]
    public void Groups_Give_Hues_And_A_Legend()
    {
        var chart = new TKCode.Arc.RaceBarChart
        {
            Width = 640,
            IsAutoPlay = false,
            Groups = [new RaceBarGroup("g1", "One"), new RaceBarGroup("g2", "Two"), new RaceBarGroup("g3", "Three"), new RaceBarGroup("g4", "Four"), new RaceBarGroup("g5", "Five")],
            Items =
            [
                new RaceBarItem("a", "A", "g1"), new RaceBarItem("b", "B", "g2"), new RaceBarItem("c", "C", "g4"),
                new RaceBarItem("d", "D", "g5"), new RaceBarItem("e", "E")
            ],
            Frames = [Frame("1", ("a", 1), ("b", 2), ("c", 3), ("d", 4), ("e", 5))]
        };
        Show(chart);
        chart.Engine.Hue.ShouldBe([0, 1, 3, -1, -1]);
        chart.Stage!.LegendCount.ShouldBe(5);
        var palette = chart.Palette;
        chart.Stage.ColorOf(0).ShouldBe(palette.Series(0));
        chart.Stage.ColorOf(2).ShouldBe(palette.Series(3));
        chart.Stage.ColorOf(3).ShouldNotBe(palette.Series(0));
    }

    [AvaloniaFact]
    public void Without_Groups_Every_Bar_Uses_The_First_Hue_And_There_Is_No_Legend()
    {
        var chart = Sample();
        Show(chart);
        chart.Engine.Hue.ShouldBe([0, 0, 0, 0]);
        chart.Stage!.LegendCount.ShouldBe(0);
        chart.Stage.ColorOf(3).ShouldBe(chart.Palette.Series(0));
    }

    [AvaloniaFact]
    public void Formatters_Are_Used_And_The_Default_Is_Compact()
    {
        var chart = Sample();
        Show(chart);
        chart.FormatValue(12300).ShouldBe(ArcFormatting.Compact(12300));
        chart.FormatTick(2000).ShouldBe(ArcFormatting.Compact(2000));
        chart.ValueFormatter = value => $"{value} units";
        chart.FormatValue(5).ShouldBe("5 units");
        chart.FormatTick(5).ShouldBe("5 units");
        chart.TickFormatter = value => $"#{value}";
        chart.FormatTick(5).ShouldBe("#5");
        chart.FormatValue(5).ShouldBe("5 units");
        chart.LiveText.ShouldBe("P1. Leader: Alder, 40 units");
    }

    [Fact]
    public void Displayed_Digits_Decide_When_A_Value_Is_Formatted_Again()
    {
        RaceBarStage.KeyOf(12340, false, 3).ShouldBe(RaceBarStage.KeyOf(12341, false, 3));
        RaceBarStage.KeyOf(12340, false, 3).ShouldNotBe(RaceBarStage.KeyOf(12440, false, 3));
        RaceBarStage.KeyOf(12340, true, 3).ShouldNotBe(RaceBarStage.KeyOf(12341, true, 3));
        RaceBarStage.KeyOf(0, false, 3).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Empty_Frames_Or_Items_Leave_The_Chart_Inert()
    {
        var chart = new TKCode.Arc.RaceBarChart { Width = 640, IsAutoPlay = false };
        var window = Show(chart);
        chart.Engine.FrameCount.ShouldBe(0);
        chart.PlayButton!.IsEnabled.ShouldBeFalse();
        chart.Timeline!.IsEnabled.ShouldBeFalse();
        chart.Stage!.Focusable.ShouldBeFalse();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        chart.IsPlaying.ShouldBeFalse();
        chart.Tick(1).ShouldBeFalse();
        chart.LiveText.ShouldBe("No data yet");
        chart.FrameIndex = 2;
        chart.FrameIndex.ShouldBe(2, "nothing to clamp against until frames arrive");

        chart.Items = [new RaceBarItem("a", "A")];
        chart.Frames = [Frame("1", ("a", 1)), Frame("2", ("a", 2))];
        Dispatcher.UIThread.RunJobs();
        chart.FrameIndex.ShouldBe(1);
        chart.PlayButton.IsEnabled.ShouldBeTrue();
        chart.Stage.Focusable.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Single_Frame_Cannot_Play()
    {
        var chart = new TKCode.Arc.RaceBarChart
        {
            Width = 640,
            IsAutoPlay = false,
            Items = [new RaceBarItem("a", "A")],
            Frames = [Frame("1", ("a", 1))]
        };
        Show(chart);
        chart.IsPlaying = true;
        chart.IsPlaying.ShouldBeFalse();
        chart.PlayButton!.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Observable_Lists_Are_Followed_And_Inline_Xaml_Works()
    {
        var chart = (TKCode.Arc.RaceBarChart)AvaloniaRuntimeXamlLoader.Parse("""
            <arc:RaceBarChart xmlns="https://github.com/avaloniaui"
                              xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                              Width="640" IsAutoPlay="False" FrameIndex="1">
                <arc:RaceBarChart.Items>
                    <arc:RaceBarItem Id="a" Label="Alder" />
                    <arc:RaceBarItem Id="b" Label="Birch" />
                </arc:RaceBarChart.Items>
                <arc:RaceBarChart.Frames>
                    <arc:RaceBarFrame Period="One">
                        <arc:RaceBarValue Id="a" Value="1" />
                    </arc:RaceBarFrame>
                    <arc:RaceBarFrame Period="Two">
                        <arc:RaceBarValue Id="a" Value="1" />
                        <arc:RaceBarValue Id="b" Value="5" />
                    </arc:RaceBarFrame>
                </arc:RaceBarChart.Frames>
            </arc:RaceBarChart>
            """);
        Show(chart);
        chart.Engine.ItemCount.ShouldBe(2);
        chart.FrameIndex.ShouldBe(1);
        Ranking(chart).ShouldBe(["b", "a"]);
        chart.Items!.Add(new RaceBarItem("c", "Cedar"));
        chart.Engine.ItemCount.ShouldBe(3);
        chart.Frames!.Add(Frame("Three", ("c", 9)));
        chart.Engine.FrameCount.ShouldBe(3);
        chart.FrameIndex = 2;
        Ranking(chart)[0].ShouldBe("c");
    }

    [AvaloniaFact]
    public void Narrow_Charts_Put_Names_Inside_Long_Bars_And_Past_Short_Ones()
    {
        var chart = Sample();
        chart.Width = 320;
        var window = Show(chart);
        chart.Stage!.IsNarrow.ShouldBeTrue();
        chart.Classes.Contains(":narrow").ShouldBeTrue();
        // Rendering decides where each name goes: the leader's bar holds its name, the shortest cannot.
        window.CaptureRenderedFrame();
        var engine = chart.Engine;
        chart.Stage.NameIsInside(engine.Order[0]).ShouldBeTrue();
        chart.Stage.NameIsInside(engine.Order[3]).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Ticks_Adapt_To_The_Width()
    {
        var wide = Sample();
        wide.Width = 680;
        Show(wide);
        var narrow = Sample();
        narrow.Width = 240;
        Show(narrow);
        wide.Engine.TickValues().Count.ShouldBeGreaterThan(narrow.Engine.TickValues().Count);
        narrow.Engine.TickValues().Count.ShouldBeGreaterThanOrEqualTo(2);
        // Zero plus at most the number of intervals the width asked for (2 to 8).
        wide.Engine.TickValues().Count.ShouldBeLessThanOrEqualTo(9);
    }

    [AvaloniaFact]
    public void Scrubbing_Pauses_And_Lands_On_The_Nearest_Period()
    {
        var chart = Sample();
        var window = Show(chart);
        chart.IsPlaying = true;
        chart.Timeline!.Bounds.Height.ShouldBe(36);
        var origin = chart.Timeline.TranslatePoint(new Point(0, 18), window)!.Value;
        var end = origin + new Point(chart.Timeline.Bounds.Width * 0.72, 0);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        chart.IsPlaying.ShouldBeFalse();
        chart.FrameIndex.ShouldBe(2);
        window.MouseMove(origin, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        chart.FrameIndex.ShouldBe(0);
        window.MouseUp(origin, MouseButton.Left);
    }

    [AvaloniaFact]
    public void The_Play_Button_Toggles_And_Names_Its_Action()
    {
        var chart = Sample();
        Show(chart);
        var play = chart.PlayButton!;
        AutomationProperties.GetName(play).ShouldBe("Play");
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(play)).Invoke();
        chart.IsPlaying.ShouldBeTrue();
        AutomationProperties.GetName(play).ShouldBe("Pause");
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(play)).Invoke();
        chart.IsPlaying.ShouldBeFalse();
        chart.FrameIndex = 3;
        AutomationProperties.GetName(play).ShouldBe("Replay from the start");
    }

    [AvaloniaFact]
    public void Renders_In_Both_Themes_Without_Throwing()
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var chart = Sample();
            chart.Groups = [new RaceBarGroup("g", "Group")];
            chart.Items![0].Group = "g";
            chart.HighlightId = "b";
            var window = Show(chart);
            window.RequestedThemeVariant = theme;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            Press(window, chart, PhysicalKey.ArrowDown);
            window.CaptureRenderedFrame();
            window.Close();
        }
    }

    [Fact]
    public void The_labels_of_a_bar_being_passed_fade_and_return()
    {
        double[] presence = [1, 1];
        int[] order = [0, 1];
        Assert.Equal(1, RaceBarStage.LabelOpacity([0, 1], presence, order, 1), 3);
        Assert.Equal(0, RaceBarStage.LabelOpacity([0.5, 0.5], presence, order, 1), 3);
        Assert.Equal(0.5, RaceBarStage.LabelOpacity([0, RaceBarStage.LabelClearRows / 2], presence, order, 1), 3);
        Assert.Equal(1, RaceBarStage.LabelOpacity([0.5, 0.5], presence, order, 0), 3);
        Assert.Equal(1, RaceBarStage.LabelOpacity([0.5, 0.5], [0, 1], order, 1), 3);
    }
}
