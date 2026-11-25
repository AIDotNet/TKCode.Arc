// Tests for ViewModel, ported from vscode (src/vs/editor/test/browser/viewModel/viewModelImpl.test.ts, with
// testViewModel.ts as ViewModelTestUtils.WithTestViewModel; commit b57d5c9e), MIT license: all cases.
// Differences:
//  - 'issue #44805: SplitLinesCollection' overrides handleEvents upstream to record the view line count once per batch
//    of view events; ViewEventHandler.HandleEvents is not virtual here, so the handler records it in every event
//    handler it gets, and the test checks the count before the undo and that every handler after it saw one line
//    (upstream's [4, 1, 1, 1, 1]: the count before, the counts the handler saw, the count after);
//  - 'view models react first to model changes' asserts nothing upstream (it fails by throwing); listeners that throw
//    are reported to UnexpectedErrors here, so the test also checks the text both view models end with;
//  - 'issue #44805: No visible lines via API call' checks that getVisibleRanges returns, as upstream (it cannot return
//    null here either);
//  - getPlainTextToCopy's string | string[] is SourceText or SourceTexts.
// ViewModelBehaviourTests has tests of the view model that are not upstream's.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;
using Xunit;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal static class ViewModelTestUtils
{
    private const int EditorId = 1;

    /// <summary>Upstream's testViewModel: a view model on a new model of the lines, disposed after the callback.</summary>
    public static void WithTestViewModel(string[] text, TestCodeEditorOptions options, Action<EditorViewModel, TextModel, ViewModelConfiguration> callback)
    {
        var configuration = new ViewModelConfiguration(TestCodeEditor.CreateViewModelOptions(options));
        using var model = TestTextModel.Create(string.Join('\n', text));
        var monospaceLineBreaksComputerFactory = new MonospaceLineBreaksComputerFactory(
            WrappingOptions.WordWrapBreakBeforeCharacters,
            WrappingOptions.WordWrapBreakAfterCharacters);
        using var testLanguageConfigurationService = new LanguageConfigurationService();
        using var viewModel = new EditorViewModel(
            EditorId,
            configuration,
            model,
            monospaceLineBreaksComputerFactory,
            monospaceLineBreaksComputerFactory,
            TestCodeEditor.NoAnimationFrames,
            testLanguageConfigurationService,
            new NullAttachedView(),
            new ImmediateBatchableTarget());

        callback(viewModel, model, configuration);
    }

    /// <summary>Upstream's async use of testViewModel.</summary>
    public static async Task WithTestViewModelAsync(string[] text, TestCodeEditorOptions options, Func<EditorViewModel, TextModel, Task> callback)
    {
        var configuration = new ViewModelConfiguration(TestCodeEditor.CreateViewModelOptions(options));
        using var model = TestTextModel.Create(string.Join('\n', text));
        var monospaceLineBreaksComputerFactory = new MonospaceLineBreaksComputerFactory(
            WrappingOptions.WordWrapBreakBeforeCharacters,
            WrappingOptions.WordWrapBreakAfterCharacters);
        using var testLanguageConfigurationService = new LanguageConfigurationService();
        using var viewModel = new EditorViewModel(
            EditorId,
            configuration,
            model,
            monospaceLineBreaksComputerFactory,
            monospaceLineBreaksComputerFactory,
            TestCodeEditor.NoAnimationFrames,
            testLanguageConfigurationService,
            new NullAttachedView(),
            new ImmediateBatchableTarget());

        await callback(viewModel, model);
    }

    private sealed class NullAttachedView : IAttachedView
    {
        public void SetVisibleLines(IReadOnlyList<TextRange> visibleLines, bool stabilized)
        {
        }
    }

    private sealed class ImmediateBatchableTarget : IBatchableTarget
    {
        public T BatchChanges<T>(Func<T> cb) => cb();
    }
}

[Collection(UnexpectedErrorsCollection.Name)]
public sealed class ViewModelTests
{
    private static readonly TestCodeEditorOptions NoOptions = new();

    private static TextRange R(int a, int b, int c, int d) => new(a, b, c, d);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    [Fact]
    public void Issue_21073_SplitLinesCollection_Attempt_To_Access_A_Newer_Model()
    {
        string[] text = [""];
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            viewModel.GetLineCount().ShouldBe(1);

            viewModel.SetViewport(1, 1, 1);

            model.ApplyEdits([new SingleEditOperation(R(1, 1, 1, 1), string.Join('\n',
            [
                "line01",
                "line02",
                "line03",
                "line04",
                "line05",
                "line06",
                "line07",
                "line08",
                "line09",
                "line10",
            ]))]);

            viewModel.GetLineCount().ShouldBe(10);
        });
    }

    private sealed class LineCountRecorder(EditorViewModel viewModel, List<int> viewLineCount) : ViewEventHandler
    {
        private bool Record()
        {
            // Access the view model
            viewLineCount.Add(viewModel.GetLineCount());
            return false;
        }

        public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e) => Record();

        public override bool OnDecorationsChanged(ViewDecorationsChangedEvent e) => Record();

        public override bool OnFlushed(ViewFlushedEvent e) => Record();

        public override bool OnLineMappingChanged(ViewLineMappingChangedEvent e) => Record();

        public override bool OnLinesChanged(ViewLinesChangedEvent e) => Record();

        public override bool OnLinesDeleted(ViewLinesDeletedEvent e) => Record();

        public override bool OnLinesInserted(ViewLinesInsertedEvent e) => Record();

        public override bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e) => Record();

        public override bool OnScrollChanged(ViewScrollChangedEvent e) => Record();

        public override bool OnTokensChanged(ViewTokensChangedEvent e) => Record();

        public override bool OnZonesChanged(ViewZonesChangedEvent e) => Record();
    }

    [Fact]
    public async Task Issue_44805_SplitLinesCollection_Attempt_To_Access_A_Newer_Model()
    {
        string[] text = [""];
        await ViewModelTestUtils.WithTestViewModelAsync(text, NoOptions, async (viewModel, model) =>
        {
            viewModel.GetLineCount().ShouldBe(1);

            model.PushEditOperations([], [new SingleEditOperation(R(1, 1, 1, 1), "\ninsert1")], _ => []);
            model.PushEditOperations([], [new SingleEditOperation(R(1, 1, 1, 1), "\ninsert2")], _ => []);
            model.PushEditOperations([], [new SingleEditOperation(R(1, 1, 1, 1), "\ninsert3")], _ => []);

            var viewLineCount = new List<int>();

            viewLineCount.Add(viewModel.GetLineCount());
            var eventHandler = new LineCountRecorder(viewModel, viewLineCount);
            viewModel.AddViewEventHandler(eventHandler);
            await model.Undo();
            viewLineCount.Add(viewModel.GetLineCount());

            viewLineCount[0].ShouldBe(4);
            viewLineCount.Count.ShouldBeGreaterThan(2);
            viewLineCount.Skip(1).ShouldAllBe(count => count == 1);

            viewModel.RemoveViewEventHandler(eventHandler);
            eventHandler.Dispose();
        });
    }

    [Fact]
    public void View_Models_React_First_To_Model_Changes()
    {
        string[] initialText = ["Hello", "world"];

        using var model = TestTextModel.Create(string.Join('\n', initialText));
        using var ed1 = TestCodeEditor.Create(model, new TestCodeEditorOptions { EditorId = 1 });
        using var ed2 = TestCodeEditor.Create(model, new TestCodeEditorOptions { EditorId = 2 });

        // Add a nasty listener which modifies the model during the model change event
        var isFirst = true;
        ed1.ViewModel.OutgoingEvent += e =>
        {
            if (e is OutgoingModelContentChangedEvent && isFirst)
            {
                isFirst = false;
                // delete the \n
                model.ApplyEdits([new SingleEditOperation(R(1, 6, 2, 1), "")]);
            }
        };

        model.ApplyEdits([new SingleEditOperation(R(2, 6, 2, 6), "!")]);

        model.GetValue().ShouldBe("Helloworld!");
        ed1.ViewModel.GetLineCount().ShouldBe(1);
        ed2.ViewModel.GetLineCount().ShouldBe(1);
        ed1.ViewModel.GetLineContent(1).ShouldBe("Helloworld!");
        ed2.ViewModel.GetLineContent(1).ShouldBe("Helloworld!");
    }

    [Fact]
    public void Issue_44805_No_Visible_Lines_Via_API_Call()
    {
        string[] text = ["line1", "line2", "line3"];
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            viewModel.GetLineCount().ShouldBe(3);
            viewModel.SetHiddenAreas([R(1, 1, 3, 1)]);
            viewModel.GetVisibleRanges().ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Issue_44805_No_Visible_Lines_Via_Undoing()
    {
        string[] text = [""];
        await ViewModelTestUtils.WithTestViewModelAsync(text, NoOptions, async (viewModel, model) =>
        {
            viewModel.GetLineCount().ShouldBe(1);

            model.PushEditOperations([], [new SingleEditOperation(R(1, 1, 1, 1), "line1\nline2\nline3")], _ => []);

            viewModel.SetHiddenAreas([R(1, 1, 1, 1)]);
            viewModel.GetLineCount().ShouldBe(2);

            await model.Undo();
            viewModel.GetVisibleRanges().ShouldNotBeNull();
        });
    }

    private static void AssertGetPlainTextToCopy(string[] text, TextRange[] ranges, bool emptySelectionClipboard, string expected)
    {
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            var actual = viewModel.GetPlainTextToCopy(ranges, emptySelectionClipboard, false);
            actual.SourceTexts.ShouldBeNull();
            actual.SourceText.ShouldBe(expected);
        });
    }

    private static void AssertGetPlainTextToCopy(string[] text, TextRange[] ranges, bool emptySelectionClipboard, string[] expected)
    {
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            var actual = viewModel.GetPlainTextToCopy(ranges, emptySelectionClipboard, false);
            actual.SourceText.ShouldBeNull();
            actual.SourceTexts.ShouldBe(expected);
        });
    }

    private static readonly string[] UsualText = ["", "line2", "line3", "line4", ""];

    [Fact]
    public void GetPlainTextToCopy_0_1() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 2)], false, "");

    [Fact]
    public void GetPlainTextToCopy_0_1_EmptySelectionClipboard() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 2)], true, "line2\n");

    [Fact]
    public void GetPlainTextToCopy_1_1() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 6)], false, "ine2");

    [Fact]
    public void GetPlainTextToCopy_1_1_EmptySelectionClipboard() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 6)], true, "ine2");

    [Fact]
    public void GetPlainTextToCopy_0_2() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 2), R(3, 2, 3, 2)], false, "");

    [Fact]
    public void GetPlainTextToCopy_0_2_EmptySelectionClipboard() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 2), R(3, 2, 3, 2)], true, ["line2\n", "line3\n"]);

    [Fact]
    public void Issue_256039_GetPlainTextToCopy_With_Multiple_Cursors_And_Empty_Selections_Should_Return_Array() =>
        // When copying with multiple cursors (empty selections) with emptySelectionClipboard enabled,
        // the result should be an array so that pasting with "editor.multiCursorPaste": "full"
        // correctly distributes each line to the corresponding cursor.
        AssertGetPlainTextToCopy(UsualText, [R(2, 1, 2, 1), R(3, 1, 3, 1)], true, ["line2\n", "line3\n"]);

    [Fact]
    public void GetPlainTextToCopy_1_2() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 6), R(3, 2, 3, 2)], false, "ine2");

    [Fact]
    public void GetPlainTextToCopy_1_2_EmptySelectionClipboard() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 6), R(3, 2, 3, 2)], true, ["ine2", "line3\n"]);

    [Fact]
    public void GetPlainTextToCopy_2_2() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 6), R(3, 2, 3, 6)], false, ["ine2", "ine3"]);

    [Fact]
    public void GetPlainTextToCopy_2_2_Reversed() =>
        AssertGetPlainTextToCopy(UsualText, [R(3, 2, 3, 6), R(2, 2, 2, 6)], false, ["ine2", "ine3"]);

    [Fact]
    public void GetPlainTextToCopy_0_3_EmptySelectionClipboard() =>
        AssertGetPlainTextToCopy(UsualText, [R(2, 2, 2, 2), R(2, 3, 2, 3), R(3, 2, 3, 2)], true, ["line2\n", "line3\n"]);

    [Fact]
    public void Issue_22688_Always_Use_CRLF_For_Clipboard_On_Windows()
    {
        ViewModelTestUtils.WithTestViewModel(UsualText, NoOptions, (viewModel, model, _) =>
        {
            model.SetEOL(EndOfLineSequence.LF);
            var actual = viewModel.GetPlainTextToCopy([R(2, 1, 5, 1)], true, true);
            actual.SourceText.ShouldBe("line2\r\nline3\r\nline4\r\n");
        });
    }

    [Fact]
    public void Issue_40926_Incorrect_Spacing_When_Inserting_New_Line_After_Multiple_Folded_Blocks_Of_Code()
    {
        ViewModelTestUtils.WithTestViewModel(
            [
                "foo = {",
                "    foobar: function() {",
                "        this.foobar();",
                "    },",
                "    foobar: function() {",
                "        this.foobar();",
                "    },",
                "    foobar: function() {",
                "        this.foobar();",
                "    },",
                "}",
            ],
            NoOptions,
            (viewModel, model, _) =>
            {
                viewModel.SetHiddenAreas(
                [
                    R(3, 1, 3, 1),
                    R(6, 1, 6, 1),
                    R(9, 1, 9, 1),
                ]);

                model.ApplyEdits(
                [
                    new SingleEditOperation(R(4, 7, 4, 7), "\n    "),
                    new SingleEditOperation(R(7, 7, 7, 7), "\n    "),
                    new SingleEditOperation(R(10, 7, 10, 7), "\n    "),
                ]);

                viewModel.GetLineCount().ShouldBe(11);
            });
    }

    [Fact]
    public void NormalizePosition_With_Multiple_Touching_Injected_Text()
    {
        ViewModelTestUtils.WithTestViewModel(["just some text"], NoOptions, (viewModel, model, _) =>
        {
            model.DeltaDecorations([],
            [
                new ModelDeltaDecoration(R(1, 8, 1, 8), new ModelDecorationOptions("test")
                {
                    Before = new ModelDecorationInjectedTextOptions("bar"),
                    ShowIfCollapsed = true,
                }),
                new ModelDeltaDecoration(R(1, 8, 1, 8), new ModelDecorationOptions("test")
                {
                    Before = new ModelDecorationInjectedTextOptions("bz"),
                    ShowIfCollapsed = true,
                }),
            ]);

            // just sobarbzme text

            viewModel.NormalizePosition(P(1, 8), PositionAffinity.None).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 9), PositionAffinity.None).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 11), PositionAffinity.None).ShouldBe(P(1, 11));
            viewModel.NormalizePosition(P(1, 12), PositionAffinity.None).ShouldBe(P(1, 11));
            viewModel.NormalizePosition(P(1, 13), PositionAffinity.None).ShouldBe(P(1, 13));

            viewModel.NormalizePosition(P(1, 8), PositionAffinity.Left).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 9), PositionAffinity.Left).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 11), PositionAffinity.Left).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 12), PositionAffinity.Left).ShouldBe(P(1, 8));
            viewModel.NormalizePosition(P(1, 13), PositionAffinity.Left).ShouldBe(P(1, 8));

            viewModel.NormalizePosition(P(1, 8), PositionAffinity.Right).ShouldBe(P(1, 13));
            viewModel.NormalizePosition(P(1, 9), PositionAffinity.Right).ShouldBe(P(1, 13));
            viewModel.NormalizePosition(P(1, 11), PositionAffinity.Right).ShouldBe(P(1, 13));
            viewModel.NormalizePosition(P(1, 12), PositionAffinity.Right).ShouldBe(P(1, 13));
            viewModel.NormalizePosition(P(1, 13), PositionAffinity.Right).ShouldBe(P(1, 13));
        });
    }

    [Fact]
    public void Issue_193262_Incorrect_Implementation_Of_ModifyPosition()
    {
        ViewModelTestUtils.WithTestViewModel(
            ["just some text"],
            new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 5 },
            (viewModel, model, _) =>
            {
                viewModel.ModifyPosition(P(3, 2), -1).ShouldBe(P(3, 1));
            });
    }

    // ------ suite 'hidden areas must always leave at least one visible line'

    [Fact]
    public void Replacing_The_Only_Visible_Line_Content_Does_Not_Make_It_Hidden()
    {
        ViewModelTestUtils.WithTestViewModel(["line1", "line2", "line3"], NoOptions, (viewModel, model, _) =>
        {
            // Hide lines 1 and 3, leaving only line 2 visible
            viewModel.SetHiddenAreas([R(1, 1, 1, 1), R(3, 1, 3, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Replace line 2 content entirely
            model.ApplyEdits([new SingleEditOperation(R(2, 1, 2, 6), "new content")]);

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Deleting_The_Only_Visible_Line_When_It_Is_The_Last_Line()
    {
        ViewModelTestUtils.WithTestViewModel(["line1", "line2", "line3"], NoOptions, (viewModel, model, _) =>
        {
            // Hide lines 1-2, leaving only line 3 visible
            viewModel.SetHiddenAreas([R(1, 1, 2, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Delete line 3 by merging it into line 2
            model.ApplyEdits([new SingleEditOperation(R(2, 6, 3, 6), null)]);

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Deleting_The_Only_Visible_Line_When_It_Is_In_The_Middle()
    {
        ViewModelTestUtils.WithTestViewModel(["line1", "line2", "line3", "line4", "line5"], NoOptions, (viewModel, model, _) =>
        {
            // Hide lines 1-2 and 4-5, leaving only line 3 visible
            viewModel.SetHiddenAreas([R(1, 1, 2, 1), R(4, 1, 5, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Delete line 3 by merging adjacent lines
            model.ApplyEdits([new SingleEditOperation(R(2, 6, 4, 1), null)]);

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public async Task Undo_That_Removes_The_Only_Visible_Line()
    {
        await ViewModelTestUtils.WithTestViewModelAsync(["line1"], NoOptions, async (viewModel, model) =>
        {
            viewModel.GetLineCount().ShouldBe(1);

            // Insert lines to create content
            model.PushEditOperations([], [new SingleEditOperation(R(1, 6, 1, 6), "\nline2\nline3\nline4\nline5")], _ => []);

            viewModel.GetLineCount().ShouldBe(5);

            // Hide lines 1-2 and 4-5, leaving only line 3 visible
            viewModel.SetHiddenAreas([R(1, 1, 2, 1), R(4, 1, 5, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Undo collapses back to 1 line, but hidden area decorations may grow
            await model.Undo();

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Deleting_The_Only_Visible_Line_Between_Two_Hidden_Areas_Leaves_All_Lines_Hidden()
    {
        string[] text = ["line1", "line2", "line3", "line4", "line5", "line6", "line7", "line8"];
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            viewModel.GetLineCount().ShouldBe(8);

            // Hide lines 1-5 and 7-8, leaving only line 6 visible
            viewModel.SetHiddenAreas([R(1, 1, 5, 1), R(7, 1, 8, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Delete lines 6, 7, 8 — the only visible line plus some hidden ones
            model.ApplyEdits([new SingleEditOperation(R(6, 1, 8, 5), null)]);

            // The view model must still have at least one visible line
            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Multiple_Visible_Lines_Deleted_Leaving_Only_Hidden_Lines()
    {
        string[] text = ["hidden1", "hidden2", "visible1", "visible2", "hidden3", "hidden4"];
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            viewModel.SetHiddenAreas([R(1, 1, 2, 1), R(5, 1, 6, 1)]);
            viewModel.GetLineCount().ShouldBe(2);

            // Delete visible lines 3 and 4
            model.ApplyEdits([new SingleEditOperation(R(2, 8, 5, 1), null)]);

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }

    [Fact]
    public void Hidden_Areas_From_Multiple_Sources_That_Overlap_Produce_Valid_Merged_Result()
    {
        var text = Enumerable.Range(1, 10).Select(i => $"line{i}").ToArray();
        ViewModelTestUtils.WithTestViewModel(text, NoOptions, (viewModel, model, _) =>
        {
            // Source A hides a large range [1-8].
            // Source B hides small ranges [2-3] and [5-6] that are subsumed by A.
            // mergeLineRangeArray advances both pointers after merging [1-8]+[2-3]=[1-8], leaving [5-6] and [8,9] as
            // separate entries that overlap with or are subsumed by [1-8]. normalizeLineRanges in setHiddenAreas cleans
            // this up, so the result should still be correct: lines 1-8 hidden, lines 9-10 visible.
            var sourceA = new object();
            var sourceB = new object();
            viewModel.SetHiddenAreas([R(1, 1, 8, 1)], sourceA);
            viewModel.SetHiddenAreas([R(2, 1, 3, 1), R(5, 1, 6, 1), R(8, 1, 9, 1)], sourceB);

            // Lines 1-9 should be hidden (merged from [1-8] and [8-9]), line 10 visible
            viewModel.GetLineCount().ShouldBe(1, "only line 10 should be visible");

            // The hidden areas returned should be non-overlapping and sorted
            var hiddenAreas = viewModel.GetHiddenAreas();
            for (var i = 1; i < hiddenAreas.Count; i++)
            {
                hiddenAreas[i].StartLineNumber.ShouldBeGreaterThan(
                    hiddenAreas[i - 1].EndLineNumber,
                    $"hidden areas should not overlap: [{hiddenAreas[i - 1].StartLineNumber}-{hiddenAreas[i - 1].EndLineNumber}] and [{hiddenAreas[i].StartLineNumber}-{hiddenAreas[i].EndLineNumber}]");
            }
        });
    }

    [Fact]
    public void Tab_Size_Change_With_Drifted_Hidden_Area_Decorations_Must_Not_Leave_0_Visible_Lines()
    {
        ViewModelTestUtils.WithTestViewModel(["line1", "line2", "line3"], NoOptions, (viewModel, model, _) =>
        {
            // Hide lines 1-2, leaving only line 3 visible.
            viewModel.SetHiddenAreas([R(1, 1, 2, 1)]);
            viewModel.GetLineCount().ShouldBe(1);

            // Insert at (2,1) — the end edge of the hidden area decoration.
            // AlwaysGrowsWhenTypingAtEdges causes the decoration to grow from
            // [1,1 → 2,1] to [1,1 → 3,1], covering what was the visible line 3.
            // After this insert, the file has 4 lines, decoration covers [1-3], line 4 visible.
            model.ApplyEdits([new SingleEditOperation(R(2, 1, 2, 1), "x\n")]);
            // Insert again to push decoration further
            model.ApplyEdits([new SingleEditOperation(R(3, 1, 3, 1), "y\n")]);
            // Now file has 5 lines, decoration covers [1-4], line 5 visible.

            // Delete lines 4-5 to collapse back, making decoration cover everything
            model.ApplyEdits([new SingleEditOperation(R(4, 1, 5, 6), "")]);
            // Now file has 4 lines. acceptVersionId ensures viewLines >= 1.

            // Tab size change: triggers _constructLines(resetHiddenAreas=false)
            // which re-reads the decoration ranges (which may cover all lines).
            model.UpdateOptions(new TextModelUpdateOptions { TabSize = 8 });

            viewModel.GetLineCount().ShouldBeGreaterThanOrEqualTo(1);
        });
    }
}
