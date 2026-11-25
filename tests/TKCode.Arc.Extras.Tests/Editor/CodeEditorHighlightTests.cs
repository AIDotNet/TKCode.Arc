using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Tests.Editor;

/// <summary>Syntax highlighting in the control (task 4.5): languages by id or name, token colors, the theme.</summary>
public class CodeEditorHighlightTests
{
    private static Avalonia.Controls.Window? s_window;

    private static (CodeEditor Editor, CodeEditorView View) Show(string text, string languageId)
    {
        var editor = new CodeEditor { Text = text, LanguageId = languageId, Width = 500, Height = 200 };
        var window = TestHelpers.Show(editor);
        s_window = window;
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        return (editor, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static ColorId ForegroundAt(CodeEditor editor, int lineNumber, int column)
    {
        editor.Model.Tokenization.ForceTokenization(lineNumber);
        var tokens = editor.Model.Tokenization.GetLineTokens(lineNumber);
        return tokens.GetForeground(tokens.FindTokenIndexAtOffset(column - 1));
    }

    [AvaloniaFact]
    public void A_language_name_resolves_to_its_id_and_its_tokenizer_runs()
    {
        var (editor, _) = Show("using System;\nvar s = \"text\"; // note", "C#");
        editor.Model.GetLanguageId().ShouldBe("csharp");
        editor.Model.Tokenization.ForceTokenization(2);
        var keyword = ForegroundAt(editor, 1, 1);
        var identifier = ForegroundAt(editor, 1, 7);
        var str = ForegroundAt(editor, 2, 10);
        var comment = ForegroundAt(editor, 2, 18);
        new[] { keyword, str, comment }.Distinct().Count().ShouldBe(3);
        keyword.ShouldNotBe(identifier);
    }

    [AvaloniaFact]
    public void Plain_text_has_no_token_colors_and_draws_in_the_foreground()
    {
        var (editor, _) = Show("just words", "plaintext");
        ForegroundAt(editor, 1, 1).ShouldBe(ColorId.DefaultForeground);
        CodeEditorLineColors.From(editor.ViewModel!.GetViewLineData(1).Tokens).ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_visible_lines_are_colored_without_waiting_for_the_background()
    {
        var text = string.Join("\n", Enumerable.Range(0, 2000).Select(i => "int x" + i + " = " + i + "; // c"));
        var (editor, view) = Show(text, "csharp");
        var colors = CodeEditorLineColors.From(view.ViewModel!.GetViewLineData(1).Tokens);
        colors.ShouldNotBeNull();
        colors.Count.ShouldBeGreaterThan(2);
    }

    [AvaloniaFact]
    public void Changing_the_language_retokenizes()
    {
        var (editor, _) = Show("<div class=\"a\">x</div>", "plaintext");
        ForegroundAt(editor, 1, 2).ShouldBe(ColorId.DefaultForeground);
        editor.LanguageId = "html";
        Dispatcher.UIThread.RunJobs();
        editor.Model.GetLanguageId().ShouldBe("html");
        ForegroundAt(editor, 1, 2).ShouldNotBe(ColorId.DefaultForeground);
    }

    [AvaloniaFact]
    public void The_dark_appearance_uses_the_dark_token_theme()
    {
        var (editor, _) = Show("using System;", "csharp");
        var light = editor.LanguageServices.ThemeService.GetColorMap();
        s_window!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        var dark = editor.LanguageServices.ThemeService.GetColorMap();
        dark.ShouldNotBe(light);
        var keyword = (int)ForegroundAt(editor, 1, 1);
        dark[keyword].ShouldNotBe(light.Length > keyword ? light[keyword] : default);
    }
}
