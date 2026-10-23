using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
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

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiPanelPageView"/>: the article column and head, each block kind, gaps, the table layout, the marker, the
/// apply sweep at each motion level (reveal, hold, fade; the text cross-fade; the height spring), following the page data,
/// the page selection with the ask button and its event, and accessible names.
/// </summary>
public class AiPanelPageViewTests
{
    private static AiPanelPageView View(AiPanelPage? page = null) => new()
    {
        Page = page ?? AiPanelPartSamples.Page(),
        AskButtonText = "Ask about selection"
    };

    /// <summary>One 60 Hz frame in ms.</summary>
    private const double Ms = 1000.0 / 60;

    private static Rect BoundsIn(Visual visual, Visual root) =>
        new(visual.TranslatePoint(default, root) ?? default, visual.Bounds.Size);

    #region Layout

    [AvaloniaFact]
    public void The_Article_Is_At_Most_680_Wide_Centered_With_Its_Padding_Inside()
    {
        using var rig = new AiPanelRig(View(), width: 1080);
        var view = (AiPanelPageView)rig.Content;
        var article = view.Article.ShouldNotBeNull();
        var bounds = BoundsIn(article, view);
        bounds.Width.ShouldBe(680, 0.5);
        bounds.X.ShouldBe(200, 0.5);
        article.Padding.ShouldBe(new Thickness(36, 72, 36, 48));
        var title = TestHelpers.Descendant<TextBlock>(view, AiPanelPageView.TitlePart);
        BoundsIn(title, view).TopLeft.ShouldBe(new Point(236, 72));
        title.Text.ShouldBe("Offline sync");
        title.FontSize.ShouldBe(22);
        title.LineHeight.ShouldBe(24.2);
        title.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(title.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Foreground));
        var meta = TestHelpers.Descendant<TextBlock>(view, AiPanelPageView.MetaPart);
        meta.Text.ShouldBe("Design note · Priya Raman · Updated Mar 3");
        meta.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(meta.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.TextMuted));
        BoundsIn(meta, view).Y.ShouldBe(BoundsIn(title, view).Bottom + 6, 0.5);
        // 18px from the head to the first block's text area (its box starts there).
        BoundsIn(view.BlockViews[0], view).Y.ShouldBe(BoundsIn(meta, view).Bottom + 18, 0.5);
    }

    [AvaloniaFact]
    public void A_Narrow_Column_Narrows_The_Article_And_Compact_Uses_The_Sheet_Padding()
    {
        using var rig = new AiPanelRig(View(), width: 500);
        var view = (AiPanelPageView)rig.Content;
        BoundsIn(view.Article!, view).Width.ShouldBe(500, 0.5);
        view.IsCompact = true;
        rig.Settle();
        view.Article!.Padding.ShouldBe(new Thickness(22, 56, 22, 40));
        BoundsIn(view.BlockViews[0], view).X.ShouldBe(22, 0.5);
    }

    [AvaloniaFact]
    public void Blocks_Show_Each_Kind_With_Its_Type_And_Gaps()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        view.BlockViews.Count.ShouldBe(6);
        var paragraph = (AiPanelText)view.BlockViews[0].Box.Body!;
        paragraph.Role.ShouldBe(AiPanelTextRole.Paragraph);
        paragraph.Text.ShouldBe(AiPanelPartSamples.Summary);
        paragraph.FontSize.ShouldBe(14);
        paragraph.LineHeight.ShouldBe(22.4);
        TestHelpers.ColorOf(paragraph.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.TextSecondary));
        var heading = (AiPanelText)view.BlockViews[1].Box.Body!;
        heading.Role.ShouldBe(AiPanelTextRole.Heading);
        heading.LineHeight.ShouldBe(15.4);
        heading.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(heading.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Foreground));
        view.BlockViews[5].Box.Body.ShouldBeOfType<AiPanelTableView>();

        // The box is the text plus 4 above and below; a heading has 20px above it, other blocks 4.
        var first = BoundsIn(view.BlockViews[0], view);
        first.Height.ShouldBe(paragraph.Bounds.Height + 8, 0.5);
        BoundsIn(paragraph, view).Y.ShouldBe(first.Y + 4, 0.5);
        BoundsIn(paragraph, view).X.ShouldBe(first.X, 0.5);
        BoundsIn(view.BlockViews[1], view).Y.ShouldBe(first.Bottom + 20, 0.5);
        BoundsIn(view.BlockViews[2], view).Y.ShouldBe(BoundsIn(view.BlockViews[1], view).Bottom + 4, 0.5);
        // The box reaches 8px past the text on both sides.
        BoundsIn(view.BlockViews[0].Box, view).X.ShouldBe(first.X - 8, 0.5);
        view.BlockViews[0].Box.Bounds.Width.ShouldBe(first.Width + 16, 0.5);
        // The article ends 48px under the last block.
        view.Article!.DesiredSize.Height.ShouldBe(BoundsIn(view.BlockViews[5], view.Article).Bottom + 48, 0.5);
    }

    [AvaloniaFact]
    public void The_Table_Fills_The_Column_In_Proportion_To_Its_Longest_Lines_With_Header_And_Row_Lines()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var block = view.BlockViews[5];
        var table = (AiPanelTableView)block.Box.Body!;
        // 6px between the box padding and the table.
        BoundsIn(table, view).Y.ShouldBe(BoundsIn(block, view).Y + 4 + 6, 0.5);
        table.Bounds.Width.ShouldBe(608, 0.5);
        table.ColumnWidths.Sum().ShouldBe(608, 0.5);
        TestHelpers.ColorOf(table.LineBrush).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.BorderSubtle));
        // Header: the 12px text, 8 under it, a 1px line. Rows: 8 + text + 8 (+1 line, none under the last).
        var header = table.Headers[0];
        header.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(header.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.TextMuted));
        table.RowHeights[0].ShouldBe(header.Bounds.Height + 8 + 1, 0.5);
        var cell = table.Rows[0][1];
        table.RowHeights[1].ShouldBe(8 + cell.Bounds.Height + 8 + 1, 0.5);
        table.RowHeights[^1].ShouldBe(8 + cell.Bounds.Height + 8, 0.5);
        // Row headers are Foreground 500; the right-aligned column hugs the right edge with tabular figures.
        var rowHeader = table.Rows[0][0];
        rowHeader.Role.ShouldBe(AiPanelTextRole.RowHeader);
        rowHeader.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(rowHeader.Foreground).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Foreground));
        var cache = table.Rows[0][2];
        BoundsIn(cache, table).Right.ShouldBe(608, 0.5);
        BoundsIn(table.Headers[2], table).Right.ShouldBe(608, 0.5);
        cache.FontFeatures.ShouldNotBeNull().Count.ShouldBe(1);
        // Proportional to the longest lines (12px after every column but the last).
        var maxes = new double[3];
        foreach (var (text, _, column) in table.Cells())
        {
            text.Measure(Size.Infinity);
            maxes[column] = Math.Max(maxes[column], Math.Ceiling(text.DesiredSize.Width) + (column < 2 ? 12 : 0));
        }
        for (var c = 0; c < 3; c++)
        {
            table.ColumnWidths[c].ShouldBe(maxes[c] * 608 / maxes.Sum(), 0.5);
        }
    }

    [AvaloniaFact]
    public void Each_Block_Is_Named_By_Its_Label()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        AutomationProperties.GetName(view.BlockViews[0]).ShouldBe("Summary");
        AutomationProperties.GetName(view.BlockViews[1]).ShouldBeNull();
        AutomationProperties.GetName(view.BlockViews[5]).ShouldBe("Storage table");
        view.Page!.Blocks[0].Label = "Overview";
        rig.Settle();
        AutomationProperties.GetName(view.BlockViews[0]).ShouldBe("Overview");
    }

    #endregion

    #region Marker

    [AvaloniaFact]
    public void The_Marker_Shows_On_The_Marked_Block_16px_Left_Of_The_Text_And_4px_In()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        view.BlockViews.ShouldAllBe(b => !b.IsMarked);
        view.MarkedBlockId = "limits-table";
        rig.Settle();
        var block = view.BlockViews[5];
        block.IsMarked.ShouldBeTrue();
        view.BlockViews.Count(b => b.IsMarked).ShouldBe(1);
        block.Marker.Opacity.ShouldBe(1);
        TestHelpers.ColorOf(block.Marker.Fill).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Accent));
        var marker = BoundsIn(block.Marker, block);
        marker.ShouldBe(new Rect(-16, 4, 2, block.Bounds.Height - 8));
        view.MarkedBlockId = null;
        rig.Settle();
        block.IsMarked.ShouldBeFalse();
        block.Marker.Opacity.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Marker_Fades_In_160ms_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(View(), level: ArcMotionLevel.Full, clock: clock);
        var view = (AiPanelPageView)rig.Content;
        view.MarkedBlockId = "summary";
        rig.Settle();
        var marker = view.BlockViews[0].Marker;
        marker.Transitions.ShouldNotBeNull().Count.ShouldBe(1);
        marker.Transitions[0].ShouldBeOfType<ArcDoubleTransition>().Duration.ShouldBe(TimeSpan.FromMilliseconds(160));
    }

    #endregion

    #region Apply sweep

    private static (AiPanelRig Rig, AiPanelPageView View, AiPanelBlockView Block) PlayRig(ArcMotionLevel level, ManualFrameClock? clock = null)
    {
        var rig = new AiPanelRig(View(), level: level, clock: clock);
        var view = (AiPanelPageView)rig.Content;
        return (rig, view, view.BlockViews[0]);
    }

    private static AiPanelParagraph Short() => new() { Id = "summary", Label = "Summary", Text = AiPanelPartSamples.ShortSummary };

    [AvaloniaFact]
    public void Off_Replaces_The_Block_At_Once_Without_A_Sweep()
    {
        var (rig, view, block) = PlayRig(ArcMotionLevel.Off);
        using (rig)
        {
            var before = block.Bounds.Height;
            view.PlayChange("summary", Short(), isUndo: false).ShouldBeTrue();
            rig.Settle();
            ((AiPanelText)block.Box.Body!).Text.ShouldBe(AiPanelPartSamples.ShortSummary);
            block.Box.Leaving.ShouldBeNull();
            block.Box.Sweep.IsVisible.ShouldBeFalse();
            block.Box.Body!.Opacity.ShouldBe(1);
            block.Bounds.Height.ShouldBeLessThan(before);
            block.Bounds.Height.ShouldBe(block.Box.Body.Bounds.Height + 8, 0.5);
            view.PlayChange("nope", Short(), false).ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void Full_Sweeps_Left_To_Right_In_420ms_Holds_And_Fades_Out_By_1400ms()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            view.PlayChange("summary", Short(), false);
            rig.Settle();
            var sweep = block.Box.Sweep;
            sweep.IsVisible.ShouldBeTrue();
            sweep.Reveal.ShouldBe(0, 0.001);
            // The tint covers the new text's area: inset 8 / 4 in the box.
            BoundsIn(sweep, block.Box).ShouldBe(new Rect(8, 4, block.Box.Bounds.Width - 16, block.Box.Body!.DesiredSize.Height));
            TestHelpers.ColorOf(sweep.Tint).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Accent));
            rig.FrameCount(13);
            sweep.Reveal.ShouldBe(ArcEasings.Standard.Ease(13 * Ms / 420), 0.001);
            sweep.Fade.ShouldBe(1);
            rig.FrameCount(13);
            sweep.Reveal.ShouldBe(1);
            rig.FrameCount(20);
            // Still held at 767ms.
            sweep.Fade.ShouldBe(1);
            rig.FrameCount(19);
            // 1083ms: half way through the 630ms fade on CSS ease.
            sweep.Fade.ShouldBe(1 - AiPanelSweep.CssEase.Ease((65 * Ms - 770) / 630), 0.001);
            rig.FrameCount(20);
            sweep.IsVisible.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void Full_Fades_The_Old_Text_Out_In_100ms_And_The_New_In_After_80ms_Over_180ms()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            var old = block.Box.Body!;
            view.PlayChange("summary", Short(), false);
            rig.Settle();
            block.Box.Leaving.ShouldBeSameAs(old);
            old.IsHitTestVisible.ShouldBeFalse();
            var fresh = block.Box.Body!;
            fresh.ShouldNotBeSameAs(old);
            fresh.Opacity.ShouldBe(0);
            // The old text is over the new one, both at the top left of the text area.
            block.Box.GetVisualChildren().ToList().IndexOf(old).ShouldBeGreaterThan(block.Box.GetVisualChildren().ToList().IndexOf(fresh));
            BoundsIn(old, block.Box).TopLeft.ShouldBe(new Point(8, 4));
            rig.FrameCount(3);
            old.Opacity.ShouldBe(1 - AiPanelBlockBox.EaseOut.Ease(0.5), 0.001);
            fresh.Opacity.ShouldBe(0);
            rig.FrameCount(4);
            // 117ms: the old text left at 100ms; the new one is 37ms into its 180ms fade.
            block.Box.Leaving.ShouldBeNull();
            fresh.Opacity.ShouldBe(ArcEasings.Standard.Ease((7 * Ms - 80) / 180), 0.001);
            rig.FrameCount(3);
            fresh.Opacity.ShouldBe(ArcEasings.Standard.Ease((10 * Ms - 80) / 180), 0.001);
            rig.FrameCount(6);
            fresh.Opacity.ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void Full_Springs_The_Height_From_The_Old_To_The_New_Without_Jumping_First()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            var from = block.Bounds.Height;
            var next = BoundsIn(view.BlockViews[1], view).Y;
            view.PlayChange("summary", Short(), false);
            rig.Settle();
            // The measure frame keeps the old height (Arc flashes the new height for one frame).
            block.Bounds.Height.ShouldBe(from, 0.5);
            BoundsIn(view.BlockViews[1], view).Y.ShouldBe(next, 0.5);
            var to = block.Box.Body!.DesiredSize.Height + 8;
            to.ShouldBeLessThan(from);
            var spec = ArcMotionPresets.Settle;
            var seen = new List<double>();
            for (var frame = 1; frame <= 12; frame++)
            {
                rig.Frame();
                seen.Add(block.Bounds.Height);
            }
            // Monotonic, no overshoot, close to the critically damped curve.
            seen.ShouldBe(seen.OrderByDescending(h => h).ToList());
            seen.ShouldAllBe(h => h >= to - 0.5);
            var (x, _) = SpringSolver.Evaluate(spec, from - to, 0, 6.0 / 60);
            seen[5].ShouldBe(to + x, 1.5);
            // The blocks below follow.
            BoundsIn(view.BlockViews[1], view).Y.ShouldBeLessThan(next);
            rig.Frames(900);
            block.Bounds.Height.ShouldBe(to, 0.5);
            block.Box.IsMoving.ShouldBeFalse();
            block.Box.Clip.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public void Reduced_Fades_The_Whole_Tint_In_600ms_Cross_Fades_In_120ms_And_Jumps_The_Height()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Reduced, clock);
        using (rig)
        {
            view.PlayChange("summary", Short(), false);
            rig.Settle();
            rig.Frame();
            var sweep = block.Box.Sweep;
            sweep.Reveal.ShouldBe(1);
            block.Bounds.Height.ShouldBe(block.Box.Body!.DesiredSize.Height + 8, 0.5);
            rig.FrameCount(3);
            // 67ms into a 120ms linear fade.
            var fresh = block.Box.Body!;
            fresh.Opacity.ShouldBe(4 * Ms / 120, 0.001);
            rig.FrameCount(5);
            fresh.Opacity.ShouldBe(1);
            block.Box.Leaving.ShouldBeNull();
            rig.FrameCount(13);
            // 367ms into the 600ms fade, revealed all along.
            sweep.Fade.ShouldBe(1 - AiPanelSweep.CssEase.Ease(22 * Ms / 600), 0.001);
            sweep.Reveal.ShouldBe(1);
            rig.FrameCount(15);
            sweep.IsVisible.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void An_Undo_Plays_The_Same_Sweep()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            view.PlayChange("summary", Short(), false);
            rig.Frames(1600);
            view.PlayChange("summary", new AiPanelParagraph { Id = "summary", Text = AiPanelPartSamples.Summary }, isUndo: true);
            rig.FrameCount(13);
            block.Box.Sweep.Reveal.ShouldBe(ArcEasings.Standard.Ease(13 * Ms / 420), 0.001);
            rig.Frames(1400);
            ((AiPanelText)block.Box.Body!).Text.ShouldBe(AiPanelPartSamples.Summary);
            block.Box.Sweep.IsVisible.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void A_Table_Change_Plays_On_The_Table()
    {
        var clock = new ManualFrameClock();
        var (rig, view, _) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            var block = view.BlockViews[5];
            var from = block.Bounds.Height;
            view.PlayChange("limits-table", AiPanelPartSamples.Table(withSync: true), false);
            rig.Frames(100);
            var table = (AiPanelTableView)block.Box.Body!;
            table.Headers.Count.ShouldBe(4);
            table.Headers[3].Text.ShouldBe("Sync");
            block.Box.Sweep.IsVisible.ShouldBeTrue();
            rig.Frames(1500);
            block.Box.Sweep.IsVisible.ShouldBeFalse();
            block.Bounds.Height.ShouldBe(from, 0.5);
        }
    }

    #endregion

    #region Following the data

    [AvaloniaFact]
    public void Text_Changes_And_Added_Removed_Or_Replaced_Blocks_Show_On_The_Next_Turn()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var page = view.Page!;
        ((AiPanelParagraph)page.Blocks[2]).Text = "Only notes are cached.";
        Dispatcher.UIThread.RunJobs();
        rig.Settle();
        ((AiPanelText)view.BlockViews[2].Box.Body!).Text.ShouldBe("Only notes are cached.");

        page.Blocks.Add(new AiPanelParagraph { Id = "extra", Label = "Extra", Text = "One more paragraph." });
        page.Blocks.RemoveAt(1);
        rig.Settle();
        view.BlockViews.Select(b => b.Block.Id).ShouldBe(["summary", "scope", "limits-heading", "limits", "limits-table", "extra"]);
        // The first block keeps no gap above it; the scope paragraph now follows the summary 4px down.
        BoundsIn(view.BlockViews[1], view).Y.ShouldBe(BoundsIn(view.BlockViews[0], view).Bottom + 4, 0.5);

        var kept = view.BlockViews[0];
        page.Blocks[0] = new AiPanelParagraph { Id = "summary", Label = "Summary", Text = "Replaced." };
        rig.Settle();
        view.BlockViews[0].ShouldBeSameAs(kept);
        ((AiPanelText)kept.Box.Body!).Text.ShouldBe("Replaced.");

        var table = (AiPanelTable)page.Find("limits-table")!;
        table.Rows[0].Cells[2] = "250 MB";
        rig.Settle();
        ((AiPanelTableView)view.BlockViews[4].Box.Body!).Rows[0][2].Text.ShouldBe("250 MB");
        table.Columns.Add(new AiPanelColumn { Header = "Sync" });
        rig.Settle();
        ((AiPanelTableView)view.BlockViews[4].Box.Body!).Headers.Count.ShouldBe(4);

        view.Page = new AiPanelPage { Title = "Empty" };
        rig.Settle();
        view.BlockViews.ShouldBeEmpty();
        TestHelpers.Descendant<TextBlock>(view, AiPanelPageView.MetaPart).IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Host_That_Updates_The_Data_Before_Playing_Keeps_The_Old_Text_Leaving()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            var paragraph = (AiPanelParagraph)view.Page!.Blocks[0];
            paragraph.Text = AiPanelPartSamples.ShortSummary;
            view.PlayChange("summary", paragraph, false);
            rig.Settle();
            ((AiPanelText)block.Box.Leaving!).Text.ShouldBe(AiPanelPartSamples.Summary);
            ((AiPanelText)block.Box.Body!).Text.ShouldBe(AiPanelPartSamples.ShortSummary);
            block.Box.Sweep.IsVisible.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void A_Host_That_Plays_Then_Replaces_The_Block_Object_Keeps_The_Animation()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            var after = Short();
            view.PlayChange("summary", after, false);
            view.Page!.Blocks[0] = after.Clone();
            rig.Settle();
            rig.Frame();
            view.BlockViews[0].ShouldBeSameAs(block);
            block.Box.Leaving.ShouldNotBeNull();
            block.Box.Sweep.IsVisible.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void ReplaceBlock_Shows_The_New_Content_At_Once_Even_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        var (rig, view, block) = PlayRig(ArcMotionLevel.Full, clock);
        using (rig)
        {
            view.ReplaceBlock("summary", Short()).ShouldBeTrue();
            rig.Settle();
            block.Box.Leaving.ShouldBeNull();
            block.Box.Sweep.IsVisible.ShouldBeFalse();
            block.Bounds.Height.ShouldBe(block.Box.Body!.DesiredSize.Height + 8, 0.5);
        }
    }

    #endregion

    #region Selection

    private static Point TextPoint(AiPanelRig rig, AiPanelText text, int offset, bool end = false)
    {
        var rect = text.RangeRects(offset, offset + 1).First();
        return rig.PointIn(text, new Point(end ? rect.Right : rect.Left + 0.5, rect.Center.Y));
    }

    [AvaloniaFact]
    public void Dragging_Across_Blocks_Selects_Every_Text_In_Between_And_Release_Shows_The_Ask_Button()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var summary = view.TextOf(0);
        var scope = view.TextOf(2);
        rig.Down(TextPoint(rig, summary, 8));
        rig.Drag(TextPoint(rig, scope, 4, end: true));
        summary.SelectionStart.ShouldBe(8);
        summary.SelectionEnd.ShouldBe(summary.Length);
        view.TextOf(1).SelectionEnd.ShouldBe(view.TextOf(1).Length);
        scope.SelectionStart.ShouldBe(0);
        scope.SelectionEnd.ShouldBe(5);
        view.IsAskShown.ShouldBeFalse();
        view.Selection.ShouldBeNull();
        rig.Up(TextPoint(rig, scope, 4, end: true));
        view.Selection.ShouldBe(new AiPanelSelection("summary", "Summary", AiPanelPartSamples.Summary[8..] + "\nScope\nNotes"));
        view.IsAskShown.ShouldBeTrue();
        var ask = view.AskButton;
        ask.IsVisible.ShouldBeTrue();
        ask.Text.ShouldBe("Ask about selection");
        AutomationProperties.GetName(ask).ShouldBe("Ask about selection");
        // Under the selection's last line: its right edge − 24, its bottom + 8 (article coordinates).
        var line = scope.RangeRects(0, 5).Last();
        var lineInArticle = new Rect(scope.TranslatePoint(line.TopLeft, view.Article!)!.Value, line.Size);
        var placed = BoundsIn(ask, view.Article!);
        placed.X.ShouldBe(lineInArticle.Right - 24, 0.5);
        placed.Y.ShouldBe(lineInArticle.Bottom + 8, 0.5);
        // The selection colors: SurfaceMuted under Foreground.
        TestHelpers.ColorOf(scope.SelectionBrush).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(scope.SelectionForegroundBrush).ShouldBe(ArcTokens.GetColor(view, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void Pressing_The_Ask_Button_Raises_SelectionRequested_And_Clears_The_Selection()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        AiPanelSelection? asked = null;
        view.SelectionRequested += (_, e) => asked = e.Selection;
        var limits = view.TextOf(4);
        rig.Down(TextPoint(rig, limits, 0));
        rig.Up(TextPoint(rig, limits, 10, end: true));
        var selection = view.Selection.ShouldNotBeNull();
        selection.Text.ShouldBe("Each device");
        rig.Click(view.AskButton);
        asked.ShouldBe(new AiPanelSelection("limits", "Limits", "Each device"));
        view.Selection.ShouldBeNull();
        view.IsAskShown.ShouldBeFalse();
        limits.SelectionStart.ShouldBe(limits.SelectionEnd);
    }

    [AvaloniaFact]
    public void A_Press_Elsewhere_Or_A_Click_Without_Drag_Hides_The_Button_And_Clears_The_Selection()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var summary = view.TextOf(0);
        rig.Down(TextPoint(rig, summary, 0));
        rig.Up(TextPoint(rig, summary, 6, end: true));
        view.IsAskShown.ShouldBeTrue();
        rig.Click(TextPoint(rig, view.TextOf(2), 3));
        view.IsAskShown.ShouldBeFalse();
        view.Selection.ShouldBeNull();
        summary.SelectionEnd.ShouldBe(summary.SelectionStart);

        rig.Down(TextPoint(rig, summary, 0));
        rig.Up(TextPoint(rig, summary, 6, end: true));
        view.ClearSelection();
        rig.Settle();
        view.IsAskShown.ShouldBeFalse();
        summary.SelectionEnd.ShouldBe(summary.SelectionStart);
    }

    [AvaloniaFact]
    public void A_Press_On_The_Title_Selects_Nothing()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var title = TestHelpers.Descendant<TextBlock>(view, AiPanelPageView.TitlePart);
        rig.Down(rig.CenterOf(title));
        rig.Up(rig.CenterOf(view.TextOf(0)));
        view.Selection.ShouldBeNull();
        view.IsAskShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Table_Selection_Takes_The_Table_Label_And_The_Button_Stays_14px_Inside_The_Right_Edge()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var table = (AiPanelTableView)view.BlockViews[5].Box.Body!;
        var first = table.Rows[0][0];
        var last = table.Rows[2][2];
        rig.Down(TextPoint(rig, first, 0));
        rig.Up(TextPoint(rig, last, last.Length - 1, end: true));
        var selection = view.Selection.ShouldNotBeNull();
        selection.BlockId.ShouldBe("limits-table");
        selection.Label.ShouldBe("Storage table");
        selection.Text.ShouldBe("Basic\t1 device\t200 MB\nPlus\t3 devices\t1 GB\nOrg\tUnlimited\t5 GB");
        var ask = view.AskButton;
        BoundsIn(ask, view.Article!).Right.ShouldBe(view.Article!.Bounds.Width - 14, 0.5);
    }

    [AvaloniaFact]
    public void A_New_Selection_Moves_The_Button_By_Fading_One_Out_While_The_Other_Fades_In()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(View(), level: ArcMotionLevel.Full, clock: clock);
        var view = (AiPanelPageView)rig.Content;
        var summary = view.TextOf(0);
        rig.Down(TextPoint(rig, summary, 0));
        rig.Up(TextPoint(rig, summary, 6, end: true));
        var first = view.AskButton;
        first.Opacity.ShouldBe(0, 0.01);
        rig.FrameCount(12);
        first.Opacity.ShouldBe(1);
        // The press of a new selection hides the button (Arc's press clears the old selection).
        var scope = view.TextOf(2);
        rig.Down(TextPoint(rig, scope, 0));
        rig.Up(TextPoint(rig, scope, 4, end: true));
        var second = view.AskButton;
        second.ShouldNotBeSameAs(first);
        rig.Frames(50);
        first.Opacity.ShouldBeLessThan(0.6);
        second.Opacity.ShouldBeGreaterThan(0.1);
        second.IsVisible.ShouldBeTrue();
        rig.Frames(200);
        first.IsVisible.ShouldBeFalse();
        second.Opacity.ShouldBe(1);
        second.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Double_Press_Selects_A_Word()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var limits = view.TextOf(4);
        var point = TextPoint(rig, limits, 7);
        rig.Click(point);
        rig.Down(point);
        rig.Up(point);
        view.Selection.ShouldNotBeNull().Text.ShouldBe("device");
    }

    [AvaloniaFact]
    public void A_Change_To_The_Page_Ends_The_Selection()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        var summary = view.TextOf(0);
        rig.Down(TextPoint(rig, summary, 0));
        rig.Up(TextPoint(rig, summary, 6, end: true));
        view.IsAskShown.ShouldBeTrue();
        ((AiPanelParagraph)view.Page!.Blocks[2]).Text = "Changed.";
        rig.Settle();
        view.Selection.ShouldBeNull();
        view.IsAskShown.ShouldBeFalse();
        summary.SelectionEnd.ShouldBe(summary.SelectionStart);
    }

    [AvaloniaFact]
    public void The_Page_Is_Not_A_Tab_Stop_And_Takes_Focus_On_A_Press_For_Copy()
    {
        using var rig = new AiPanelRig(View());
        var view = (AiPanelPageView)rig.Content;
        view.IsTabStop.ShouldBeFalse();
        rig.Down(TextPoint(rig, view.TextOf(0), 0));
        rig.Up(TextPoint(rig, view.TextOf(0), 6, end: true));
        view.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void ScrollToBlock_Brings_A_Block_Into_View()
    {
        using var rig = new AiPanelRig(View(), width: 700, height: 260);
        var view = (AiPanelPageView)rig.Content;
        view.Scroller!.Offset.Y.ShouldBe(0);
        view.ScrollToBlock("limits-table").ShouldBeTrue();
        rig.Settle();
        view.Scroller.Offset.Y.ShouldBeGreaterThan(0);
        var table = BoundsIn(view.BlockViews[5], view);
        table.Bottom.ShouldBeLessThanOrEqualTo(view.Bounds.Height + 0.5);
        view.ScrollToBlock("missing").ShouldBeFalse();
    }

    #endregion
}
