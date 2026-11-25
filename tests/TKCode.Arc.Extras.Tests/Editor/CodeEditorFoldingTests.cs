using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Code folding in the control (upstream's folding.ts FoldingController and actions): regions from the indentation,
/// region markers and providers, hidden lines, the chevrons of the folding column, the placeholder, keybindings, the
/// Folding property and the view state.
/// </summary>
public class CodeEditorFoldingTests
{
    private const string Sample =
        "class A\n" +          // 1  region 1-6
        "  def b\n" +          // 2  region 2-3
        "    x = 1\n" +        // 3
        "  def c\n" +          // 4  region 4-6
        "    y = 2\n" +        // 5
        "    z = 3\n" +        // 6
        "end";                 // 7

    private static (CodeEditor Editor, CodeEditorView View, FoldingController Controller, Window Window) Show(string text = Sample, string? language = null, double height = 300)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = height };
        if (language is not null)
        {
            editor.LanguageId = language;
        }

        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        var controller = FoldingController.Get(editor).ShouldNotBeNull();
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), controller, window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<(int Start, int End, bool Collapsed)> Regions(FoldingController controller)
    {
        var regions = controller.CurrentFoldingModel.ShouldNotBeNull().Regions;
        return [.. Enumerable.Range(0, regions.Length).Select(i => (regions.GetStartLineNumber(i), regions.GetEndLineNumber(i), regions.IsCollapsed(i)))];
    }

    private static void Run(CodeEditor editor, Window window, string actionId, object? args = null)
    {
        var task = editor.GetAction(actionId).ShouldNotBeNull().Run(args);
        Render(window);
        task.IsCompleted.ShouldBeTrue();
    }

    private static Point ToWindow(Window window, CodeEditorView view, Point point) =>
        view.TranslatePoint(point, window)!.Value;

    private static Point ChevronAt(Window window, CodeEditorView view, int viewLine)
    {
        var column = view.GetGutterLayout().Columns.Single(c => c.Column.Id == FoldingController.FoldingColumnId);
        var cell = view.GetGutterCell(column, viewLine);
        return ToWindow(window, view, cell.Center);
    }

    private static KeyModifiers Primary => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private static RawInputModifiers RawPrimary => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [AvaloniaFact]
    public void The_regions_come_from_the_indentation_and_the_folding_column_shows()
    {
        var (editor, view, controller, _) = Show();

        controller.IsEnabled.ShouldBeTrue();
        editor.ContextKeyService.GetContextKeyValue("foldingEnabled").ShouldBe(true);
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, false)]);

        var column = view.GetGutterLayout().Columns.Single(c => c.Column.Id == FoldingController.FoldingColumnId);
        column.Width.ShouldBe(view.FoldingWidth);
        view.FoldingWidth.ShouldBeGreaterThan(0);

        // mouseover: expanded chevrons show only while the pointer is over the gutter
        controller.GetFoldingControlAt(1).ShouldBe(FoldingController.FoldingControlKind.ExpandedOnHover);
        controller.GetFoldingControlAt(3).ShouldBe(FoldingController.FoldingControlKind.None);
        controller.IsFoldingControlVisible(1).ShouldBeFalse();
        controller.SetGutterHovered(true);
        controller.IsFoldingControlVisible(1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Fold_hides_the_lines_of_the_region_and_shows_the_placeholder_and_unfold_shows_them_again()
    {
        var (editor, view, controller, window) = Show();
        editor.SetPosition(new TextPosition(4, 3));
        Run(editor, window, "editor.fold");

        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, true)]);
        controller.HiddenRangeModel!.HiddenRanges.ShouldBe([new TextRange(5, 1, 6, 1)]);
        editor.ViewModel!.GetLineCount().ShouldBe(5);
        editor.ViewModel.GetHiddenAreas().ShouldBe([new TextRange(5, 1, 6, 1)]);

        // the collapsed chevron always shows; the placeholder follows the line's text; the line gets the fold background
        controller.GetFoldingControlAt(4).ShouldBe(FoldingController.FoldingControlKind.Collapsed);
        controller.IsFoldingControlVisible(4).ShouldBeTrue();
        var placeholder = view.GetContentDecorationRects(1, 5).Single();
        placeholder.Text.ShouldBe(FoldingController.PlaceholderText);
        placeholder.ViewLineNumber.ShouldBe(4);
        placeholder.Bounds.X.ShouldBe(view.GetX(4, 8), 0.01);
        placeholder.Foreground.ShouldBe(view.MutedBrush);
        var background = view.GetDecorationRects(1, 5).Single(r => r.ClassName == FoldingDecorationClassNames.FoldedBackground);
        background.ViewLineNumber.ShouldBe(4);
        background.Fill.ShouldBe(view.FoldBackgroundBrush);
        background.Fill.ShouldNotBeNull();

        Run(editor, window, "editor.unfold");
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, false)]);
        editor.ViewModel.GetLineCount().ShouldBe(7);
        view.GetContentDecorationRects(1, 7).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Fold_all_unfold_all_fold_level_and_recursive_actions()
    {
        var (editor, _, controller, window) = Show();

        Run(editor, window, "editor.foldAll");
        Regions(controller).ShouldAllBe(r => r.Collapsed);
        editor.ViewModel!.GetLineCount().ShouldBe(2);

        Run(editor, window, "editor.unfoldAll");
        Regions(controller).ShouldAllBe(r => !r.Collapsed);

        Run(editor, window, "editor.foldLevel2");
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, true), (4, 6, true)]);
        Run(editor, window, "editor.unfoldAll");

        editor.SetPosition(new TextPosition(1, 1));
        Run(editor, window, "editor.foldRecursively");
        Regions(controller).ShouldAllBe(r => r.Collapsed);
        Run(editor, window, "editor.unfoldRecursively");
        Regions(controller).ShouldAllBe(r => !r.Collapsed);

        Run(editor, window, "editor.toggleFold");
        Regions(controller)[0].Collapsed.ShouldBeTrue();
        Run(editor, window, "editor.toggleFold");
        Regions(controller)[0].Collapsed.ShouldBeFalse();

        // the arguments of upstream's fold command: levels, direction and 0-based selection lines
        Run(editor, window, "editor.fold", new FoldingArguments(Levels: 1, Direction: "down", SelectionLines: [3]));
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, true)]);
    }

    [AvaloniaFact]
    public void Monacos_keybindings_fold_and_unfold()
    {
        var (editor, _, controller, window) = Show();
        editor.SetPosition(new TextPosition(2, 3));

        var foldModifiers = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta | RawInputModifiers.Alt : RawInputModifiers.Control | RawInputModifiers.Shift;
        window.KeyPressQwerty(PhysicalKey.BracketLeft, foldModifiers);
        Render(window);
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, true), (4, 6, false)]);

        window.KeyPressQwerty(PhysicalKey.BracketRight, foldModifiers);
        Render(window);
        Regions(controller).ShouldAllBe(r => !r.Collapsed);

        // Ctrl+K Ctrl+0: fold all; Ctrl+K Ctrl+J: unfold all
        window.KeyPressQwerty(PhysicalKey.K, RawPrimary);
        window.KeyPressQwerty(PhysicalKey.Digit0, RawPrimary);
        Render(window);
        Regions(controller).ShouldAllBe(r => r.Collapsed);
        editor.Text.ShouldBe(Sample);

        window.KeyPressQwerty(PhysicalKey.K, RawPrimary);
        window.KeyPressQwerty(PhysicalKey.J, RawPrimary);
        Render(window);
        Regions(controller).ShouldAllBe(r => !r.Collapsed);
    }

    [AvaloniaFact]
    public void A_click_on_a_chevron_toggles_its_region_and_does_not_move_the_cursor()
    {
        var (editor, view, controller, window) = Show();
        editor.SetPosition(new TextPosition(7, 1));

        var chevron = ChevronAt(window, view, 2);
        window.MouseMove(chevron);
        controller.IsGutterHovered.ShouldBeTrue();
        window.MouseDown(chevron, MouseButton.Left);
        window.MouseUp(chevron, MouseButton.Left);
        Render(window);
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, true), (4, 6, false)]);
        editor.GetPosition().ShouldBe(new TextPosition(7, 1));

        window.MouseDown(chevron, MouseButton.Left);
        window.MouseUp(chevron, MouseButton.Left);
        Render(window);
        Regions(controller).ShouldAllBe(r => !r.Collapsed);

        // Shift: recursive (first the children, then the region)
        var outer = ChevronAt(window, view, 1);
        window.MouseDown(outer, MouseButton.Left, RawInputModifiers.Shift);
        window.MouseUp(outer, MouseButton.Left, RawInputModifiers.Shift);
        Render(window);
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, true), (4, 6, true)]);

        // the pointer leaves the gutter: the expanded chevrons hide
        window.MouseMove(ToWindow(window, view, new Point(view.Bounds.Width - 5, 5)));
        controller.IsGutterHovered.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_click_on_the_placeholder_unfolds_the_region()
    {
        var (editor, view, controller, window) = Show();
        editor.SetPosition(new TextPosition(4, 1));
        Run(editor, window, "editor.fold");
        Regions(controller)[2].Collapsed.ShouldBeTrue();

        var placeholder = view.GetContentDecorationRects(1, 5).Single();
        var point = ToWindow(window, view, placeholder.Bounds.Center);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);
        Regions(controller)[2].Collapsed.ShouldBeFalse();
        editor.ViewModel!.GetLineCount().ShouldBe(7);

        // far right of a folded line does nothing (unfoldOnClickAfterEndOfLine is off)
        Run(editor, window, "editor.fold");
        var far = ToWindow(window, view, new Point(view.Bounds.Width - 10, view.GetY(4) + (view.LineHeight / 2)));
        window.MouseDown(far, MouseButton.Left);
        window.MouseUp(far, MouseButton.Left);
        Render(window);
        Regions(controller)[2].Collapsed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_cursor_moved_into_a_folded_region_unfolds_it()
    {
        var (editor, _, controller, window) = Show();
        editor.SetPosition(new TextPosition(2, 1));
        Run(editor, window, "editor.fold");
        Regions(controller)[1].Collapsed.ShouldBeTrue();

        editor.SetPosition(new TextPosition(3, 2));
        controller.FlushCursorChanged();
        Render(window);
        Regions(controller)[1].Collapsed.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Folding_off_removes_the_column_the_regions_and_the_hidden_lines()
    {
        var (editor, view, controller, window) = Show();
        Run(editor, window, "editor.foldAll");
        var gutterWithFolding = view.GutterWidth;

        editor.Folding = false;
        Render(window);
        controller.IsEnabled.ShouldBeFalse();
        controller.CurrentFoldingModel.ShouldBeNull();
        editor.ViewModel!.GetLineCount().ShouldBe(7);
        editor.ViewModel.GetHiddenAreas().ShouldBeEmpty();
        view.GetGutterLayout().Columns.ShouldNotContain(c => c.Column.Id == FoldingController.FoldingColumnId);
        view.GutterWidth.ShouldBe(gutterWithFolding - view.FoldingWidth, 0.01);
        editor.ContextKeyService.GetContextKeyValue("foldingEnabled").ShouldBe(false);
        editor.GetAction("editor.fold").ShouldNotBeNull().IsSupported().ShouldBeFalse();
        view.GetContentDecorationRects(1, 7).ShouldBeEmpty();

        editor.Folding = true;
        Render(window);
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, false)]);
    }

    [AvaloniaFact]
    public void Edits_update_the_regions_after_the_delay()
    {
        var (editor, _, controller, window) = Show();
        editor.SetPosition(new TextPosition(7, 4));
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("\n  w"));
        Render(window);

        // the update waits for its delay; an action runs it at once
        Run(editor, window, "editor.unfoldAll");
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, false), (7, 8, false)]);

        TestHelpers.PumpUntil(() => controller.GetFoldingModel()!.IsCompleted).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Region_markers_and_block_comments_fold_by_their_actions()
    {
        const string text =
            "#region One\n" +     // 1
            "int a;\n" +          // 2
            "#endregion\n" +      // 3
            "/*\n" +              // 4
            "  comment\n" +       // 5
            "*/\n" +              // 6
            "class C\n" +         // 7
            "{\n" +               // 8
            "    int b;\n" +      // 9
            "}";                  // 10
        var (editor, _, controller, window) = Show(text, "csharp");
        Regions(controller).ShouldContain((1, 3, false));

        Run(editor, window, "editor.foldAllMarkerRegions");
        Regions(controller).Single(r => r.Start == 1).Collapsed.ShouldBeTrue();
        Run(editor, window, "editor.unfoldAllMarkerRegions");
        Regions(controller).Single(r => r.Start == 1).Collapsed.ShouldBeFalse();

        Run(editor, window, "editor.foldAllBlockComments");
        Regions(controller).Where(r => r.Collapsed).Select(r => r.Start).ShouldBe([4]);
    }

    [AvaloniaFact]
    public void A_folding_range_provider_takes_over_from_the_indentation()
    {
        var (editor, _, controller, window) = Show();
        using var registration = editor.LanguageFeatures.FoldingRangeProvider.Register("plaintext", new FixedProvider([new FoldingRange(1, 7) { Kind = FoldingRangeKind.Region }]));
        Run(editor, window, "editor.unfoldAll");
        Regions(controller).ShouldBe([(1, 7, false)]);
        controller.CurrentFoldingModel!.Regions.GetType(0).ShouldBe("region");

        controller.FoldingStrategy = FoldingStrategy.Indentation;
        Run(editor, window, "editor.unfoldAll");
        Regions(controller).ShouldBe([(1, 6, false), (2, 3, false), (4, 6, false)]);
    }

    [AvaloniaFact]
    public void Go_to_fold_actions_move_the_cursor()
    {
        var (editor, _, _, window) = Show();
        editor.SetPosition(new TextPosition(5, 5));
        Run(editor, window, "editor.gotoParentFold");
        editor.GetPosition().ShouldBe(new TextPosition(4, 1));
        Run(editor, window, "editor.gotoPreviousFold");
        editor.GetPosition().ShouldBe(new TextPosition(2, 1));
        Run(editor, window, "editor.gotoNextFold");
        editor.GetPosition().ShouldBe(new TextPosition(4, 1));
    }

    [AvaloniaFact]
    public void A_folding_range_from_the_selection_is_manual_and_can_be_removed()
    {
        var (editor, _, controller, window) = Show("a\nb\nc\nd\ne");
        Regions(controller).ShouldBeEmpty();
        editor.SetSelection(new TextSelection(2, 1, 4, 2));
        Run(editor, window, "editor.createFoldingRangeFromSelection");
        Regions(controller).ShouldBe([(2, 4, true)]);
        controller.CurrentFoldingModel!.Regions.GetSource(0).ShouldBe(FoldSource.UserDefined);
        controller.GetFoldingControlAt(2).ShouldBe(FoldingController.FoldingControlKind.Collapsed);
        editor.GetPosition().ShouldBe(new TextPosition(2, 1));

        Run(editor, window, "editor.removeManualFoldingRanges");
        Regions(controller).ShouldBeEmpty();
        editor.ViewModel!.GetLineCount().ShouldBe(5);
    }

    [AvaloniaFact]
    public void The_view_state_keeps_the_folded_regions_and_a_new_view_model_keeps_the_hidden_lines()
    {
        var (editor, _, controller, window) = Show();
        editor.SetPosition(new TextPosition(2, 1));
        Run(editor, window, "editor.fold");
        var state = controller.SaveViewState().ShouldBeOfType<FoldingStateMemento>();
        state.CollapsedRegions.ShouldNotBeNull().Single().ShouldBe(new FoldingLineMemento(2, 3) { IsCollapsed = true, Source = FoldSource.Provider, Checksum = state.CollapsedRegions![0].Checksum });

        Run(editor, window, "editor.unfoldAll");
        controller.RestoreViewState(state);
        Render(window);
        Regions(controller)[1].Collapsed.ShouldBeTrue();

        // a new view model (here: another font size) gets the hidden areas again
        editor.FontSize += 2;
        Render(window);
        editor.ViewModel!.GetHiddenAreas().ShouldBe([new TextRange(3, 1, 3, 1)]);
        editor.ViewModel.GetLineCount().ShouldBe(6);
    }

    private sealed class FixedProvider(IReadOnlyList<FoldingRange> ranges) : IFoldingRangeProvider
    {
        public Task<IReadOnlyList<FoldingRange>?> ProvideFoldingRanges(TextModel model, FoldingContext context, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<FoldingRange>?>(ranges);
    }
}
