using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Contrib.WordHighlighter;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The multi-cursor features in the control (phase 5): the multicursor actions with their keybindings, the selection
/// highlighter, occurrence highlighting (wordHighlighter) and the clipboard with its metadata, headless.
/// </summary>
public class CodeEditorMultiCursorTests
{
    private static readonly RawInputModifiers CtrlCmd = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static (CodeEditor Editor, Avalonia.Controls.Window Window, CodeEditorView View) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = 240 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<CodeEditorView>(editor));
    }

    private static string[] Selections(CodeEditor editor) => editor.GetSelections().Select(static s => s.ToString()).ToArray();

    private static string[] DecorationRanges(CodeEditor editor, string className) =>
        editor.Model.GetAllDecorations()
            .Where(d => d.Options.ClassName == className)
            .Select(static d => d.Range)
            .OrderBy(static r => r, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
            .Select(static r => r.ToString())
            .ToArray();

    // ---- multicursor actions

    [AvaloniaFact]
    public void Ctrl_D_Selects_The_Word_Then_Adds_The_Next_Occurrences()
    {
        var (editor, window, _) = Show("foo bar\nfoo baz foo\nfood");
        editor.SetPosition(new TextPosition(1, 2));

        window.KeyPressQwerty(PhysicalKey.D, CtrlCmd);
        Selections(editor).ShouldBe([new TextSelection(1, 1, 1, 4).ToString()]);

        window.KeyPressQwerty(PhysicalKey.D, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.D, CtrlCmd);
        Selections(editor).ShouldBe(
        [
            new TextSelection(1, 1, 1, 4).ToString(),
            new TextSelection(2, 1, 2, 4).ToString(),
            new TextSelection(2, 9, 2, 12).ToString(),
        ]);

        // whole word and case sensitive from a collapsed cursor: 'food' is not a match, the search wraps
        window.KeyPressQwerty(PhysicalKey.D, CtrlCmd);
        editor.GetSelections().Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Ctrl_K_Ctrl_D_Moves_The_Last_Selection_To_The_Next_Match()
    {
        var (editor, window, _) = Show("abc x abc y abc");
        editor.SetSelection(new TextSelection(1, 1, 1, 4));

        window.KeyPressQwerty(PhysicalKey.K, CtrlCmd);
        window.KeyPressQwerty(PhysicalKey.D, CtrlCmd);
        Selections(editor).ShouldBe([new TextSelection(1, 7, 1, 10).ToString()]);
    }

    [AvaloniaFact]
    public void Insert_Cursor_Below_And_Above_Use_The_Platform_Keybindings()
    {
        var (editor, window, _) = Show("one\ntwo\nthree");
        editor.SetPosition(new TextPosition(2, 2));
        var modifiers = OperatingSystem.IsLinux() ? RawInputModifiers.Shift | RawInputModifiers.Alt : CtrlCmd | RawInputModifiers.Alt;

        window.KeyPressQwerty(PhysicalKey.ArrowDown, modifiers);
        Selections(editor).ShouldBe([new TextSelection(2, 2, 2, 2).ToString(), new TextSelection(3, 2, 3, 2).ToString()]);

        window.KeyPressQwerty(PhysicalKey.ArrowUp, modifiers);
        editor.GetSelections().Select(static s => s.PositionLineNumber).OrderBy(static l => l).ShouldBe([1, 2, 3]);
    }

    [AvaloniaFact]
    public void Shift_Alt_I_Adds_Cursors_To_The_Line_Ends_Of_The_Selection()
    {
        var (editor, window, _) = Show("alpha\nbeta\ngamma");
        editor.SetSelection(new TextSelection(1, 2, 3, 3));

        window.KeyPressQwerty(PhysicalKey.I, RawInputModifiers.Shift | RawInputModifiers.Alt);
        Selections(editor).ShouldBe(
        [
            new TextSelection(1, 6, 1, 6).ToString(),
            new TextSelection(2, 5, 2, 5).ToString(),
            new TextSelection(3, 3, 3, 3).ToString(),
        ]);
    }

    [AvaloniaFact]
    public void Ctrl_Shift_L_And_Ctrl_F2_Select_All_Occurrences_And_Typing_Changes_Them_All()
    {
        var (editor, window, _) = Show("let x = 1;\nx = x + 1;");
        editor.SetPosition(new TextPosition(2, 1));

        window.KeyPressQwerty(PhysicalKey.L, CtrlCmd | RawInputModifiers.Shift);
        editor.GetSelections().Count.ShouldBe(3);
        editor.GetSelection()!.Value.ToString().ShouldBe(new TextSelection(2, 1, 2, 2).ToString()); // the primary stays where it was

        window.KeyTextInput("y");
        editor.Text.ShouldBe("let y = 1;\ny = y + 1;");

        editor.SetPosition(new TextPosition(1, 5));
        window.KeyPressQwerty(PhysicalKey.F2, CtrlCmd);
        editor.GetSelections().Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Actions_Are_Listed_With_Upstream_Ids()
    {
        var (editor, _, _) = Show("x");
        string[] ids =
        [
            MultiCursorActions.InsertCursorAboveId, MultiCursorActions.InsertCursorBelowId, MultiCursorActions.InsertCursorAtEndOfEachLineSelectedId,
            MultiCursorActions.AddSelectionToNextFindMatchId, MultiCursorActions.AddSelectionToPreviousFindMatchId,
            MultiCursorActions.MoveSelectionToNextFindMatchId, MultiCursorActions.MoveSelectionToPreviousFindMatchId,
            MultiCursorActions.SelectHighlightsId, MultiCursorActions.ChangeAllId, MultiCursorActions.AddCursorsToBottomId,
            MultiCursorActions.AddCursorsToTopId, MultiCursorActions.FocusNextCursorId, MultiCursorActions.FocusPreviousCursorId,
            "editor.action.wordHighlight.next", "editor.action.wordHighlight.prev", "editor.action.wordHighlight.trigger",
        ];
        foreach (var id in ids)
        {
            editor.GetAction(id).ShouldNotBeNull(id);
        }
    }

    [AvaloniaFact]
    public void Add_Cursors_To_Bottom_And_Focus_Next_Cursor_Run_As_Actions()
    {
        var (editor, _, _) = Show("aa\nbb\ncc");
        editor.SetPosition(new TextPosition(2, 2));

        editor.Trigger("keyboard", MultiCursorActions.AddCursorsToBottomId, null);
        Selections(editor).ShouldBe([new TextSelection(2, 2, 2, 2).ToString(), new TextSelection(3, 2, 3, 2).ToString()]);

        editor.Trigger("keyboard", MultiCursorActions.FocusNextCursorId, null);
        editor.GetSelection()!.Value.PositionLineNumber.ShouldBe(3);
    }

    // ---- selection highlighter

    [AvaloniaFact]
    public void Selecting_Text_Highlights_Its_Other_Occurrences()
    {
        var (editor, _, view) = Show("item one\nitem two\nan item");
        TestHelpers.PumpUntil(() => SelectionHighlighterContribution.Get(editor) is not null).ShouldBeTrue();

        editor.SetSelection(new TextSelection(1, 1, 1, 5));
        TestHelpers.PumpUntil(() => DecorationRanges(editor, HighlightDecorations.SelectionHighlightClassName).Length == 2).ShouldBeTrue();
        DecorationRanges(editor, HighlightDecorations.SelectionHighlightClassName).ShouldBe(
            [new TextRange(2, 1, 2, 5).ToString(), new TextRange(3, 4, 3, 8).ToString()]);

        // the view draws them with the theme's selection highlight brush
        var rects = view.GetDecorationRects(1, 3).Where(r => r.ClassName == HighlightDecorations.SelectionHighlightClassName).ToList();
        rects.Count.ShouldBe(2);
        rects.ShouldAllBe(r => r.Fill != null);

        // an empty selection clears them
        editor.SetPosition(new TextPosition(1, 1));
        TestHelpers.PumpUntil(() => DecorationRanges(editor, HighlightDecorations.SelectionHighlightClassName).Length == 0).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Selection_Highlights_Can_Be_Turned_Off()
    {
        var (editor, _, _) = Show("ab ab ab");
        var controller = MultiCursorContribution.Get(editor).ShouldNotBeNull();
        controller.Options = controller.Options with { SelectionHighlight = false };

        editor.SetSelection(new TextSelection(1, 1, 1, 3));
        TestHelpers.PumpUntil(() => false, 450);
        DecorationRanges(editor, HighlightDecorations.SelectionHighlightClassName).ShouldBeEmpty();
    }

    // ---- occurrence highlighting

    [AvaloniaFact]
    public void Moving_The_Cursor_Into_A_Word_Highlights_Its_Occurrences_And_F7_Moves_Between_Them()
    {
        var (editor, window, _) = Show("count = count + 1\nprint(count)");
        var highlighter = WordHighlighterContribution.Get(editor).ShouldNotBeNull();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        TestHelpers.PumpUntil(() => highlighter.HasDecorations()).ShouldBeTrue();
        DecorationRanges(editor, HighlightDecorations.WordHighlightTextClassName).ShouldBe(
        [
            new TextRange(1, 1, 1, 6).ToString(),
            new TextRange(1, 9, 1, 14).ToString(),
            new TextRange(2, 7, 2, 12).ToString(),
        ]);
        editor.ContextKeyService.GetContextKeyValue("hasWordHighlights").ShouldBe(true);

        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        editor.GetPosition().ShouldBe(new TextPosition(1, 9));
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.None);
        editor.GetPosition().ShouldBe(new TextPosition(2, 7));
        window.KeyPressQwerty(PhysicalKey.F7, RawInputModifiers.Shift);
        editor.GetPosition().ShouldBe(new TextPosition(1, 9));

        // typing removes them
        window.KeyTextInput("x");
        highlighter.HasDecorations().ShouldBeFalse();
        editor.ContextKeyService.GetContextKeyValue("hasWordHighlights").ShouldBe(false);
    }

    private sealed class WriteHighlights : IDocumentHighlightProvider
    {
        public Task<IReadOnlyList<DocumentHighlight>?> ProvideDocumentHighlights(TextModel model, TextPosition position, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<DocumentHighlight>?>([new DocumentHighlight(new TextRange(1, 1, 1, 2)) { Kind = DocumentHighlightKind.Write }]);
    }

    [AvaloniaFact]
    public void A_Document_Highlight_Provider_Wins_Over_The_Textual_Fallback()
    {
        var (editor, window, _) = Show("a b a");
        using var registration = editor.LanguageFeatures.DocumentHighlightProvider.Register(LanguageIds.PlaintextLanguageId, new WriteHighlights());
        var highlighter = WordHighlighterContribution.Get(editor).ShouldNotBeNull();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        TestHelpers.PumpUntil(() => highlighter.HasDecorations()).ShouldBeTrue();
        DecorationRanges(editor, HighlightDecorations.WordHighlightStrongClassName).ShouldBe([new TextRange(1, 1, 1, 2).ToString()]);
        DecorationRanges(editor, HighlightDecorations.WordHighlightTextClassName).ShouldBeEmpty();
    }

    // ---- clipboard

    [AvaloniaFact]
    public void Copy_Of_An_Empty_Selection_Copies_The_Line_And_Pastes_It_Above()
    {
        var (editor, _, _) = Show("first\nsecond");
        editor.SetPosition(new TextPosition(1, 3));
        var data = ClipboardController.GenerateDataToCopy(editor.ViewModel!, emptySelectionClipboard: true, forceCRLF: false);
        data.Text.ShouldBe("first\n");
        data.Metadata.IsFromEmptySelection.ShouldBeTrue();

        // pasting with that metadata inserts the line above the cursor's line
        editor.SetPosition(new TextPosition(2, 4));
        var controller = ClipboardController.Get(editor).ShouldNotBeNull();
        controller.Paste(data.Text, data.Metadata);
        editor.Text.ShouldBe("first\nfirst\nsecond");
        editor.GetPosition().ShouldBe(new TextPosition(3, 4));
    }

    [AvaloniaFact]
    public void Copy_With_Several_Cursors_Pastes_One_Part_Per_Cursor()
    {
        var (editor, _, _) = Show("a1 b2\nc3 d4");
        editor.SetSelections([new TextSelection(1, 1, 1, 3), new TextSelection(2, 4, 2, 6)]);
        var data = ClipboardController.GenerateDataToCopy(editor.ViewModel!, emptySelectionClipboard: true, forceCRLF: false);
        data.Text.ShouldBe("a1\nd4");
        data.Metadata.MulticursorText.ShouldBe(["a1", "d4"]);

        // the metadata survives the clipboard as JSON
        var json = data.Metadata.ToJson();
        var read = ClipboardStoredMetadata.FromJson(json).ShouldNotBeNull();
        read.MulticursorText.ShouldBe(["a1", "d4"]);
        read.IsFromEmptySelection.ShouldBeFalse();
        ClipboardStoredMetadata.FromJson("{\"version\":2}").ShouldBeNull();
        ClipboardStoredMetadata.FromJson("not json").ShouldBeNull();

        editor.SetSelections([new TextSelection(1, 6, 1, 6), new TextSelection(2, 6, 2, 6)]);
        ClipboardController.Get(editor)!.Paste(data.Text, read);
        editor.Text.ShouldBe("a1 b2a1\nc3 d4d4");
    }

    [AvaloniaFact]
    public void Paste_Of_As_Many_Lines_As_Cursors_Spreads_Them_Without_Metadata()
    {
        var (editor, _, _) = Show("x\ny\nz");
        editor.SetSelections([new TextSelection(1, 2, 1, 2), new TextSelection(2, 2, 2, 2), new TextSelection(3, 2, 3, 2)]);

        ClipboardController.Get(editor)!.Paste("1\n2\n3", null);
        editor.Text.ShouldBe("x1\ny2\nz3");
    }

    [AvaloniaFact]
    public async Task Copy_And_Paste_Through_The_Clipboard_Keep_The_Metadata()
    {
        var (editor, window, _) = Show("line one\nline two");
        editor.SetPosition(new TextPosition(1, 2));
        var controller = ClipboardController.Get(editor).ShouldNotBeNull();

        await controller.CopyAsync(cut: false);
        editor.SetPosition(new TextPosition(2, 1));
        await controller.PasteAsync();
        Dispatcher.UIThread.RunJobs();

        editor.Text.ShouldBe("line one\nline one\nline two");
        _ = window;
    }

    [AvaloniaFact]
    public async Task Cut_Of_An_Empty_Selection_Removes_The_Line()
    {
        var (editor, _, _) = Show("keep\ndrop\nkeep too");
        editor.SetPosition(new TextPosition(2, 2));

        await ClipboardController.Get(editor)!.CopyAsync(cut: true);
        editor.Text.ShouldBe("keep\nkeep too");
    }
}
