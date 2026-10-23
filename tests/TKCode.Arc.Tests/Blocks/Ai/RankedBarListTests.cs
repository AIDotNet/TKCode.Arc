using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="RankedBarList"/>: rows in order with their values, bar widths and series tones, row geometry, the limit, the
/// replay timeline on a manual clock (hold, reorder on the Settle spring, bars on their own spring, fixed rank column),
/// rows that leave and enter, reduced and off motion, a change during a replay, and what screen readers get.
/// </summary>
public class RankedBarListTests
{
    private static RankedBarList List(RankedBarItem[]? items = null, Action<RankedBarList>? configure = null)
    {
        var list = new RankedBarList
        {
            Width = 266,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Items = items ?? RankedBarSamples.Standings(),
            RankFormat = "Rank {0},",
            WinRateSuffix = ", win rate"
        };
        configure?.Invoke(list);
        return list;
    }

    private static string[] Order(RankedBarList list) => list.ShownRows.Select(r => r.Item.Id).ToArray();

    private static RankedBarRow Row(RankedBarList list, string id) => list.AllRows.First(r => r.Item.Id == id);

    private static double Y(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y;

    private static void Run(ManualFrameClock clock, int milliseconds)
    {
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Rows_Show_The_Items_In_Order_With_Their_Values_And_Tones()
    {
        var list = List();
        var window = TestHelpers.Show(list);
        try
        {
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            list.ShownRows.Select(r => r.Slot).ShouldBe([0, 1, 2, 3, 4]);
            list.ShownRows.Select(r => r.Percent).ShouldBe([70, 50, 50, 33, 17]);
            list.ShownRows.Select(r => r.Digits!.DisplayText).ShouldBe(["70%", "50%", "50%", "33%", "17%"]);
            list.ShownRows[0].FillRatio.ShouldBe(0.7, 1e-9);
            list.ShownRows[0].Bar!.Ratio.ShouldBe(0.7, 1e-9);
            list.ShownRows[4].Bar!.Ratio.ShouldBe(0.5 / 3, 1e-9);
            // Tones: Lumen 3, Kite 4, Basalt 1, Vireo 2.
            TestHelpers.ColorOf(list.ShownRows[0].Bar!.FillBrush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Series3));
            TestHelpers.ColorOf(list.ShownRows[1].Bar!.FillBrush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Series4));
            TestHelpers.ColorOf(list.ShownRows[2].Bar!.FillBrush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Series1));
            TestHelpers.ColorOf(list.ShownRows[3].Bar!.FillBrush).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Series2));
            list.ShownRows[0].ItemName.ShouldBe("Lumen 3 Pro");
            TestHelpers.Descendant<ModelMark>(list.ShownRows[0], RankedBarRow.MarkPart).Mark.ShouldBeSameAs(RankedBarSamples.SunMark);
            TestHelpers.Descendant<ModelMark>(list.ShownRows[0], RankedBarRow.MarkPart).MarkColor.ShouldBeSameAs(RankedBarSamples.Amber);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Are_36_High_With_A_14px_Rank_Column_And_A_56px_Bar()
    {
        var list = List();
        var window = TestHelpers.Show(list);
        try
        {
            list.Bounds.Height.ShouldBe(5 * 36);
            list.Ranks!.Count.ShouldBe(5);
            list.Ranks.Bounds.Width.ShouldBe(14);
            list.Ranks.Bounds.Height.ShouldBe(5 * 36);
            var rows = list.RowsPanel!;
            rows.TranslatePoint(default, list)!.Value.X.ShouldBe(14 + 8);
            var second = list.ShownRows[1];
            second.Bounds.ShouldBe(new Rect(0, 36, 266 - 22, 36));
            var mark = TestHelpers.Descendant<ModelMark>(second, RankedBarRow.MarkPart);
            var name = TestHelpers.Descendant<TextBlock>(second, RankedBarRow.NamePart);
            var bar = second.Bar!;
            var digits = second.Digits!;
            mark.Bounds.Size.ShouldBe(new Size(16, 16));
            mark.TranslatePoint(default, second)!.Value.X.ShouldBe(0);
            name.TranslatePoint(default, second)!.Value.X.ShouldBe(26);
            bar.Bounds.Size.ShouldBe(new Size(56, 4));
            bar.TranslatePoint(default, second)!.Value.ShouldBe(new Point(244 - 40 - 10 - 56, 16));
            (digits.TranslatePoint(default, second)!.Value.X + digits.Bounds.Width).ShouldBe(244, 0.01);
            name.FontSize.ShouldBe(14);
            name.TextTrimming.ShouldBe(Avalonia.Media.TextTrimming.CharacterEllipsis);
            TestHelpers.ColorOf(name.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(list.Ranks.Foreground).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.TextMuted));
            list.Ranks.FontSize.ShouldBe(12);
            // The track is a 9% tint of the foreground.
            var foreground = ArcTokens.GetColor(list, ArcDesignTokenKind.Foreground);
            var track = (Avalonia.Media.ISolidColorBrush)bar.Track!;
            track.Color.ShouldBe(foreground);
            track.Opacity.ShouldBe(RankedBarBar.TrackOpacity, 1e-9);
            // Rows are opaque so crossing rows hide each other.
            TestHelpers.ColorOf(second.Background).ShouldBe(ArcTokens.GetColor(list, ArcDesignTokenKind.SurfaceRaised));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Limit_Caps_The_Rows_And_Ranks()
    {
        var list = List(RankedBarSamples.Six());
        var window = TestHelpers.Show(list);
        try
        {
            list.ShownRows.Count.ShouldBe(5);
            Order(list).ShouldNotContain("marlin-xl");
            list.Limit = 3;
            Dispatcher.UIThread.RunJobs();
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra"]);
            list.Ranks!.Count.ShouldBe(3);
            list.Bounds.Height.ShouldBe(3 * 36);
            list.Limit = 0;
            Dispatcher.UIThread.RunJobs();
            list.ShownRows.Count.ShouldBe(6);
            list.RowHeight = 40;
            Dispatcher.UIThread.RunJobs();
            list.Bounds.Height.ShouldBe(6 * 40);
            list.ShownRows[2].Bounds.Y.ShouldBe(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Items_Reuses_Rows_By_Id_And_Shows_At_Once()
    {
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            var lumen = Row(list, "lumen-pro");
            list.Items = RankedBarSamples.Earlier();
            Dispatcher.UIThread.RunJobs();
            Row(list, "lumen-pro").ShouldBeSameAs(lumen);
            Order(list).ShouldBe(["basalt-ultra", "kite-5", "lumen-pro", "vireo-4", "tern-flash"]);
            lumen.Slot.ShouldBe(2);
            lumen.Offset.ShouldBe(0);
            lumen.FillRatio.ShouldBe(0.625, 1e-9);
            lumen.IsMoving.ShouldBeFalse();
            lumen.Digits!.DisplayText.ShouldBe("63%");
            list.IsReplaying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Holds_The_Old_Standings_Then_Springs_Rows_To_Their_New_Ranks()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            var ranks = list.Ranks!;
            var ranksY = Y(ranks, list);
            list.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            Order(list).ShouldBe(["basalt-ultra", "kite-5", "lumen-pro", "vireo-4", "tern-flash"]);
            Row(list, "lumen-pro").FillRatio.ShouldBe(0.625, 1e-9);
            Row(list, "lumen-pro").Digits!.DisplayText.ShouldBe("63%");
            AutomationProperties.GetName(list.ShownRows[0]).ShouldBe("Rank 1, Basalt 2 Ultra, win rate 75%");

            // Still the old order just before the hold ends.
            CoreFrames.Run(clock, 18);
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            Order(list)[0].ShouldBe("basalt-ultra");

            // The hold ends (325ms): the rows take their new slots but are drawn where they were.
            CoreFrames.Run(clock, 2);
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.Moving);
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            var lumen = Row(list, "lumen-pro");
            var basalt = Row(list, "basalt-ultra");
            lumen.Slot.ShouldBe(0);
            basalt.Slot.ShouldBe(2);
            (lumen.Slot * 36 + lumen.Offset).ShouldBeGreaterThan(36);
            (basalt.Slot * 36 + basalt.Offset).ShouldBeLessThan(36);
            Row(list, "kite-5").Offset.ShouldBe(0);
            AutomationProperties.GetName(list.ShownRows[0]).ShouldBe("Rank 1, Lumen 3 Pro, win rate 70%");
            // The rank column does not move or change.
            Y(ranks, list).ShouldBe(ranksY);
            ranks.Count.ShouldBe(5);

            // Halfway: still moving, the numbers already show the new values.
            CoreFrames.Run(clock, 6);
            lumen.Offset.ShouldBeInRange(1, 71);
            lumen.FillRatio.ShouldBeInRange(0.625, 0.7);
            lumen.Digits!.DisplayText.ShouldBe("70%");

            CoreFrames.Run(clock, 60);
            Dispatcher.UIThread.RunJobs();
            lumen.Offset.ShouldBe(0);
            basalt.Offset.ShouldBe(0);
            lumen.FillRatio.ShouldBe(0.7, 1e-9);
            basalt.FillRatio.ShouldBe(0.5, 1e-9);
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Rows_Follow_The_Settle_Spring_And_Bars_Their_Own()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            list.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            // The hold: 325ms in 1/60s frames ends on the 20th frame.
            CoreFrames.Run(clock, 20);
            list.Phase.ShouldBe(RankedBarReplayPhase.Moving);
            var lumen = Row(list, "lumen-pro");
            lumen.Offset.ShouldBe(72);
            lumen.FillRatio.ShouldBe(0.625, 1e-9);
            var seconds = CoreFrames.Run(clock, 9);
            var settle = ArcMotionPresets.Get(ArcSpring.Settle);
            lumen.Offset.ShouldBe(SpringSolver.Evaluate(settle, 72, 0, seconds).Displacement, 0.01);
            lumen.FillRatio.ShouldBe(0.7 + SpringSolver.Evaluate(RankedBarList.BarSpring, 0.625 - 0.7, 0, seconds).Displacement, 1e-6);
            RankedBarList.BarSpring.NaturalFrequency.ShouldBe(15.5, 0.05);
            RankedBarList.BarSpring.DampingRatio.ShouldBe(1, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_That_Drop_Out_Fade_And_New_Ones_Fade_In_Growing_From_Zero()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List(RankedBarSamples.Six());
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            var earlier = RankedBarSamples.Rank(("kite-5", 2, 0, 1), ("lumen-pro", 2, 1, 1), ("basalt-ultra", 1, 1, 1),
                ("vireo-4", 1, 0, 2), ("marlin-xl", 0, 1, 1), ("tern-flash", 0, 0, 2));
            list.Replay(earlier);
            Dispatcher.UIThread.RunJobs();
            Order(list).ShouldContain("marlin-xl");
            Order(list).ShouldNotContain("tern-flash");
            // Tern waits invisible at its new rank; it is neither laid out nor read.
            var tern = Row(list, "tern-flash");
            tern.State.ShouldBe(RankedBarRowState.Entering);
            tern.Opacity.ShouldBe(0);
            tern.Slot.ShouldBe(4);
            tern.FillRatio.ShouldBe(0);
            list.Bounds.Height.ShouldBe(5 * 36);
            var peer = ControlAutomationPeer.CreatePeerForElement(list);
            peer.GetChildren().Count.ShouldBe(5);

            CoreFrames.Run(clock, 20);
            Dispatcher.UIThread.RunJobs();
            var marlin = list.LeavingRows.Single();
            marlin.Item.Id.ShouldBe("marlin-xl");
            // The fades start on the next frame: Marlin out over 100ms, Tern in over 160ms.
            CoreFrames.Run(clock, 3);
            marlin.Opacity.ShouldBeInRange(0.01, 0.99);
            tern.State.ShouldBe(RankedBarRowState.Shown);
            tern.Opacity.ShouldBeInRange(0.01, 0.99);
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            peer.GetChildren().Count.ShouldBe(5);

            CoreFrames.Run(clock, 60);
            Dispatcher.UIThread.RunJobs();
            list.LeavingRows.ShouldBeEmpty();
            list.AllRows.Count().ShouldBe(5);
            tern.Opacity.ShouldBe(1);
            tern.FillRatio.ShouldBe(0.5 / 3, 1e-9);
            tern.Digits!.DisplayText.ShouldBe("17%");
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Start_Grows_The_Bars_From_Zero()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            list.Replay([]);
            Dispatcher.UIThread.RunJobs();
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            list.ShownRows.ShouldAllBe(r => r.FillRatio == 0);
            list.ShownRows[0].Digits!.DisplayText.ShouldBe("0%");
            CoreFrames.Run(clock, 80);
            Dispatcher.UIThread.RunJobs();
            list.ShownRows[0].FillRatio.ShouldBe(0.7, 1e-9);
            list.ShownRows[0].Digits!.DisplayText.ShouldBe("70%");
            list.ShownRows.ShouldAllBe(r => r.Offset == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Holds_Then_Puts_Rows_In_Place_And_Tweens_The_Bars()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Reduced);
        try
        {
            list.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            CoreFrames.Run(clock, 18);
            Order(list)[0].ShouldBe("basalt-ultra");
            CoreFrames.Run(clock, 2);
            var lumen = Row(list, "lumen-pro");
            Order(list)[0].ShouldBe("lumen-pro");
            lumen.Offset.ShouldBe(0);
            lumen.Digits!.DisplayText.ShouldBe("70%");
            // 120ms linear: about halfway after 60ms.
            CoreFrames.Run(clock, 4);
            lumen.FillRatio.ShouldBeInRange(0.64, 0.68);
            CoreFrames.Run(clock, 10);
            Dispatcher.UIThread.RunJobs();
            lumen.FillRatio.ShouldBe(0.7, 1e-9);
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Shows_The_End_At_Once()
    {
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Off);
        try
        {
            list.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            list.ShownRows.ShouldAllBe(r => r.Offset == 0 && !r.IsMoving);
            Row(list, "lumen-pro").FillRatio.ShouldBe(0.7, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_Items_During_A_Replay_Finishes_It_First()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            list.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            CoreFrames.Run(clock, 5);
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            list.Items = RankedBarSamples.Six();
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
            Order(list).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra", "vireo-4", "tern-flash"]);
            list.ShownRows.ShouldAllBe(r => !r.IsMoving && r.Offset == 0);
            CoreFrames.Run(clock, 30);
            Order(list)[0].ShouldBe("lumen-pro");

            // Mid-move as well.
            list.Items = RankedBarSamples.Standings();
            list.Replay(RankedBarSamples.Earlier());
            CoreFrames.Run(clock, 24);
            list.Phase.ShouldBe(RankedBarReplayPhase.Moving);
            list.Items = RankedBarSamples.Earlier();
            Dispatcher.UIThread.RunJobs();
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
            Order(list)[0].ShouldBe("basalt-ultra");
            list.ShownRows.ShouldAllBe(r => !r.IsMoving && r.Offset == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Replay_Before_The_List_Is_Shown_Starts_When_It_Is()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var list = List();
        list.Replay(RankedBarSamples.Earlier());
        list.Phase.ShouldBe(RankedBarReplayPhase.Pending);
        var window = TestHelpers.Show(list, ArcMotionLevel.Full);
        try
        {
            Order(list)[0].ShouldBe("basalt-ultra");
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            CoreFrames.Run(clock, 90);
            Dispatcher.UIThread.RunJobs();
            Order(list)[0].ShouldBe("lumen-pro");
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_List_Of_Named_Items_Without_The_Rank_Column()
    {
        var items = RankedBarSamples.Standings();
        items[1] = items[1] with { AutomationName = "" };
        var list = List(items);
        var window = TestHelpers.Show(list);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(list);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.List);
            var children = peer.GetChildren();
            children.Count.ShouldBe(5);
            children.ShouldAllBe(c => c.GetAutomationControlType() == AutomationControlType.ListItem);
            children[0].GetName().ShouldBe("Rank 1, Lumen 3 Pro, win rate 70%");
            // An item without a name gets one built from the list's texts.
            children[1].GetName().ShouldBe("Rank 2, Kite 5, win rate 50%");
            children[0].GetChildren().ShouldBeEmpty();
            AutomationProperties.GetAccessibilityView(list.Ranks!).ShouldBe(AccessibilityView.Raw);
            list.RankFormat = null;
            children = peer.GetChildren();
            children[1].GetName().ShouldBe("Kite 5, win rate 50%");
        }
        finally
        {
            window.Close();
        }
    }
}
