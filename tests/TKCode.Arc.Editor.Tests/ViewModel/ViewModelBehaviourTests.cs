// Tests of ViewModel and CursorsController (vscode's src/vs/editor/common/viewModel/viewModelImpl.ts and
// src/vs/editor/common/cursor/cursor.ts, commit b57d5c9e, MIT license) that are not upstream's: upstream covers these
// paths through its cursor and editor tests, which come in the next step. They check what the ported code says: the
// configuration changes (wrapping, read-only, cursor options), the edits through the controller (typing with
// auto-closed characters, undo, composition, paste, cut, commands, executeEdits from a snippet), the visible ranges with
// hidden areas, the view state, the copy, the overview ruler, the reveal requests and the outgoing events.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ViewModelBehaviourTests
{
    private const string AutoClosingLanguageId = "autoClosingLanguage";

    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    private static TextSelection S(int a, int b, int c, int d) => new(a, b, c, d);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private sealed class EventRecorder : ViewEventHandler
    {
        public List<ViewEventType> Types { get; } = new();

        public List<ViewRevealRangeRequestEvent> Reveals { get; } = new();

        public override bool OnConfigurationChanged(ViewConfigurationChangedEvent e) => Add(e);

        public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e) => Add(e);

        public override bool OnDecorationsChanged(ViewDecorationsChangedEvent e) => Add(e);

        public override bool OnFlushed(ViewFlushedEvent e) => Add(e);

        public override bool OnFocusChanged(ViewFocusChangedEvent e) => Add(e);

        public override bool OnLanguageConfigurationChanged(ViewLanguageConfigurationEvent e) => Add(e);

        public override bool OnLineMappingChanged(ViewLineMappingChangedEvent e) => Add(e);

        public override bool OnZonesChanged(ViewZonesChangedEvent e) => Add(e);

        public override bool OnThemeChanged(ViewThemeChangedEvent e) => Add(e);

        public override bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e)
        {
            Reveals.Add(e);
            return Add(e);
        }

        private bool Add(ViewEvent e)
        {
            Types.Add(e.Type);
            return true;
        }
    }

    private static TestCodeEditor CreateAutoClosingEditor(string text, EditorOptions? options = null)
    {
        var service = new LanguageConfigurationService();
        service.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("(", ")"), new AutoClosingPairConditional("\"", "\"")],
            Brackets = [("(", ")")],
        });
        return TestCodeEditor.Create(text, new TestCodeEditorOptions
        {
            LanguageId = AutoClosingLanguageId,
            LanguageConfigurationService = service,
            EditorOptions = options ?? EditorOptions.Default,
        });
    }

    // ------ configuration

    [Fact]
    public void Changing_The_Wrapping_Column_Flushes_The_Lines_And_Keeps_The_Cursor_Valid()
    {
        using var editor = TestCodeEditor.Create("just some text", new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 5 });
        var viewModel = editor.ViewModel;
        viewModel.GetLineCount().ShouldBe(3);
        editor.SetPosition(P(1, 15));
        viewModel.GetCursorStates()[0].ViewState.Position.ShouldBe(P(3, 5));

        var recorder = new EventRecorder();
        viewModel.AddViewEventHandler(recorder);
        editor.UpdateOptions(o => o with { WrappingColumn = -1, Layout = o.Layout });
        viewModel.RemoveViewEventHandler(recorder);

        viewModel.GetLineCount().ShouldBe(1);
        viewModel.Configuration.ViewLineCount.ShouldBe(1);
        viewModel.GetCursorStates()[0].ViewState.Position.ShouldBe(P(1, 15));
        recorder.Types.ShouldContain(ViewEventType.ViewFlushed);
        recorder.Types.ShouldContain(ViewEventType.ViewLineMappingChanged);
        recorder.Types.ShouldContain(ViewEventType.ViewConfigurationChanged);
    }

    [Fact]
    public void Changing_A_Cursor_Option_Recreates_The_Cursor_Configuration()
    {
        using var editor = TestCodeEditor.Create("abc");
        var viewModel = editor.ViewModel;
        var before = viewModel.CursorConfig;
        editor.UpdateOptions(o => o with { Cursor = o.Cursor with { WordSeparators = "-" } });
        viewModel.CursorConfig.ShouldNotBeSameAs(before);
        viewModel.CursorConfig.WordSeparators.ShouldBe("-");

        // an option the cursor does not read keeps it
        var again = viewModel.CursorConfig;
        editor.UpdateOptions(o => o with { CursorSurroundingLines = 3 });
        viewModel.CursorConfig.ShouldBeSameAs(again);
    }

    [Fact]
    public void A_Tab_Size_Change_Of_The_Model_Reaches_The_Cursor_Configuration()
    {
        using var editor = TestCodeEditor.Create("\tabc");
        editor.Model.UpdateOptions(new TextModelUpdateOptions { TabSize = 8 });
        editor.ViewModel.CursorConfig.TabSize.ShouldBe(8);
    }

    [Fact]
    public void A_Language_Configuration_Change_Recreates_The_Cursor_Configuration()
    {
        using var editor = CreateAutoClosingEditor("abc");
        var viewModel = editor.ViewModel;
        var recorder = new EventRecorder();
        viewModel.AddViewEventHandler(recorder);
        var outgoing = new List<OutgoingViewModelEvent>();
        viewModel.OutgoingEvent += outgoing.Add;

        var before = viewModel.CursorConfig;
        using var registration = editor.LanguageConfigurationService.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            AutoClosingPairs = [new AutoClosingPairConditional("[", "]")],
        }, 1);

        viewModel.CursorConfig.ShouldNotBeSameAs(before);
        recorder.Types.ShouldContain(ViewEventType.ViewLanguageConfigurationChanged);
        outgoing.ShouldContain(e => e is OutgoingModelLanguageConfigurationChangedEvent);
    }

    [Fact]
    public void A_Read_Only_Editor_Reports_The_Edit_Attempt_And_Does_Not_Edit()
    {
        using var editor = TestCodeEditor.Create("abc", new TestCodeEditorOptions { EditorOptions = new EditorOptions { ReadOnly = true } });
        var outgoing = new List<OutgoingViewModelEvent>();
        editor.ViewModel.OutgoingEvent += outgoing.Add;

        editor.Type("x");
        editor.Paste("y");
        editor.Cut();

        editor.GetValue().ShouldBe("abc");
        outgoing.Count(e => e is ReadOnlyEditAttemptEvent).ShouldBe(3, "one per attempt");
    }

    // ------ edits through the controller

    [Fact]
    public void Typing_An_Open_Paren_Auto_Closes_It_And_Typing_The_Close_Paren_Types_Over_It()
    {
        using var editor = CreateAutoClosingEditor("");
        var viewModel = editor.ViewModel;

        editor.Type("(");
        editor.GetValue().ShouldBe("()");
        editor.GetPosition().ShouldBe(P(1, 2));
        viewModel.GetCursorAutoClosedCharacters().ShouldBe([R(1, 2, 1, 3)]);

        editor.Type(")");
        editor.GetValue().ShouldBe("()");
        editor.GetPosition().ShouldBe(P(1, 3));
    }

    [Fact]
    public void Moving_Out_Of_The_Pair_Forgets_The_Auto_Closed_Character()
    {
        using var editor = CreateAutoClosingEditor("abc");
        editor.SetPosition(P(1, 4));
        editor.Type("(");
        editor.ViewModel.GetCursorAutoClosedCharacters().Count.ShouldBe(1);

        editor.SetPosition(P(1, 1));
        editor.ViewModel.GetCursorAutoClosedCharacters().ShouldBeEmpty();
    }

    [Fact]
    public void Issue_205598_Retains_Auto_Closed_Actions_Created_By_A_Reentrant_Decorations_Listener()
    {
        // The case of TypeOperationsAutoClosingOvertypeTests, on the real controller: disposing the invalid auto-closed
        // actions fires a decorations event whose listener types again.
        using var editor = CreateAutoClosingEditor("");
        var model = editor.Model;

        var results = new List<string>();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            editor.ExecuteEdits("test", [new SingleEditOperation(model.GetFullModelRange(), "")], [S(1, 1, 1, 1)]);
            for (var pair = 0; pair <= iteration; pair++)
            {
                editor.Type("(");
            }
            var armed = true;
            var listening = true;
            model.DidChangeDecorations += _ =>
            {
                if (listening && armed)
                {
                    armed = false;
                    editor.Type("(");
                }
            };
            editor.SetPosition(P(1, model.GetLineMaxColumn(1)));
            listening = false;
            editor.Type(")");
            editor.SetPosition(P(1, 1));
            var autoClosedDecorations = model.GetAllDecorations().Count(d => d.Options.Description.StartsWith("auto-closed-", StringComparison.Ordinal));
            results.Add($"{model.GetValue()} {autoClosedDecorations}");
        }
        results.ShouldBe(["()() 0", "(())() 0", "((()))() 0"]);
    }

    [Fact]
    public async Task Undo_Restores_The_Text_And_The_Cursor()
    {
        using var editor = TestCodeEditor.Create("abc");
        editor.SetPosition(P(1, 4));
        editor.Type("def");
        editor.GetValue().ShouldBe("abcdef");

        await editor.Undo();
        editor.GetValue().ShouldBe("abc");
        editor.GetPosition().ShouldBe(P(1, 4));
        editor.ViewModel.LastChangeReason.ShouldBe(CursorChangeReason.Undo);

        await editor.Redo();
        editor.GetValue().ShouldBe("abcdef");
        editor.ViewModel.LastChangeReason.ShouldBe(CursorChangeReason.Redo);
    }

    [Fact]
    public void An_Edit_Reveals_The_Cursors()
    {
        using var editor = TestCodeEditor.Create("abc");
        var recorder = new EventRecorder();
        editor.ViewModel.AddViewEventHandler(recorder);
        editor.Type("x");
        recorder.Reveals.Count.ShouldBe(1);
        recorder.Reveals[0].Range.ShouldBe(R(1, 2, 1, 2));
        recorder.Reveals[0].ScrollType.ShouldBe(ScrollType.Smooth);
    }

    [Fact]
    public void Composition_Types_And_Ends()
    {
        using var editor = TestCodeEditor.Create("ab");
        editor.SetPosition(P(1, 2));
        editor.StartComposition();
        editor.CompositionType("x");
        editor.CompositionType("xy", 1);
        editor.EndComposition();
        editor.GetValue().ShouldBe("axyb");
        editor.GetPosition().ShouldBe(P(1, 4));
    }

    [Fact]
    public void A_No_Op_Composition_Type_Still_Moves_The_Cursor()
    {
        using var editor = TestCodeEditor.Create("abcd");
        editor.SetPosition(P(1, 2));
        editor.CompositionType("", 0, 0, 2);
        editor.GetPosition().ShouldBe(P(1, 4));
    }

    [Fact]
    public void Paste_Spreads_Lines_Over_Cursors_And_Cut_Removes_The_Line()
    {
        using var editor = TestCodeEditor.Create("a\nb");
        editor.SetSelections([S(1, 2, 1, 2), S(2, 2, 2, 2)]);
        editor.Paste("1\n2", false, ["1", "2"]);
        editor.GetValue().ShouldBe("a1\nb2");
        editor.ViewModel.LastChangeReason.ShouldBe(CursorChangeReason.Paste);

        editor.SetPosition(P(1, 1));
        editor.Cut();
        editor.GetValue().ShouldBe("b2");
    }

    [Fact]
    public void ExecuteCommand_Keeps_Only_The_Primary_Cursor()
    {
        using var editor = TestCodeEditor.Create("abc\ndef");
        editor.SetSelections([S(1, 1, 1, 1), S(2, 1, 2, 1)]);
        editor.ExecuteCommand("test", new ReplaceCommand(R(1, 1, 1, 1), "x"));
        editor.GetValue().ShouldBe("xabc\ndef");
        editor.GetSelections().Count.ShouldBe(1);
    }

    [Fact]
    public void ExecuteCommands_Runs_One_Command_Per_Cursor()
    {
        using var editor = TestCodeEditor.Create("abc\ndef");
        editor.SetSelections([S(1, 1, 1, 1), S(2, 1, 2, 1)]);
        editor.ExecuteCommands("test", [new ReplaceCommand(R(1, 1, 1, 1), "x"), new ReplaceCommand(R(2, 1, 2, 1), "y")]);
        editor.GetValue().ShouldBe("xabc\nydef");
        editor.GetSelections().ShouldBe([S(1, 2, 1, 2), S(2, 2, 2, 2)]);
    }

    [Fact]
    public void ExecuteEdits_From_A_Snippet_Tracks_The_Auto_Closed_Character()
    {
        using var editor = CreateAutoClosingEditor("");
        editor.ExecuteEdits("snippet", [new SingleEditOperation(R(1, 1, 1, 1), "foo()")], [S(1, 5, 1, 5)]);
        editor.GetValue().ShouldBe("foo()");
        editor.GetPosition().ShouldBe(P(1, 5));
        editor.ViewModel.GetCursorAutoClosedCharacters().ShouldBe([R(1, 5, 1, 6)]);

        // so typing the closing paren types over it
        editor.Type(")");
        editor.GetValue().ShouldBe("foo()");
        editor.GetPosition().ShouldBe(P(1, 6));
    }

    [Fact]
    public void ExecuteEdits_From_Another_Source_Does_Not_Track_Auto_Closed_Characters()
    {
        using var editor = CreateAutoClosingEditor("");
        editor.ExecuteEdits("api", [new SingleEditOperation(R(1, 1, 1, 1), "foo()")], [S(1, 5, 1, 5)]);
        editor.ViewModel.GetCursorAutoClosedCharacters().ShouldBeEmpty();
    }

    [Fact]
    public void An_Edit_By_Another_Editor_Recovers_The_Cursor_From_Its_Markers_Without_Focus()
    {
        using var model = TestTextModel.Create("abc");
        using var editor1 = TestCodeEditor.Create(model, new TestCodeEditorOptions { EditorId = 1, HasTextFocus = false });
        using var editor2 = TestCodeEditor.Create(model, new TestCodeEditorOptions { EditorId = 2 });
        editor1.SetPosition(P(1, 3));
        editor2.SetPosition(P(1, 1));
        editor2.Type("xy");
        editor1.GetPosition().ShouldBe(P(1, 5));
        editor1.ViewModel.LastChangeReason.ShouldBe(CursorChangeReason.RecoverFromMarkers);
    }

    // ------ hidden areas and visible ranges

    [Fact]
    public void Visible_Ranges_Leave_Out_The_Hidden_Areas()
    {
        using var editor = TestCodeEditor.Create(["l1", "l2", "l3", "l4", "l5"], new TestCodeEditorOptions { OuterHeight = 1000 });
        var viewModel = editor.ViewModel;
        var outgoing = new List<OutgoingViewModelEvent>();
        viewModel.OutgoingEvent += outgoing.Add;

        viewModel.SetHiddenAreas([R(2, 1, 3, 1)]);
        viewModel.GetLineCount().ShouldBe(3);
        viewModel.GetHiddenAreas().ShouldBe([R(2, 1, 3, 1)]);
        viewModel.GetVisibleRanges().ShouldBe([R(1, 1, 1, 3), R(4, 1, 5, 3)]);
        outgoing.ShouldContain(e => e is HiddenAreasChangedEvent);

        // the same areas again change nothing
        outgoing.Clear();
        viewModel.SetHiddenAreas([R(2, 1, 3, 1)]);
        outgoing.ShouldBeEmpty();
    }

    [Fact]
    public void The_Completely_Visible_View_Range_Follows_The_Scroll_Position()
    {
        using var editor = TestCodeEditor.Create(["l1", "l2", "l3", "l4", "l5", "l6"], new TestCodeEditorOptions { OuterHeight = 57 });
        var viewModel = editor.ViewModel;
        viewModel.GetCompletelyVisibleViewRange().ShouldBe(R(1, 1, 3, 3));

        viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: 19), ScrollType.Immediate);
        viewModel.GetCompletelyVisibleViewRange().ShouldBe(R(2, 1, 4, 3));
        viewModel.GetCompletelyVisibleViewRangeAtScrollTop(38).ShouldBe(R(3, 1, 5, 3));
    }

    [Fact]
    public void Cursor_Padding_Uses_Sticky_Scroll_And_Surrounding_Lines()
    {
        using var editor = TestCodeEditor.Create(Enumerable.Range(1, 20).Select(i => $"l{i}").ToArray());
        var viewModel = editor.ViewModel;
        // sticky scroll is on with 5 lines by default: the padding is min(5, 10 / 2)
        viewModel.GetViewRangeWithCursorPadding(R(1, 1, 10, 4)).ShouldBe(R(6, 1, 6, 3));

        editor.UpdateOptions(o => o with { StickyScroll = new StickyScrollOptions(false, 5), CursorSurroundingLines = 2 });
        viewModel.GetViewRangeWithCursorPadding(R(1, 1, 10, 4)).ShouldBe(R(3, 1, 9, 3));

        editor.UpdateOptions(o => o with { CursorSurroundingLines = 0 });
        viewModel.GetViewRangeWithCursorPadding(R(1, 1, 10, 4)).ShouldBe(R(1, 1, 10, 4));
    }

    [Fact]
    public void Save_And_Reduce_Restore_State_Round_Trip()
    {
        using var editor = TestCodeEditor.Create(Enumerable.Range(1, 50).Select(i => $"l{i}").ToArray());
        var viewModel = editor.ViewModel;
        viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: 100), ScrollType.Immediate);

        var state = viewModel.SaveState();
        state.FirstPosition.ShouldBe(P(6, 1));
        state.FirstPositionDeltaTop.ShouldBe(-5);

        viewModel.ReduceRestoreState(state).ShouldBe((0d, 100d));
        viewModel.ReduceRestoreState(new ViewModelViewState(3, null, 0, null, 42)).ShouldBe((3d, 42d));
    }

    [Fact]
    public void SaveCursorState_And_RestoreCursorState_Round_Trip_And_Reveal()
    {
        using var editor = TestCodeEditor.Create("abc\ndef");
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(1, 1, 2, 2), S(2, 3, 2, 3)]);
        var saved = viewModel.SaveCursorState();
        saved.ShouldBe([new SavedCursorState(true, P(1, 1), P(2, 2)), new SavedCursorState(false, P(2, 3), P(2, 3))]);

        editor.SetPosition(P(1, 1));
        var recorder = new EventRecorder();
        viewModel.AddViewEventHandler(recorder);
        viewModel.RestoreCursorState(saved);
        editor.GetSelections().ShouldBe([S(1, 1, 2, 2), S(2, 3, 2, 3)]);
        recorder.Reveals.Single().ScrollType.ShouldBe(ScrollType.Immediate);

        // a missing (zero) line or column counts as 1
        viewModel.RestoreCursorState([new SavedCursorState(false, P(0, 0), P(2, 0))]);
        editor.GetSelections().ShouldBe([S(2, 1, 2, 1)]);
    }

    // ------ copy

    [Fact]
    public void Rich_Text_To_Copy_Is_Null_For_Plain_Text_And_Html_Otherwise()
    {
        using (var plain = TestCodeEditor.Create("a<b"))
        {
            plain.ViewModel.GetRichTextToCopy([R(1, 1, 1, 4)], true).ShouldBeNull();
        }

        using var editor = TestCodeEditor.Create("a<b", new TestCodeEditorOptions { LanguageId = "test" });
        var rich = editor.ViewModel.GetRichTextToCopy([R(1, 1, 1, 1)], true);
        rich.ShouldNotBeNull();
        rich.Value.Mode.ShouldBe("test");
        rich.Value.Html.ShouldStartWith("<div style=\"color: undefined;background-color: undefined;font-family: mockFont, ");
        rich.Value.Html.ShouldContain("font-weight: normal;font-size: 14px;line-height: 19px;white-space: pre;\">");
        rich.Value.Html.ShouldEndWith("<div><span style=\"color: undefined;\">a&lt;b</span></div></div>");

        editor.ViewModel.GetRichTextToCopy([R(1, 1, 1, 1)], false).ShouldBeNull();
        editor.ViewModel.GetRichTextToCopy([R(1, 1, 1, 2), R(1, 2, 1, 3)], true).ShouldBeNull();
    }

    [Fact]
    public void Rich_Text_To_Copy_Uses_The_Token_Color_Map()
    {
        using var editor = TestCodeEditor.Create("ab", new TestCodeEditorOptions
        {
            LanguageId = "test",
            Configure = o => o with { TokenColorMap = ["", "#111111", "#222222"] },
        });
        var rich = editor.ViewModel.GetRichTextToCopy([R(1, 1, 1, 3)], false);
        rich!.Value.Html.ShouldStartWith("<div style=\"color: #111111;background-color: #222222;");
        rich.Value.Html.ShouldContain("<span style=\"color: #111111;\">ab</span>");
    }

    // ------ decorations

    [Fact]
    public void Overview_Ruler_Decorations_Are_Grouped_By_Color_And_Merged()
    {
        using var editor = TestCodeEditor.Create(["l1", "l2", "l3", "l4", "l5"]);
        ModelDecorationOptions Ruler(string color, OverviewRulerLane lane) => new("ruler")
        {
            OverviewRuler = new ModelDecorationOverviewRulerOptions { Color = DecorationColor.FromValue(color), Position = lane },
        };
        editor.Model.DeltaDecorations([],
        [
            new ModelDeltaDecoration(R(1, 1, 1, 2), Ruler("#ff0000", OverviewRulerLane.Left)),
            new ModelDeltaDecoration(R(2, 1, 2, 2), Ruler("#ff0000", OverviewRulerLane.Left)),
            new ModelDeltaDecoration(R(4, 1, 4, 2), Ruler("#ff0000", OverviewRulerLane.Left)),
            new ModelDeltaDecoration(R(3, 1, 3, 2), Ruler("#00ff00", OverviewRulerLane.Right)),
        ]);

        var groups = editor.ViewModel.GetAllOverviewRulerDecorations(new object());
        groups.Count.ShouldBe(2);
        groups[0].Color.ShouldBe("#ff0000");
        groups[0].Data.ShouldBe([1, 1, 2, 1, 4, 4]);
        groups[1].Color.ShouldBe("#00ff00");
        groups[1].Data.ShouldBe([4, 3, 3]);
    }

    [Fact]
    public void A_Theme_Change_Reaches_The_View()
    {
        using var editor = TestCodeEditor.Create("abc");
        var recorder = new EventRecorder();
        editor.ViewModel.AddViewEventHandler(recorder);
        editor.ViewModel.OnDidColorThemeChange(new object());
        recorder.Types.ShouldBe([ViewEventType.ViewThemeChanged]);
    }

    [Fact]
    public void Rendering_Data_Of_A_Line()
    {
        using var editor = TestCodeEditor.Create("\tabc", new TestCodeEditorOptions { TabSize = 4 });
        var data = editor.ViewModel.GetViewLineRenderingData(1);
        data.Content.ShouldBe("\tabc");
        data.MinColumn.ShouldBe(1);
        data.MaxColumn.ShouldBe(5);
        data.TabSize.ShouldBe(4);
        data.TextDirection.ShouldBe(TextDirection.LTR);

        var viewport = editor.ViewModel.GetViewportViewLineRenderingData(R(1, 1, 1, 5), 1);
        viewport.Content.ShouldBe("\tabc");
    }

    // ------ reveal, focus and whitespace

    [Fact]
    public void RevealRange_And_The_Top_And_Bottom_Most_Cursor_Requests()
    {
        using var editor = TestCodeEditor.Create("abc\ndef\nghi");
        var viewModel = editor.ViewModel;
        editor.SetSelections([S(2, 2, 2, 2), S(1, 3, 1, 3), S(3, 1, 3, 1)]);
        var recorder = new EventRecorder();
        viewModel.AddViewEventHandler(recorder);

        viewModel.RevealRange("test", false, R(2, 1, 2, 3), VerticalRevealType.Center, ScrollType.Immediate);
        viewModel.RevealAllCursors("test", true);
        viewModel.RevealPrimaryCursor("test", true);

        recorder.Reveals.Count.ShouldBe(3);
        recorder.Reveals[0].Range.ShouldBe(R(2, 1, 2, 3));
        recorder.Reveals[0].VerticalType.ShouldBe(VerticalRevealType.Center);
        recorder.Reveals[1].Selections!.Count.ShouldBe(3);
        recorder.Reveals[2].Selections.ShouldBe([S(2, 2, 2, 2)]);
    }

    [Fact]
    public void Focus_Changes_Go_To_The_View_And_Out()
    {
        using var editor = TestCodeEditor.Create("abc", new TestCodeEditorOptions { HasTextFocus = false });
        var recorder = new EventRecorder();
        editor.ViewModel.AddViewEventHandler(recorder);
        var outgoing = new List<OutgoingViewModelEvent>();
        editor.ViewModel.OutgoingEvent += outgoing.Add;

        editor.ViewModel.SetHasFocus(true);
        recorder.Types.ShouldBe([ViewEventType.ViewFocusChanged]);
        outgoing.OfType<FocusChangedEvent>().Single().HasFocus.ShouldBeTrue();
    }

    [Fact]
    public void ChangeWhitespace_Raises_Zone_Events_When_Something_Changed()
    {
        using var editor = TestCodeEditor.Create("abc\ndef");
        var recorder = new EventRecorder();
        editor.ViewModel.AddViewEventHandler(recorder);

        editor.ViewModel.ChangeWhitespace(_ => { });
        recorder.Types.ShouldBeEmpty();

        editor.ViewModel.ChangeWhitespace(accessor => accessor.InsertWhitespace(1, 0, 10, 0));
        recorder.Types.ShouldBe([ViewEventType.ViewZonesChanged]);
        editor.ViewModel.ViewLayout.GetWhitespaces().Length.ShouldBe(1);
        editor.ViewModel.ViewLayout.GetVerticalOffsetForLineNumber(2).ShouldBe(29);
    }

    [Fact]
    public void The_Column_Select_Data_Is_Kept_Until_The_Cursors_Change()
    {
        using var editor = TestCodeEditor.Create("abc\ndef");
        var viewModel = editor.ViewModel;
        editor.SetSelection(S(1, 2, 2, 3));
        viewModel.GetCursorColumnSelectData().ShouldBe(new ColumnSelectData(false, 1, 1, 2, 2));

        var data = new ColumnSelectData(true, 1, 5, 2, 7);
        viewModel.SetCursorColumnSelectData(data);
        viewModel.GetCursorColumnSelectData().ShouldBe(data);

        editor.SetPosition(P(1, 1));
        viewModel.GetCursorColumnSelectData().IsReal.ShouldBeFalse();
    }
}
