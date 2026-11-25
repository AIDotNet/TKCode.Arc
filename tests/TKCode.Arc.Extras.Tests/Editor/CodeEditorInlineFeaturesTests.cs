using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.InlayHints;
using TKCode.Arc.Editor.Contrib.SemanticTokens;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Inlay hints, inline completions (ghost text) and semantic tokens in the control (upstream's inlayHintsController,
/// inlineCompletionsController with ghostTextView, documentSemanticTokens / viewportSemanticTokens).
/// </summary>
public class CodeEditorInlineFeaturesTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(
        string text,
        IReadOnlyList<EditorContributionDescription> contributions,
        IReadOnlyList<EditorCommand>? commands = null,
        Action<CodeEditor>? before = null)
    {
        var editor = new CodeEditor(contributions, commands ?? []) { Text = text, Width = 400, Height = 200 };
        before?.Invoke(editor);
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static TKCode.Arc.Editor.Tokens.FontStyle FontStyleAt(CodeEditorLineColors colors, int offset)
    {
        for (var i = 0; i < colors.Count; i++)
        {
            if (offset < colors.GetEndOffset(i))
            {
                return colors.GetFontStyle(i);
            }
        }

        return TKCode.Arc.Editor.Tokens.FontStyle.None;
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    // ---- inlay hints

    private sealed class HintsProvider(Func<TextModel, TextRange, IReadOnlyList<InlayHint>> hints) : IInlayHintsProvider
    {
        public int Calls { get; private set; }

        public Task<InlayHintList?> ProvideInlayHints(TextModel model, TextRange range, CancellationToken token)
        {
            Calls++;
            return Task.FromResult<InlayHintList?>(new InlayHintList(hints(model, range)));
        }
    }

    [AvaloniaFact]
    public async Task Inlay_hints_are_injected_with_their_colors_and_background()
    {
        var (editor, view, window) = Show("let value = compute(1);", [InlayHintsController.Description], before: e =>
            e.LanguageFeatures.InlayHintsProvider.Register("*", new HintsProvider((_, _) =>
            [
                new InlayHint(new TextPosition(1, 10)) { Label = ": number", Kind = InlayHintKind.Type },
                new InlayHint(new TextPosition(1, 21)) { Label = "x:", Kind = InlayHintKind.Parameter, PaddingRight = true, Tooltip = "the x" },
            ])));
        var controller = InlayHintsController.Get(editor).ShouldNotBeNull();
        await controller.RefreshNow();
        Render(window);

        editor.ViewModel!.GetLineContent(1).ShouldBe("let value: number = compute(x:\u00a01);");
        controller.Decorations.Count.ShouldBe(3);

        // the background behind each hint (the padding has none), in the inlay hint brush
        var rects = view.GetDecorationRects(1, 1).Where(r => r.Kind == CodeEditorView.DecorationDrawKind.Inline).ToList();
        rects.Count.ShouldBe(2);
        rects.All(r => ReferenceEquals(r.Fill, view.InlayHintBackgroundBrush)).ShouldBeTrue();
        rects[0].Bounds.Left.ShouldBe(view.GetX(1, 10), 0.01);
        rects[0].Bounds.Right.ShouldBe(view.GetX(1, 18), 0.01);

        // the hint's text in the inlay hint foreground
        var colors = view.GetLineColors(1, new TextRange(1, 1, 1, 40)).ShouldNotBeNull();
        view.TextCache!.GetBrush(colors.GetForegroundAt(11), Brushes.Black).ShouldBe(view.InlayHintForegroundBrush);

        // the cursor steps over a hint as over one character boundary
        editor.SetPosition(new TextPosition(1, 10));
        editor.GetPosition().ShouldBe(new TextPosition(1, 10));
    }

    [AvaloniaFact]
    public async Task Inlay_hints_hide_while_ctrl_alt_is_held_with_onUnlessPressed_and_show_a_tooltip()
    {
        var (editor, view, window) = Show("call(1);", [InlayHintsController.Description], before: e =>
            e.LanguageFeatures.InlayHintsProvider.Register("*", new HintsProvider((_, _) =>
            [
                new InlayHint(new TextPosition(1, 6)) { Label = "x:", Tooltip = "the x parameter" },
            ])));
        var controller = InlayHintsController.Get(editor).ShouldNotBeNull();
        await controller.RefreshNow();
        editor.ViewModel!.GetLineContent(1).ShouldBe("call(x:1);");

        controller.Enabled = InlayHintsEnabled.OnUnlessPressed;
        await controller.RefreshNow();
        editor.ViewModel!.GetLineContent(1).ShouldBe("call(x:1);");
        controller.SetModifierPressed(true);
        editor.ViewModel!.GetLineContent(1).ShouldBe("call(1);");
        controller.SetModifierPressed(false);
        editor.ViewModel!.GetLineContent(1).ShouldBe("call(x:1);");

        // the label part under the pointer, and its tooltip above it
        Render(window);
        var point = new Point((view.GetX(1, 6) + view.GetX(1, 7)) / 2, view.GetY(1) + (view.LineHeight / 2));
        var part = controller.GetLabelPartAt(point).ShouldNotBeNull();
        part.Part.Label.ShouldBe("x:");
        controller.GetLabelPartAt(new Point(view.GetX(1, 2), point.Y)).ShouldBeNull();

        controller.SetActivePart(part);
        Render(window);
        var placement = editor.GetContentWidgetPlacement(controller.Tooltip.Id).ShouldNotBeNull();
        ((Tooltip)controller.Tooltip.Control).Content.ShouldBe("the x parameter");
        placement.Bounds.Width.ShouldBeGreaterThan(0);

        controller.Enabled = InlayHintsEnabled.Off;
        editor.ViewModel!.GetLineContent(1).ShouldBe("call(1);");
    }

    // ---- inline completions

    private sealed class CompletionsProvider : IInlineCompletionsProvider
    {
        public List<InlineCompletion> Items { get; set; } = new();

        public int Calls { get; private set; }

        public Task<InlineCompletions?> ProvideInlineCompletions(TextModel model, TextPosition position, InlineCompletionContext context, CancellationToken token)
        {
            Calls++;
            return Task.FromResult<InlineCompletions?>(new InlineCompletions(Items));
        }
    }

    private static (CodeEditor Editor, CodeEditorView View, Window Window, CompletionsProvider Provider) ShowWithCompletions(string text)
    {
        var provider = new CompletionsProvider();
        var (editor, view, window) = Show(text, [InlineCompletionsController.Description], CodeEditorContributions.EditorCommands, e =>
            e.LanguageFeatures.InlineCompletionsProvider.Register("*", provider));
        TestHelpers.PumpUntil(() => InlineCompletionsController.Get(editor) is not null, 2000);
        return (editor, view, window, provider);
    }

    [AvaloniaFact]
    public async Task Ghost_text_is_shown_after_typing_and_Tab_accepts_it()
    {
        var (editor, view, window, provider) = ShowWithCompletions("");
        var controller = InlineCompletionsController.Get(editor).ShouldNotBeNull();
        provider.Items = [new InlineCompletion { InsertText = "foobar", Range = new TextRange(1, 1, 1, 4) }];
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("foo"));
        await controller.Model.TriggerExplicitly();
        Render(window);

        editor.ViewModel!.GetLineContent(1).ShouldBe("foobar");
        editor.Model.GetValue().ShouldBe("foo");
        (editor.ContextKeyService.GetContextKeyValue("inlineSuggestionVisible") is true).ShouldBeTrue();

        // muted and italic
        var colors = view.GetLineColors(1, new TextRange(1, 1, 1, 7)).ShouldNotBeNull();
        view.TextCache!.GetBrush(colors.GetForegroundAt(4), Brushes.Black).ShouldBe(view.MutedBrush);
        FontStyleAt(colors, 4).ShouldBe(TKCode.Arc.Editor.Tokens.FontStyle.Italic);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        editor.Model.GetValue().ShouldBe("foobar");
        editor.GetPosition().ShouldBe(new TextPosition(1, 7));
        (editor.ContextKeyService.GetContextKeyValue("inlineSuggestionVisible") is true).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task More_lines_of_ghost_text_open_a_view_zone_and_Escape_hides_it()
    {
        var (editor, view, window, provider) = ShowWithCompletions("if (a) {}\nnext");
        var controller = InlineCompletionsController.Get(editor).ShouldNotBeNull();
        editor.SetPosition(new TextPosition(1, 9));
        provider.Items = [new InlineCompletion { InsertText = "{\n\tb();\n\tc();\n}", Range = new TextRange(1, 8, 1, 10) }];
        await controller.Model.TriggerExplicitly();
        Render(window);

        // "{" stays, "\n\tb();\n\tc();\n" opens two lines below, then the hidden "}" ends the last one
        controller.View.LineControls.Count.ShouldBe(1);
        controller.View.LineControls[0].Lines.ShouldBe(["\tb();", "\tc();", ""]);
        controller.View.LineControls[0].OriginalSuffix.ShouldBe("}");
        var zone = editor.GetViewZoneBounds(controller.View.ZoneIds[0]).ShouldNotBeNull();
        zone.Height.ShouldBe(view.LineHeight * 3, 0.01);
        view.GetY(2).ShouldBe(view.GetY(1) + (view.LineHeight * 4), 0.01);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        controller.Model.PrimaryGhostText.ShouldBeNull();
        controller.View.ZoneIds.ShouldBeEmpty();
        view.GetY(2).ShouldBe(view.GetY(1) + view.LineHeight, 0.01);
    }

    [AvaloniaFact]
    public async Task Ctrl_right_accepts_the_next_word_and_alt_brackets_cycle()
    {
        var (editor, _, window, provider) = ShowWithCompletions("");
        var controller = InlineCompletionsController.Get(editor).ShouldNotBeNull();
        controller.Model.Enabled = false;
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("var"));
        provider.Items =
        [
            new InlineCompletion { InsertText = "var first = 1;", Range = new TextRange(1, 1, 1, 4) },
            new InlineCompletion { InsertText = "var second = 2;", Range = new TextRange(1, 1, 1, 4) },
        ];
        await controller.Model.TriggerExplicitly();
        Render(window);
        editor.ViewModel!.GetLineContent(1).ShouldBe("var first = 1;");

        window.KeyPressQwerty(PhysicalKey.BracketRight, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        editor.ViewModel!.GetLineContent(1).ShouldBe("var second = 2;");
        window.KeyPressQwerty(PhysicalKey.BracketLeft, RawInputModifiers.Alt);
        Dispatcher.UIThread.RunJobs();
        editor.ViewModel!.GetLineContent(1).ShouldBe("var first = 1;");

        var modifier = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.ArrowRight, modifier);
        Dispatcher.UIThread.RunJobs();
        editor.Model.GetValue().ShouldBe("var ");
        editor.ViewModel!.GetLineContent(1).ShouldBe("var first = 1;");
    }

    // ---- semantic tokens

    private sealed class SemanticProvider : IDocumentSemanticTokensProvider
    {
        public int Calls { get; private set; }

        public SemanticTokensLegend GetLegend() => new(["variable", "function"], ["readonly"]);

        public Task<SemanticTokensOrEdits?> ProvideDocumentSemanticTokens(TextModel model, string? lastResultId, CancellationToken token)
        {
            Calls++;
            // "value" (0..5) is a readonly variable, "go" (8..10) a function
            return Task.FromResult<SemanticTokensOrEdits?>(new SemanticTokens([0, 0, 5, 0, 1, 0, 8, 2, 1, 0]));
        }

        public void ReleaseDocumentSemanticTokens(string? resultId)
        {
        }
    }

    [AvaloniaFact]
    public void Semantic_tokens_color_the_text_over_the_syntax_colors_when_enabled()
    {
        var provider = new SemanticProvider();
        var (editor, view, window) = Show("value = go();", [SemanticTokensContribution.Description], before: e =>
            e.LanguageFeatures.DocumentSemanticTokensProvider.Register("*", provider));

        // the editor's token theme ("arc", over vs) with rules for the semantic token types
        editor.LanguageServices.ThemeService.DefineTheme("arc", new TKCode.Arc.Editor.Standalone.StandaloneThemeData(
            "vs", true,
            [
                new TokenThemeRule("variable.readonly") { Foreground = "aa0000" },
                new TokenThemeRule("function") { Foreground = "00aa00", FontStyle = "bold" },
            ],
            new Dictionary<string, string>()));
        var contribution = SemanticTokensContribution.Get(editor).ShouldNotBeNull();

        // the standalone theme does not ask for semantic highlighting: nothing is requested
        TestHelpers.PumpUntil(() => provider.Calls > 0, 500).ShouldBeFalse();

        contribution.Setting = EditorSemanticHighlighting.Enabled;
        TestHelpers.PumpUntil(() => editor.Model.Tokenization.HasCompleteSemanticTokens(), 3000).ShouldBeTrue();
        Render(window);

        var colors = view.GetLineColors(1, new TextRange(1, 1, 1, 14)).ShouldNotBeNull();
        var fallback = Brushes.Black;
        view.TextCache!.GetBrush(colors.GetForegroundAt(1), fallback).ShouldBeAssignableTo<ISolidColorBrush>()!.Color.ShouldBe(Color.Parse("#aa0000"));
        view.TextCache.GetBrush(colors.GetForegroundAt(9), fallback).ShouldBeAssignableTo<ISolidColorBrush>()!.Color.ShouldBe(Color.Parse("#00aa00"));
        FontStyleAt(colors, 9).ShouldBe(TKCode.Arc.Editor.Tokens.FontStyle.Bold);

        // off again: the semantic tokens go
        contribution.Setting = EditorSemanticHighlighting.Disabled;
        editor.Model.Tokenization.HasSomeSemanticTokens().ShouldBeFalse();
    }
}
