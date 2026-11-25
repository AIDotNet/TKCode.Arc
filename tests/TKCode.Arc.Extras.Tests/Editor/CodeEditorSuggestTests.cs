using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Code completion in the control (upstream's suggestController.test.ts and the parts of suggestModel.test.ts that need
/// the controller): triggering, the widget and its keys, inserting with insert / replace ranges, additional edits,
/// commit characters, snippets through the plain-text fallback, word-based suggestions and the details pane.
/// </summary>
public class CodeEditorSuggestTests
{
    private sealed class Provider(Func<TextModel, TextPosition, CompletionContext, CompletionList?> provide, IReadOnlyList<string>? triggerCharacters = null) : ICompletionItemProvider
    {
        public int Calls { get; private set; }

        public IReadOnlyList<string>? TriggerCharacters => triggerCharacters;

        public Func<CompletionItem, CompletionItem?>? Resolve { get; init; }

        public bool SupportsResolveCompletionItem => Resolve is not null;

        public Task<CompletionList?> ProvideCompletionItems(TextModel model, TextPosition position, CompletionContext context, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(provide(model, position, context));
        }

        public Task<CompletionItem?> ResolveCompletionItem(CompletionItem item, CancellationToken token) => Task.FromResult(Resolve!(item));
    }

    private static TextRange WordRange(TextModel model, TextPosition position)
    {
        var word = SuggestWords.GetWordUntilPosition(model, position, null);
        return new TextRange(position.LineNumber, word.StartColumn, position.LineNumber, word.EndColumn);
    }

    private static Provider Words(params string[] labels) => new((model, pos, _) => new CompletionList(
        labels.Select(label => new CompletionItem { Label = label, Kind = CompletionItemKind.Property, InsertText = label, Range = WordRange(model, pos) }).ToList()));

    private static (CodeEditor Editor, Window Window, SuggestController Controller) Show(string text, SuggestOptions? options = null, double width = 700, double height = 300)
    {
        var editor = new CodeEditor { Text = text, Width = width, Height = height };
        var window = TestHelpers.Show(editor);
        window.Width = width;
        window.Height = height;
        editor.Focus();
        Render(window);
        editor.OnBeforeInteractionEvent();
        var controller = SuggestController.Get(editor).ShouldNotBeNull();
        controller.Options = options ?? SuggestOptions.Default with { QuickSuggestionsDelay = 0, WordBasedSuggestions = WordBasedSuggestions.Off };
        return (editor, window, controller);
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

    // runs the dispatcher until the condition holds (providers on the thread pool answer later)
    private static void Pump(Window window, Func<bool> until)
    {
        for (var i = 0; i < 200 && !until(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (!until())
            {
                Thread.Sleep(5);
            }
        }
        Render(window);
    }

    private static string[] Labels(SuggestController controller) =>
        controller.Widget.List.Items.Select(i => i.TextLabel).ToArray();

    private static bool Visible(CodeEditor editor) => editor.ContextKeyService.GetContextKeyValue("suggestWidgetVisible") is true;

    private static void CtrlSpace(Window window) => window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.Control);

    [AvaloniaFact]
    public void Ctrl_space_shows_the_suggestions_below_the_word_and_escape_hides_them()
    {
        var (editor, window, controller) = Show("let x = fo");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words("foo", "fooBar", "bar"));
        editor.SetPosition(new TextPosition(1, 11));

        CtrlSpace(window);
        Pump(window, () => Visible(editor));

        Visible(editor).ShouldBeTrue();
        Labels(controller).ShouldBe(["foo", "fooBar"]);
        controller.Widget.GetFocusedItem().ShouldNotBeNull().Index.ShouldBe(0);
        var placement = editor.GetContentWidgetPlacement(CodeEditorSuggestWidget.WidgetId).ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Below);
        placement.Bounds.Left.ShouldBe(editor.View!.GetX(1, 9), 0.5);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Render(window);
        Visible(editor).ShouldBeFalse();
        controller.Widget.IsVisible.ShouldBeFalse();
        editor.Text.ShouldBe("let x = fo");
    }

    [AvaloniaFact]
    public void Typing_filters_the_list_and_enter_inserts_the_focused_item()
    {
        var (editor, window, controller) = Show("");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words("console", "const", "constructor", "context"));

        window.KeyTextInput("c");
        Pump(window, () => Visible(editor));
        Labels(controller).Length.ShouldBe(4);

        window.KeyTextInput("on");
        window.KeyTextInput("st");
        Pump(window, () => Labels(controller).Length == 2);
        Labels(controller).ShouldBe(["const", "constructor"]);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Item.TextLabel.ShouldBe("constructor");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);

        editor.Text.ShouldBe("constructor");
        editor.GetPosition().ShouldBe(new TextPosition(1, 12));
        Visible(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Up_wraps_around_and_page_down_moves_a_page()
    {
        var labels = Enumerable.Range(0, 40).Select(i => $"item{i:D2}").ToArray();
        var (editor, window, controller) = Show("item", height: 420);
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words(labels));
        editor.SetPosition(new TextPosition(1, 5));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));

        controller.Widget.List.VisibleCount.ShouldBe(12);
        controller.Widget.List.Bounds.Height.ShouldBe(12 * controller.Widget.List.RowHeight, 0.5);

        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Index.ShouldBe(39);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Index.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Index.ShouldBe(11);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Index.ShouldBe(23);
        controller.Widget.List.ScrollTop.ShouldBe(12);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        controller.Widget.GetFocusedItem()!.Index.ShouldBe(12);
    }

    [AvaloniaFact]
    public void Tab_inserts_and_shift_enter_replaces_the_rest_of_the_word()
    {
        var (editor, window, controller) = Show("fooxyz");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((_, pos, _) => new CompletionList(
        [
            new CompletionItem
            {
                Label = "foobar",
                InsertText = "foobar",
                Range = new CompletionItemRanges(new TextRange(pos.LineNumber, 1, pos.LineNumber, pos.Column), new TextRange(pos.LineNumber, 1, pos.LineNumber, 7)),
            },
        ])));

        // insert: the text right of the cursor stays
        editor.SetPosition(new TextPosition(1, 4));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Render(window);
        editor.Text.ShouldBe("foobarxyz");

        // replace (the alternative of insert): the rest of the word goes
        editor.Text = "fooxyz";
        Render(window);
        editor.SetPosition(new TextPosition(1, 4));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        Render(window);
        editor.Text.ShouldBe("foobar");
    }

    [AvaloniaFact]
    public void Text_changes_for_completion_CodeAction_are_affected_by_the_completion_39893()
    {
        var (editor, window, controller) = Show("ba; import { foo } from \"./b\"");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((_, pos, _) => new CompletionList(
        [
            new CompletionItem
            {
                Label = "bar",
                Kind = CompletionItemKind.Property,
                InsertText = "bar",
                Range = new TextRange(pos with { Column = pos.Column - 2 }, pos),
                AdditionalTextEdits = [new SingleEditOperation(new TextRange(1, 17, 1, 17), ", bar")],
            },
        ]) { Incomplete = true }));

        editor.SetPosition(new TextPosition(1, 3));
        controller.Model.Trigger(new SuggestTriggerOptions { Auto = false });
        Pump(window, () => Visible(editor));
        var first = controller.Widget.GetFocusedItem().ShouldNotBeNull();
        first.Item.Completion.Label.Label.ShouldBe("bar");

        controller.InsertSuggestion(first, SuggestController.InsertFlags.None);
        Render(window);
        editor.Text.ShouldBe("bar; import { foo, bar } from \"./b\"");
    }

    [AvaloniaFact]
    public void A_commit_character_accepts_the_focused_item_and_is_typed()
    {
        var (editor, window, controller) = Show("");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((model, pos, _) => new CompletionList(
        [
            new CompletionItem { Label = "console", InsertText = "console", Range = WordRange(model, pos), CommitCharacters = ["."] },
        ])));

        window.KeyTextInput("co");
        Pump(window, () => Visible(editor));
        window.KeyTextInput(".");
        Pump(window, () => editor.Text == "console.");
        editor.Text.ShouldBe("console.");
    }

    [AvaloniaFact]
    public void A_snippet_completion_inserts_its_text_and_selects_the_first_placeholder()
    {
        var (editor, window, controller) = Show("    fo");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((model, pos, _) => new CompletionList(
        [
            new CompletionItem
            {
                Label = "for",
                Kind = CompletionItemKind.Snippet,
                InsertText = "for (${1:i}) {\n\t$0\n}",
                InsertTextRules = CompletionItemInsertTextRule.InsertAsSnippet,
                Range = WordRange(model, pos),
            },
        ])));

        editor.SetPosition(new TextPosition(1, 7));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);

        // the snippet controller adjusts the whitespace: the tab follows InsertSpaces (upstream's adjustWhitespace)
        editor.Text.Replace("\r\n", "\n").ShouldBe("    for (i) {\n        \n    }");
        editor.GetSelection().ShouldBe(new TextSelection(1, 10, 1, 11));
    }

    [AvaloniaFact]
    public void Plain_items_are_inserted_as_they_are()
    {
        var (editor, window, controller) = Show("");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((model, pos, _) => new CompletionList(
        [
            new CompletionItem { Label = "price", InsertText = "${price}", Range = WordRange(model, pos) },
        ])));

        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Render(window);
        editor.Text.ShouldBe("${price}");
    }

    [AvaloniaFact]
    public void Words_of_the_document_are_suggested_when_no_provider_answers()
    {
        var (editor, window, controller) = Show("alpha beta gamma\nal", SuggestOptions.Default with { QuickSuggestionsDelay = 0 });
        editor.SetPosition(new TextPosition(2, 3));

        CtrlSpace(window);
        Pump(window, () => Visible(editor) && controller.Widget.List.Items.Count > 0);
        Labels(controller).ShouldBe(["alpha"]);
        controller.Widget.List.Items[0].Completion.Kind.ShouldBe(CompletionItemKind.Text);
    }

    [AvaloniaFact]
    public void Quick_suggestions_show_while_typing_a_word()
    {
        var (editor, window, controller) = Show("");
        var provider = Words("hello", "help");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", provider);

        window.KeyTextInput("he");
        Pump(window, () => Visible(editor));
        Visible(editor).ShouldBeTrue();
        Labels(controller).ShouldBe(["hello", "help"]);

        // moving the cursor away cancels
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        Render(window);
        Visible(editor).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_trigger_character_asks_its_provider()
    {
        var (editor, window, controller) = Show("");
        var provider = new Provider((_, pos, ctx) => ctx.TriggerKind == CompletionTriggerKind.TriggerCharacter
            ? new CompletionList([new CompletionItem { Label = "length", InsertText = "length", Range = new TextRange(pos) }])
            : null, ["."]);
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", provider);

        window.KeyTextInput("s.");
        Pump(window, () => Visible(editor));
        Labels(controller).ShouldBe(["length"]);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);
        editor.Text.ShouldBe("s.length");
    }

    [AvaloniaFact]
    public void Ctrl_space_toggles_the_details_of_the_focused_item()
    {
        var (editor, window, controller) = Show("pri");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((model, pos, _) => new CompletionList(
        [
            new CompletionItem
            {
                Label = "print",
                Kind = CompletionItemKind.Function,
                Detail = "print(value: string): void",
                InsertText = "print",
                Range = WordRange(model, pos),
            },
        ]))
        {
            Resolve = item => item with { Documentation = new MarkdownString("Writes **value** to the output.\n\n```\nprint(\"hi\")\n```") },
        });

        editor.SetPosition(new TextPosition(1, 4));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        controller.Widget.IsDetailsVisible.ShouldBeFalse();

        CtrlSpace(window);
        Render(window);
        controller.Widget.IsDetailsVisible.ShouldBeTrue();
        editor.ContextKeyService.GetContextKeyValue("suggestWidgetDetailsVisible").ShouldBe(true);
        var details = TestHelpers.Descendant<Border>(controller.Widget, CodeEditorSuggestWidget.DetailsPart);
        details.IsVisible.ShouldBeTrue();
        var texts = details.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.ShouldContain("print");
        texts.ShouldContain("print(value: string): void");
        texts.ShouldContain("Writes value to the output.");
        texts.ShouldContain("print(\"hi\")");

        CtrlSpace(window);
        Render(window);
        controller.Widget.IsDetailsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Clicking_a_row_inserts_it()
    {
        var (editor, window, controller) = Show("b");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words("bar", "baz", "bat"));
        editor.SetPosition(new TextPosition(1, 2));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));

        var list = controller.Widget.List;
        Labels(controller).ShouldBe(["bar", "bat", "baz"]);
        var point = list.TranslatePoint(new Point(40, list.RowHeight * 1.5), window).ShouldNotBeNull();
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);

        editor.Text.ShouldBe("bat");
    }

    [AvaloniaFact]
    public void A_manual_request_without_results_says_so()
    {
        var (editor, window, controller) = Show("zzz");
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words("foo"));
        editor.SetPosition(new TextPosition(1, 4));

        CtrlSpace(window);
        Pump(window, () => controller.Widget.IsMessageState);
        controller.Widget.Message.ShouldBe(CodeEditorSuggestWidget.NoSuggestionsMessage);
        Visible(editor).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Recently_used_items_are_selected_first()
    {
        var (editor, window, controller) = Show("");
        controller.Options = controller.Options with { SuggestSelection = SuggestSelection.RecentlyUsed };
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words("apple", "apricot"));

        window.KeyTextInput("a");
        Pump(window, () => Visible(editor));
        controller.Widget.GetFocusedItem()!.Item.TextLabel.ShouldBe("apple");
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);
        editor.Text.ShouldBe("apricot");

        window.KeyTextInput(" a");
        Pump(window, () => Visible(editor));
        controller.Widget.GetFocusedItem()!.Item.TextLabel.ShouldBe("apricot");
    }

    [AvaloniaFact]
    public void A_narrow_editor_puts_the_details_below_the_list()
    {
        var (editor, window, controller) = Show("pri", width: 500);
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", new Provider((model, pos, _) => new CompletionList(
        [
            new CompletionItem { Label = "print", Detail = "print(value)", Documentation = "Writes a value.", InsertText = "print", Range = WordRange(model, pos) },
        ])));
        editor.SetPosition(new TextPosition(1, 4));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        CtrlSpace(window);
        Render(window);

        controller.Widget.Classes.Contains(CodeEditorSuggestWidget.DetailsBelowPseudoClass).ShouldBeTrue();
        var details = TestHelpers.Descendant<Border>(controller.Widget, CodeEditorSuggestWidget.DetailsPart);
        details.Bounds.Top.ShouldBeGreaterThan(controller.Widget.List.Bounds.Bottom - 1);
    }

    [AvaloniaFact]
    public void The_list_fits_the_room_above_or_below_the_line()
    {
        var labels = Enumerable.Range(0, 40).Select(i => $"item{i:D2}").ToArray();
        var text = string.Join('\n', Enumerable.Range(1, 12).Select(i => "line")) + "\nitem";
        var (editor, window, controller) = Show(text, height: 260);
        using var registration = editor.LanguageFeatures.CompletionProvider.Register("*", Words(labels));

        // the last line is near the bottom: above, with as many rows as fit there
        editor.SetPosition(new TextPosition(13, 5));
        CtrlSpace(window);
        Pump(window, () => Visible(editor));
        var placement = editor.GetContentWidgetPlacement(CodeEditorSuggestWidget.WidgetId).ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Above);
        placement.Bounds.Top.ShouldBeGreaterThanOrEqualTo(0);
        controller.Widget.List.VisibleCount.ShouldBeLessThan(12);
        controller.Widget.List.VisibleCount.ShouldBeGreaterThan(3);
    }
}
