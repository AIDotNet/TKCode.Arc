using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The diff editor (upstream's DiffEditorWidget as a behavior reference): two editors with the diff as decorations,
/// aligned lines, inline removed lines, hidden unchanged regions, moved blocks, revert, navigation and the overview ruler.
/// </summary>
public class DiffEditorTests
{
    private static string Lines(int count, Func<int, string>? line = null) =>
        string.Join('\n', Enumerable.Range(1, count).Select(i => line?.Invoke(i) ?? $"const value{i} = {i};"));

    private static (DiffEditor Diff, Window Window) Show(string original, string modified, Action<DiffEditor>? configure = null, double width = 900, double height = 400)
    {
        var diff = new DiffEditor { OriginalText = original, ModifiedText = modified, Width = width, Height = height };
        configure?.Invoke(diff);
        var window = TestHelpers.Show(diff);
        window.Width = width + 40;
        window.Height = height + 40;
        Render(window);
        diff.ComputeDiffNow();
        Render(window);
        return (diff, window);
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void WaitForDiff(DiffEditor diff, Window window)
    {
        var task = diff.WaitForDiffAsync();
        // timers (the pause after an edit) and the thread pool's result posted back
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue("the diff was not computed in time");
        Render(window);
    }

    private static IEnumerable<ModelDecoration> Decorations(CodeEditor editor, string className) =>
        editor.Model.GetAllDecorations().Where(d => d.Options.ClassName == className);

    [AvaloniaFact]
    public void It_shows_the_two_texts_side_by_side_with_only_the_modified_one_editable()
    {
        var (diff, _) = Show("a\nb", "a\nc");

        var original = diff.OriginalEditor.ShouldNotBeNull();
        var modified = diff.ModifiedEditor.ShouldNotBeNull();
        original.Text.ShouldBe("a\nb");
        modified.Text.ShouldBe("a\nc");
        original.IsReadOnly.ShouldBeTrue();
        modified.IsReadOnly.ShouldBeFalse();
        original.IsVisible.ShouldBeTrue();
        original.Bounds.Width.ShouldBeGreaterThan(300);
        modified.Bounds.Left.ShouldBeGreaterThan(original.Bounds.Right - 1);

        diff.IsReadOnly = true;
        modified.IsReadOnly.ShouldBeTrue();
        diff.LanguageId = "csharp";
        original.LanguageId.ShouldBe("csharp");
        modified.LanguageId.ShouldBe("csharp");
    }

    [AvaloniaFact]
    public void The_theme_gives_it_its_brushes_and_geometry()
    {
        var (diff, _) = Show("a", "b");

        diff.InsertedLineBrush.ShouldNotBeNull();
        diff.RemovedLineBrush.ShouldNotBeNull();
        diff.InsertedTextBrush.ShouldNotBeNull();
        diff.RemovedTextBrush.ShouldNotBeNull();
        diff.CollapsedRegionBackground.ShouldNotBeNull();
        diff.IndicatorWidth.ShouldBe(new DiffEditorToken().Also(t => t.CalculateTokenValues(false)).IndicatorWidth);
        diff.OverviewRuler.ShouldNotBeNull().Bounds.Width.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void The_diff_is_computed_in_the_background_and_reported()
    {
        var diff = new DiffEditor { OriginalText = "a\nb\nc\nd", ModifiedText = "a\nx\ny\nc\nd\ne", Width = 800, Height = 300 };
        var reports = new List<DiffComputedEventArgs>();
        diff.DiffComputed += (_, e) => reports.Add(e);
        var window = TestHelpers.Show(diff);
        window.Width = 840;
        WaitForDiff(diff, window);

        diff.IsDiffUpToDate.ShouldBeTrue();
        var report = reports.ShouldHaveSingleItem();
        report.ChangeCount.ShouldBe(2);
        report.InsertedLineCount.ShouldBe(3);
        report.RemovedLineCount.ShouldBe(1);
        report.IsIdentical.ShouldBeFalse();
        report.HitTimeout.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Identical_texts_have_no_changes()
    {
        DiffComputedEventArgs? report = null;
        var (diff, _) = Show("same\ntext", "same\ntext", d => d.DiffComputed += (_, e) => report = e);

        diff.Diff.ShouldNotBeNull().Changes.ShouldBeEmpty();
        report.ShouldNotBeNull().IsIdentical.ShouldBeTrue();
        diff.OriginalDecorationIds.ShouldBeEmpty();
        diff.ModifiedDecorationIds.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Changed_lines_and_characters_are_decorated_on_both_sides()
    {
        var (diff, window) = Show("let a = 1;\nkeep\nremoved line", "let b = 1;\nkeep");
        var original = diff.OriginalEditor!;
        var modified = diff.ModifiedEditor!;

        Decorations(original, DiffEditor.LineDeleteClassName).Select(d => d.Range.StartLineNumber).ShouldBe([1, 3]);
        Decorations(modified, DiffEditor.LineInsertClassName).Select(d => d.Range.StartLineNumber).ShouldBe([1]);
        Decorations(original, DiffEditor.CharDeleteClassName).ShouldContain(d => d.Range == new TextRange(1, 5, 1, 6));
        Decorations(modified, DiffEditor.CharInsertClassName).ShouldContain(d => d.Range == new TextRange(1, 5, 1, 6));
        // a removed line as a whole
        Decorations(original, DiffEditor.CharDeleteClassName).ShouldContain(d => d.Options.IsWholeLine && d.Range.StartLineNumber == 3);

        // the view draws them with the theme's brushes, and the + / − signs in the lines decorations column
        var view = TestHelpers.Descendant<CodeEditorView>(original);
        var rects = view.GetDecorationRects(1, 3);
        rects.ShouldContain(r => r.ClassName == DiffEditor.LineDeleteClassName && r.Fill == diff.RemovedLineBrush);
        rects.ShouldContain(r => r.ClassName == DiffEditor.CharDeleteClassName && r.Fill != null);
        original.Gutter.LinesDecorations.IsVisible.ShouldBeTrue();
        original.DecorationStyles.Get(DiffEditor.DeleteSignClassName).ShouldNotBeNull().Glyph.ShouldBe(DiffEditor.MinusGlyph);
        modified.DecorationStyles.Get(DiffEditor.InsertSignClassName).ShouldNotBeNull().Glyph.ShouldBe(DiffEditor.PlusGlyph);
        Render(window);
    }

    [AvaloniaFact]
    public void The_shorter_side_gets_empty_space_so_that_lines_stay_aligned()
    {
        var (diff, window) = Show("a\nb\nc", "a\nnew 1\nnew 2\nb\nc");
        var original = diff.OriginalEditor!;
        var modified = diff.ModifiedEditor!;
        var lineHeight = original.View!.LineHeight;

        var zoneId = diff.OriginalZoneIds.ShouldHaveSingleItem();
        diff.ModifiedZoneIds.ShouldBeEmpty();
        original.GetViewZone(zoneId).ShouldNotBeNull().AfterLineNumber.ShouldBe(1);
        original.GetViewZoneBounds(zoneId).ShouldNotBeNull().Height.ShouldBe(2 * lineHeight, 0.01);
        original.GetViewZone(zoneId)!.Control.ShouldBeOfType<DiffEditorEmptyLinesView>();

        // "b" is at the same height on both sides
        original.GetTopForLineNumber(2).ShouldBe(modified.GetTopForLineNumber(4), 0.01);
        Render(window);
    }

    [AvaloniaFact]
    public void Removed_lines_make_room_in_the_modified_editor()
    {
        var (diff, _) = Show("a\nold 1\nold 2\nold 3\nb", "a\nb");

        var zoneId = diff.ModifiedZoneIds.ShouldHaveSingleItem();
        var modified = diff.ModifiedEditor!;
        modified.GetViewZone(zoneId).ShouldNotBeNull().AfterLineNumber.ShouldBe(1);
        diff.OriginalEditor!.GetTopForLineNumber(5).ShouldBe(modified.GetTopForLineNumber(2), 0.01);
    }

    [AvaloniaFact]
    public void Scrolling_one_side_scrolls_the_other()
    {
        var (diff, window) = Show(Lines(200), Lines(200, i => i == 50 ? "changed" : $"const value{i} = {i};"), height: 300);
        var original = diff.OriginalEditor!;
        var modified = diff.ModifiedEditor!;

        modified.ViewModel!.ViewLayout.SetScrollPosition(new TKCode.Arc.Editor.NewScrollPosition(null, 600), TKCode.Arc.Editor.ViewModel.ScrollType.Immediate);
        Render(window);
        original.ViewModel!.ViewLayout.GetCurrentScrollTop().ShouldBe(600, 0.01);

        original.ViewModel.ViewLayout.SetScrollPosition(new TKCode.Arc.Editor.NewScrollPosition(null, 300), TKCode.Arc.Editor.ViewModel.ScrollType.Immediate);
        Render(window);
        modified.ViewModel.ViewLayout.GetCurrentScrollTop().ShouldBe(300, 0.01);
    }

    [AvaloniaFact]
    public void Inline_the_removed_lines_show_in_the_modified_editor()
    {
        var (diff, window) = Show("a\nold x\nold y\nb", "a\nnew x\nb", d => d.RenderSideBySide = false);
        var modified = diff.ModifiedEditor!;

        diff.OriginalEditor!.IsVisible.ShouldBeFalse();
        diff.MovedBlocksStrip!.IsVisible.ShouldBeFalse();
        modified.Bounds.Width.ShouldBeGreaterThan(700);
        diff.OriginalZoneIds.ShouldBeEmpty();
        var zone = modified.GetViewZone(diff.ModifiedZoneIds.ShouldHaveSingleItem()).ShouldNotBeNull();
        zone.AfterLineNumber.ShouldBe(1);
        zone.HeightInPx.ShouldNotBeNull().ShouldBe(2 * modified.View!.LineHeight, 0.01);
        var deleted = zone.Control.ShouldBeOfType<DiffEditorDeletedLinesView>();
        deleted.Lines.ShouldBe(["old x", "old y"]);
        deleted.GetRemovedColumns(0).ShouldNotBeEmpty();
        zone.MarginControl.ShouldBeOfType<DiffEditorDeletedLinesMargin>().LineCount.ShouldBe(2);

        // and back side by side: the removed lines are in the original editor, empty space in the modified one
        diff.RenderSideBySide = true;
        Render(window);
        diff.OriginalEditor.IsVisible.ShouldBeTrue();
        modified.GetViewZone(diff.ModifiedZoneIds.ShouldHaveSingleItem())!.Control.ShouldBeOfType<DiffEditorEmptyLinesView>();
    }

    [AvaloniaFact]
    public void Removed_columns_of_a_deleted_line_follow_the_inner_changes()
    {
        var change = new DetailedLineRangeMapping(new LineRange(2, 4), new LineRange(2, 2),
        [
            new RangeMapping(new TextRange(2, 3, 2, 5), new TextRange(2, 3, 2, 3)),
            new RangeMapping(new TextRange(2, 8, 3, 2), new TextRange(2, 3, 2, 3)),
        ]);
        var view = new DiffEditorDeletedLinesView(new DiffEditor(), new CodeEditor(), ["abcdefghij", "xyz"], 2, change);

        view.GetRemovedColumns(0).ShouldBe([(3, 5), (8, int.MaxValue)]);
        view.GetRemovedColumns(1).ShouldBe([(1, 2)]);
        DiffEditorDeletedLinesView.ExpandTabs("\ta\tb", 4).VisibleColumns.ShouldBe([0, 4, 5, 8, 9]);
    }

    [AvaloniaFact]
    public void Unchanged_regions_can_be_hidden_and_shown_again()
    {
        var modifiedText = Lines(40, i => i == 20 ? "const value20 = 100;" : $"const value{i} = {i};");
        var (diff, window) = Show(Lines(40), modifiedText, d => d.HideUnchangedRegions = true);
        var original = diff.OriginalEditor!;
        var modified = diff.ModifiedEditor!;

        diff.UnchangedRegions.Select(r => (r.ModifiedLineNumber, r.LineCount)).ShouldBe([(1, 16), (24, 17)]);
        modified.ViewModel!.GetHiddenAreas().Select(r => (r.StartLineNumber, r.EndLineNumber)).ShouldBe([(1, 16), (24, 40)]);
        original.ViewModel!.GetHiddenAreas().Count.ShouldBe(2);
        // a bar for each region on each side
        diff.ModifiedZoneIds.Count.ShouldBe(2);
        diff.OriginalZoneIds.Count.ShouldBe(2);
        var bar = modified.GetViewZone(diff.ModifiedZoneIds[0])!.Control.ShouldBeOfType<DiffEditorCollapsedRegionView>();
        bar.HiddenLineCount.ShouldBe(16);
        bar.Text.ShouldBe("16 hidden lines");

        bar.Region.ShowAll();
        Render(window);
        modified.ViewModel.GetHiddenAreas().Select(r => r.StartLineNumber).ShouldBe([24]);
        diff.ModifiedZoneIds.Count.ShouldBe(1);

        // an opened region stays open when the diff is computed again
        diff.ComputeDiffNow();
        modified.ViewModel.GetHiddenAreas().Select(r => r.StartLineNumber).ShouldBe([24]);

        diff.HideUnchangedRegions = false;
        Render(window);
        modified.ViewModel.GetHiddenAreas().ShouldBeEmpty();
        original.ViewModel.GetHiddenAreas().ShouldBeEmpty();
        diff.ModifiedZoneIds.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Moving_the_cursor_into_a_hidden_region_shows_its_line()
    {
        var (diff, window) = Show(Lines(40), Lines(40, i => i == 20 ? "changed" : $"const value{i} = {i};"), d => d.HideUnchangedRegions = true);
        var modified = diff.ModifiedEditor!;

        modified.SetPosition(new TextPosition(35, 1));
        Render(window);

        modified.ViewModel!.GetHiddenAreas().ShouldAllBe(r => r.StartLineNumber > 35 || r.EndLineNumber < 35);
    }

    [AvaloniaFact]
    public void F7_and_Shift_F7_go_to_the_next_and_previous_change()
    {
        var modifiedText = Lines(30, i => i is 5 or 15 or 25 ? $"changed {i}" : $"const value{i} = {i};");
        var (diff, window) = Show(Lines(30), modifiedText, height: 200);
        var modified = diff.ModifiedEditor!;
        modified.Focus();
        Render(window);

        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        modified.GetPosition().ShouldBe(new TextPosition(5, 1));
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        modified.GetPosition().ShouldBe(new TextPosition(15, 1));
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        modified.GetPosition().ShouldBe(new TextPosition(5, 1)); // wraps around
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.Shift);
        modified.GetPosition().ShouldBe(new TextPosition(25, 1));
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.Shift);
        modified.GetPosition().ShouldBe(new TextPosition(15, 1));

        // the change is scrolled into view
        Render(window);
        var top = modified.GetTopForLineNumber(15) - modified.ViewModel!.ViewLayout.GetCurrentScrollTop();
        top.ShouldBeInRange(0, modified.GetLayoutSize().Height);
    }

    [AvaloniaFact]
    public void Reverting_a_change_puts_the_original_lines_back()
    {
        var (diff, window) = Show("a\nb\nc\nd", "a\nB1\nB2\nc\nd\ne");
        var modified = diff.ModifiedEditor!;
        diff.CanRevert.ShouldBeTrue();
        modified.Gutter.Get(DiffEditor.RevertColumnId).ShouldNotBeNull().IsVisible.ShouldBeTrue();

        var change = diff.GetRevertableChangeAt(2).ShouldNotBeNull();
        diff.Revert(change).ShouldBeTrue();
        modified.Text.ShouldBe("a\nb\nc\nd\ne");
        diff.ModifiedText.ShouldBe("a\nb\nc\nd\ne");
        diff.IsDiffUpToDate.ShouldBeFalse();

        diff.ComputeDiffNow();
        var insertion = diff.GetRevertableChangeAt(5).ShouldNotBeNull();
        diff.Revert(insertion).ShouldBeTrue();
        modified.Text.ShouldBe("a\nb\nc\nd");

        // one undo step each
        modified.Trigger("keyboard", "undo", null);
        modified.Text.ShouldBe("a\nb\nc\nd\ne");
        Render(window);
    }

    [AvaloniaFact]
    public void Removed_lines_get_a_revert_arrow_in_the_modified_margin()
    {
        var (diff, _) = Show("a\nold\nb", "a\nb");
        var modified = diff.ModifiedEditor!;

        var zone = modified.GetViewZone(diff.ModifiedZoneIds.ShouldHaveSingleItem()).ShouldNotBeNull();
        var arrow = zone.MarginControl.ShouldBeOfType<DiffEditorRevertArrowView>();
        diff.Revert(arrow.Change).ShouldBeTrue();
        modified.Text.ShouldBe("a\nold\nb");
    }

    [AvaloniaFact]
    public void Nothing_is_reverted_when_read_only_or_inline()
    {
        var (diff, _) = Show("a\nb", "a\nc", d => d.IsReadOnly = true);
        var modified = diff.ModifiedEditor!;

        diff.CanRevert.ShouldBeFalse();
        modified.Gutter.Get(DiffEditor.RevertColumnId)!.IsVisible.ShouldBeFalse();
        diff.Revert(diff.Diff!.Changes[0]).ShouldBeFalse();
        modified.Text.ShouldBe("a\nc");

        diff.IsReadOnly = false;
        diff.RenderSideBySide = false;
        diff.CanRevert.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Editing_the_modified_text_computes_the_diff_again_after_a_pause()
    {
        var (diff, window) = Show("a\nb\nc", "a\nb\nc");
        var reports = 0;
        diff.DiffComputed += (_, _) => reports++;
        var modified = diff.ModifiedEditor!;
        diff.Diff!.Changes.ShouldBeEmpty();

        modified.ExecuteEdits("test", [new SingleEditOperation(new TextRange(2, 1, 2, 2), "B")]).ShouldBeTrue();
        diff.ModifiedText.ShouldBe("a\nB\nc");
        diff.IsDiffUpToDate.ShouldBeFalse();
        WaitForDiff(diff, window);

        reports.ShouldBe(1);
        diff.Diff!.Changes.ShouldHaveSingleItem().Modified.ShouldBe(new LineRange(2, 3));
    }

    [AvaloniaFact]
    public void Setting_the_texts_or_options_computes_the_diff_again()
    {
        var (diff, window) = Show("a\nb", "a\nb");

        diff.ModifiedText = "a\nb  ";
        WaitForDiff(diff, window);
        diff.Diff!.Changes.ShouldBeEmpty(); // trailing whitespace is ignored by default

        diff.IgnoreTrimWhitespace = false;
        WaitForDiff(diff, window);
        diff.Diff!.Changes.ShouldHaveSingleItem();

        diff.OriginalText = "a\nb  ";
        WaitForDiff(diff, window);
        diff.Diff!.Changes.ShouldBeEmpty();
        diff.OriginalEditor!.Text.ShouldBe("a\nb  ");
    }

    [AvaloniaFact]
    public void Moved_blocks_are_connected_between_the_editors()
    {
        string[] block = ["function moved(a, b) {", "    const sum = a + b;", "    const product = a * b;", "    return sum + product;", "}"];
        var others = Enumerable.Range(0, 12).Select(i => $"const other{i} = compute({i}, \"text {i}\");").ToArray();
        var (diff, window) = Show(string.Join('\n', [.. block, .. others]), string.Join('\n', [.. others, .. block]), height: 500);
        var strip = diff.MovedBlocksStrip!;

        diff.Diff!.Moves.ShouldHaveSingleItem();
        Render(window);
        strip.Bounds.Width.ShouldBe(strip.MovedBlocksWidth, 0.01);
        var (original, modified) = strip.GetConnections().ShouldHaveSingleItem();
        original.Top.ShouldBeLessThan(modified.Top);
        (original.Bottom - original.Top).ShouldBe(5 * diff.OriginalEditor!.View!.LineHeight, 0.01);

        // without moves the strip is the separator line
        diff.ModifiedText = diff.OriginalText;
        diff.ComputeDiffNow();
        Render(window);
        strip.Bounds.Width.ShouldBe(strip.SeparatorWidth, 0.01);
    }

    [AvaloniaFact]
    public void The_overview_ruler_marks_the_changes_and_scrolls_there()
    {
        var modifiedText = Lines(200, i => i == 150 ? "changed" : $"const value{i} = {i};");
        var (diff, window) = Show(Lines(200), modifiedText, height: 300);
        var ruler = diff.OverviewRuler!;

        var marks = ruler.GetMarks();
        marks.Count(m => m.Lane == 0).ShouldBe(1);
        var inserted = marks.Single(m => m.Lane == 1);
        var modified = diff.ModifiedEditor!;
        var scrollHeight = modified.ViewModel!.ViewLayout.GetScrollHeight();
        inserted.Top.ShouldBe(modified.GetTopForLineNumber(150) / scrollHeight * ruler.Bounds.Height, 0.5);
        inserted.Top.ShouldBeGreaterThan(ruler.Bounds.Height / 2);

        ruler.ScrollToPoint(inserted.Top);
        Render(window);
        var top = modified.GetTopForLineNumber(150) - modified.ViewModel!.ViewLayout.GetCurrentScrollTop();
        top.ShouldBeInRange(0, modified.GetLayoutSize().Height);
        diff.OriginalEditor!.ViewModel!.ViewLayout.GetCurrentScrollTop().ShouldBe(modified.ViewModel.ViewLayout.GetCurrentScrollTop(), 0.01);
    }

    [AvaloniaFact]
    public void Leaving_the_visual_tree_and_coming_back_keeps_the_diff()
    {
        var (diff, window) = Show("a\nb", "a\nc");
        window.Content = null;
        Render(window);
        window.Content = diff;
        Render(window);

        diff.ComputeDiffNow();
        diff.Diff!.Changes.ShouldHaveSingleItem();
        diff.ModifiedDecorationIds.ShouldNotBeEmpty();
    }
}

internal static class DiffEditorTestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
