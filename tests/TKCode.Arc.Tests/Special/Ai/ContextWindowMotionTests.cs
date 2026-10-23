using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The measured motion of ContextWindow (notes section 6): per-cell recolor waves (9ms per cell for an append, 3ms for an
/// insert or a compaction, both capped at 360ms; 1.4ms from cell 0 for a model switch), 240ms recolors, the fold sweep
/// (min(6ms, 420ms / (n − 1)) per cell, 1 → 0.35 at 55% → 0.5 over 720ms), 400ms ease-out-cubic number rolls from the
/// value on screen, the 160ms legend hover, and reduced motion.
/// </summary>
public class ContextWindowMotionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (ContextWindow Card, Window Window, ContextWindowTestTimers Timers) Open(ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var timers = new ContextWindowTestTimers();
        var card = new ContextWindow { Timers = timers, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = TestHelpers.Show(card, level);
        window.Width = 792;
        window.Height = 900;
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        return (card, window, timers);
    }

    // The manual clock runs whole frames until the time has passed (it may overshoot by part of a frame).
    private static void Run(ManualFrameClock clock, double milliseconds) =>
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds), Frame);

    private static void Frames(ManualFrameClock clock, int count) => clock.Advance(Frame * count, Frame);

    [Fact]
    public void Wave_Delays_Follow_The_Measured_Steps()
    {
        ContextWindowGrid.Delay(ContextWindowWave.Append, 87, 87).ShouldBe(0);
        ContextWindowGrid.Delay(ContextWindowWave.Append, 80, 87).ShouldBe(0);
        ContextWindowGrid.Delay(ContextWindowWave.Append, 94, 87).ShouldBe(0.063, 1e-9);
        ContextWindowGrid.Delay(ContextWindowWave.Append, 87 + 50, 87).ShouldBe(0.36);
        ContextWindowGrid.Delay(ContextWindowWave.Insert, 46, 26).ShouldBe(0.06, 1e-9);
        ContextWindowGrid.Delay(ContextWindowWave.Insert, 26 + 121, 26).ShouldBe(0.36);
        ContextWindowGrid.Delay(ContextWindowWave.Relayout, 199, 0).ShouldBe(0.2786, 1e-9);
        ContextWindowGrid.Delay(ContextWindowWave.None, 199, 0).ShouldBe(0);
    }

    [Fact]
    public void The_Fold_Keyframes_Dip_To_035_And_Rest_At_05()
    {
        ContextWindowGrid.FoldOpacity(-0.1).ShouldBe(1);
        ContextWindowGrid.FoldOpacity(0).ShouldBe(1);
        ContextWindowGrid.FoldOpacity(0.72 * 0.55).ShouldBe(0.35, 1e-6);
        ContextWindowGrid.FoldOpacity(0.72).ShouldBe(0.5);
        ContextWindowGrid.FoldOpacity(5).ShouldBe(0.5);
        // The first segment eases out fast: well under halfway down a fifth of the way in.
        ContextWindowGrid.FoldOpacity(0.72 * 0.55 * 0.2).ShouldBeLessThan(0.7);
    }

    [AvaloniaFact]
    public void A_Send_Recolors_From_The_First_New_Cell_At_9ms_Per_Cell()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var grid = card.GridPartControl!;
            var t0 = grid.Now;
            card.Send();
            // 436K → 466.6K: the conversation grows from cell 87 to cell 92.
            grid.KindAt(87).ShouldBe(ContextWindowCellKind.Messages);
            grid.KindAt(92).ShouldBe(ContextWindowCellKind.Messages);
            grid.KindAt(93).ShouldBe(ContextWindowCellKind.Free);
            grid.StartOf(87).ShouldBe(t0, 1e-9);
            grid.StartOf(88).ShouldBe(t0 + 0.009, 1e-9);
            grid.StartOf(92).ShouldBe(t0 + 0.045, 1e-9);
            var free = grid.FillAt(92);
            Run(clock, 120);
            var mid = grid.FillAt(92);
            Run(clock, 300);
            var done = grid.FillAt(92);
            grid.IsMoving.ShouldBeFalse();
            // The new cell moves from the free fill to the conversation blue in OKLab, through a lighter blue.
            done.ShouldNotBe(free);
            mid.ShouldNotBe(free);
            mid.ShouldNotBe(done);
            (mid.R + mid.G + mid.B).ShouldBeGreaterThan(done.R + done.G + done.B);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Attachment_Inserts_At_3ms_Per_Cell_From_The_First_New_File_Cell()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var grid = card.GridPartControl!;
            var t0 = grid.Now;
            card.Attach();
            grid.KindAt(26).ShouldBe(ContextWindowCellKind.Files);
            grid.StartOf(26).ShouldBe(t0, 1e-9);
            grid.StartOf(30).ShouldBe(t0 + 0.012, 1e-9);
            grid.StartOf(99).ShouldBe(t0 + 0.219, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Model_Switch_Recolors_Every_Changed_Cell_At_1_4ms_From_Cell_0()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var grid = card.GridPartControl!;
            var t0 = grid.Now;
            card.SelectedModel = card.EffectiveModels[1];
            // Cell 7 was a file on the 1M window and is a tool on 512K.
            grid.KindAt(7).ShouldBe(ContextWindowCellKind.Tools);
            grid.StartOf(7).ShouldBe(t0 + 0.0098, 1e-9);
            grid.KindAt(150).ShouldBe(ContextWindowCellKind.Messages);
            grid.StartOf(150).ShouldBe(t0 + 0.21, 1e-9);
            // The large number keeps its value; the free tokens jump; the reserve row rolls from 64K to 32K.
            card.ReadoutPartControl!.NumberText.ShouldBe("436K");
            card.ReadoutPartControl.FreeText.ShouldBe("44K free");
            var reserve = card.LegendRows[(int)ContextWindowPart.Reserve]!;
            reserve.TokensText.ShouldBe("64K");
            Run(clock, 450);
            reserve.TokensText.ShouldBe("32K");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compaction_Sweeps_Oldest_To_Newest_Then_Lands_At_Full_Opacity()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, timers) = Open();
        try
        {
            var grid = card.GridPartControl!;
            card.Compact();
            grid.IsFolding.ShouldBeTrue();
            // 61 conversation cells from cell 26: 420 / 60 = 7ms, capped at 6ms.
            grid.FoldDelayOf(25).ShouldBe(-1);
            grid.FoldDelayOf(26).ShouldBe(0);
            grid.FoldDelayOf(27).ShouldBe(0.006, 1e-9);
            grid.FoldDelayOf(86).ShouldBe(0.36, 1e-9);
            grid.FoldDelayOf(87).ShouldBe(-1);
            Run(clock, 0.72 * 0.55 * 1000);
            grid.OpacityAt(26).ShouldBe(0.35, 0.02);
            grid.OpacityAt(86).ShouldBeGreaterThan(0.35);
            grid.OpacityAt(0).ShouldBe(1);
            timers.Advance(700);
            grid.IsFolding.ShouldBeFalse();
            grid.OpacityAt(26).ShouldBe(1);
            grid.OpacityAt(86).ShouldBe(1);
            // The summary lands right after the files and recolors at 3ms per cell from its first cell.
            var t = grid.Now;
            grid.KindAt(26).ShouldBe(ContextWindowCellKind.Summary);
            grid.StartOf(26).ShouldBe(t, 1e-9);
            grid.StartOf(27).ShouldBe(t + 0.003, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Long_Conversation_Sweeps_Within_420ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var grid = card.GridPartControl!;
            grid.BeginFold(53, 114);
            grid.FoldDelayOf(54).ShouldBe(0.42 / 113, 1e-9);
            grid.FoldDelayOf(53 + 113).ShouldBe(0.42, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Numbers_Roll_For_400ms_From_The_Value_On_Screen()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var readout = card.ReadoutPartControl!;
            card.Send();
            // The words around the numbers change at once; the numbers start where they were (never backwards).
            readout.FreeText.ShouldBe("469K free");
            readout.NumberText.ShouldBe("436K");
            // One frame in (1/60 s): already on the way up, never a step backwards.
            Frames(clock, 1);
            readout.NumberText.ShouldBe("440K");
            // Halfway (0.2 s): ease-out cubic gives 436K + 30.6K × 0.875.
            Frames(clock, 11);
            readout.NumberText.ShouldBe("463K");
            Frames(clock, 13);
            readout.NumberText.ShouldBe("467K");
            readout.PercentText.ShouldBe("47%");
            // A second send restarts from the value on screen.
            card.Send();
            readout.NumberText.ShouldBe("467K");
            Frames(clock, 30);
            readout.NumberText.ShouldBe("494K");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Legend_Row_Quiets_The_Other_Cells_And_Rows_In_160ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, _) = Open();
        try
        {
            var grid = card.GridPartControl!;
            card.SetHover(ContextWindowPart.Files);
            grid.OpacityAt(0).ShouldBe(1);
            Run(clock, 60);
            grid.OpacityAt(0).ShouldBeInRange(0.29, 0.99);
            Run(clock, 140);
            grid.OpacityAt(0).ShouldBe(0.28, 1e-9);
            grid.OpacityAt(10).ShouldBe(1);
            card.LegendRows[(int)ContextWindowPart.System]!.IsDimmed.ShouldBeTrue();
            card.LegendRows[(int)ContextWindowPart.Files]!.IsDimmed.ShouldBeFalse();
            card.SetHover(null);
            Run(clock, 200);
            grid.OpacityAt(0).ShouldBe(1);
            card.LegendRows[(int)ContextWindowPart.System]!.IsDimmed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Lands_Everything_At_Once_And_Keeps_Every_Status()
    {
        var (card, window, timers) = Open(ArcMotionLevel.Reduced);
        try
        {
            var grid = card.GridPartControl!;
            card.Send();
            card.ReadoutPartControl!.NumberText.ShouldBe("467K");
            grid.IsMoving.ShouldBeFalse();
            card.Compact();
            grid.IsFolding.ShouldBeFalse();
            timers.Requested[^1].ShouldBe(0);
            timers.Advance(0);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            card.Announcer!.History.ShouldBe([
                "Added “Add a dry-run flag to the import job” (+31K)",
                "Folding 23 earlier messages…",
                "Folded 291K into a 17K summary"
            ]);
            card.SetHover(ContextWindowPart.Tools);
            grid.OpacityAt(0).ShouldBe(0.28, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }
}
