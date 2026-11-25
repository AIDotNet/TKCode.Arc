using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Tests.Editor;

/// <summary>Bracket pair colorization in the control (task 4.6): brackets take a color by nesting level.</summary>
public class CodeEditorBracketColorTests
{
    private static (CodeEditor Editor, CodeEditorView View) Show(string text, string languageId)
    {
        var editor = new CodeEditor { Text = text, LanguageId = languageId, Width = 500, Height = 200 };
        var window = TestHelpers.Show(editor);
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        return (editor, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static CodeEditorLineColors LineColors(CodeEditor editor, CodeEditorView view, int lineNumber)
    {
        editor.Model.Tokenization.ForceTokenization(editor.Model.GetLineCount());
        var colors = view.GetLineColors(lineNumber, new TextRange(1, 1, editor.Model.GetLineCount(), editor.Model.GetLineMaxColumn(editor.Model.GetLineCount())));
        colors.ShouldNotBeNull();
        return colors;
    }

    [AvaloniaFact]
    public void Brackets_take_the_color_of_their_nesting_level()
    {
        // offsets:  f0 (1 a2 [3 b4 {5 c6 }7 ]8 )9 ' '10 }11
        var (editor, view) = Show("f(a[b{c}]) }", "csharp");
        view.BracketColorCount.ShouldBe(3); // the light theme shows three level colors
        var colors = LineColors(editor, view, 1);
        var level0 = view.BracketColorBase;
        colors.GetForegroundAt(1).ShouldBe(level0);
        colors.GetForegroundAt(9).ShouldBe(level0);
        colors.GetForegroundAt(3).ShouldBe(level0 + 1);
        colors.GetForegroundAt(8).ShouldBe(level0 + 1);
        colors.GetForegroundAt(5).ShouldBe(level0 + 2);
        colors.GetForegroundAt(7).ShouldBe(level0 + 2);
        colors.GetForegroundAt(11).ShouldBe(level0 + 3); // unexpected closing bracket
        colors.GetForegroundAt(2).ShouldBeLessThan(level0); // 'a' keeps its token color
    }

    [AvaloniaFact]
    public void The_fourth_level_starts_again_with_the_first_color()
    {
        var (editor, view) = Show("(((())))", "csharp");
        var colors = LineColors(editor, view, 1);
        colors.GetForegroundAt(3).ShouldBe(colors.GetForegroundAt(0));
        colors.GetForegroundAt(1).ShouldNotBe(colors.GetForegroundAt(0));
    }

    [AvaloniaFact]
    public void Brackets_in_strings_and_comments_are_not_colored()
    {
        var (editor, view) = Show("x(\"(\" // (\n)", "csharp");
        var colors = LineColors(editor, view, 1);
        colors.GetForegroundAt(1).ShouldBe(view.BracketColorBase);
        colors.GetForegroundAt(3).ShouldBeLessThan(view.BracketColorBase);
        colors.GetForegroundAt(9).ShouldBeLessThan(view.BracketColorBase);
        LineColors(editor, view, 2).GetForegroundAt(0).ShouldBe(view.BracketColorBase);
    }

    [AvaloniaFact]
    public void Turning_colorization_off_draws_brackets_in_their_token_color()
    {
        var (editor, view) = Show("f(a)", "csharp");
        editor.Model.UpdateOptions(new TextModelUpdateOptions { BracketColorizationOptions = new BracketPairColorizationOptions(false, false) });
        Dispatcher.UIThread.RunJobs();
        LineColors(editor, view, 1).GetForegroundAt(1).ShouldBeLessThan(view.BracketColorBase);
    }

    [AvaloniaFact]
    public void The_dark_appearance_uses_the_dark_bracket_colors()
    {
        var (editor, view) = Show("f(a)", "csharp");
        Avalonia.Controls.TopLevel.GetTopLevel(editor)!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        var theme = (TKCode.Arc.Editor.Standalone.StandaloneTheme)editor.LanguageServices.ThemeService.GetColorTheme();
        theme.Type.ShouldBe(TKCode.Arc.Editor.Standalone.ColorScheme.Dark);
        theme.GetColorOrDefault(TKCode.Arc.Editor.Standalone.EditorColorRegistry.EditorBracketHighlightingForeground1)
            .ShouldBe(RgbaColor.FromHex("#FFD700"));
        LineColors(editor, view, 1).GetForegroundAt(1).ShouldBe(view.BracketColorBase);
    }
}

/// <summary>Color overrides over a line's token colors.</summary>
public class CodeEditorLineColorsOverrideTests
{
    private static LineTokens FakeTokens(params (int End, int Color)[] tokens) =>
        new(
            tokens.SelectMany(t => new[] { (uint)t.End, (uint)t.Color << MetadataConsts.ForegroundOffset }).ToArray(),
            new string('x', tokens[^1].End),
            new TKCode.Arc.Editor.Languages.LanguageIdCodec());

    private static int[] Runs(CodeEditorLineColors c) =>
        Enumerable.Range(0, c.Count).SelectMany(i => new[] { c.GetEndOffset(i), c.GetForeground(i) }).ToArray();

    [Fact]
    public void Overrides_split_tokens_and_merge_with_equal_neighbours()
    {
        var tokens = FakeTokens((4, 5), (10, 6));
        var colors = CodeEditorLineColors.From(tokens, [new ColorOverride(1, 2, 9), new ColorOverride(3, 5, 9), new ColorOverride(9, 10, 6)])!;
        Runs(colors).ShouldBe([1, 5, 2, 9, 3, 5, 5, 9, 10, 6]);
    }

    [Fact]
    public void An_override_over_a_whole_default_line_keeps_the_line_colored()
    {
        var colors = CodeEditorLineColors.From(FakeTokens((3, (int)ColorId.DefaultForeground)), [new ColorOverride(0, 1, 9)]);
        colors.ShouldNotBeNull();
        Runs(colors).ShouldBe([1, 9, 3, (int)ColorId.DefaultForeground]);
    }
}
