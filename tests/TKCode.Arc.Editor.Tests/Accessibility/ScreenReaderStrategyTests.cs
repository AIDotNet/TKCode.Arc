// Ported from vscode (src/vs/editor/test/browser/controller/textAreaState.test.ts, suite SimplePagedScreenReaderStrategy,
// commit b57d5c9e), MIT license. All 3 upstream tests (9 cases). Upstream compares a TextAreaState made with
// TextAreaState.fromScreenReaderContentState; that swaps the offsets of a right-to-left selection back, which Expect
// does here. The other tests (RTL, trimming, several pages, newline count) are written for the port.

using Shouldly;
using TKCode.Arc.Editor.Accessibility;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ScreenReaderStrategyTests
{
    private static void TestPagedScreenReaderStrategy(string[] lines, TextSelection selection, string value, int selectionStart, int selectionEnd, TextRange range, int newlineCountBeforeSelection)
    {
        using var model = TestTextModel.Create(string.Join('\n', lines));
        var state = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), selection, 10, true);

        // TextAreaState.fromScreenReaderContentState
        var (start, end) = state.Selection.Direction == SelectionDirection.LeftToRight
            ? (state.SelectionStart, state.SelectionEnd)
            : (state.SelectionEnd, state.SelectionStart);
        state.Value.ShouldBe(value);
        start.ShouldBe(selectionStart);
        end.ShouldBe(selectionEnd);
        state.Selection.Range.ShouldBe(range);
        state.NewlineCountBeforeSelection.ShouldBe(newlineCountBeforeSelection);
    }

    [Fact]
    public void Simple()
    {
        TestPagedScreenReaderStrategy(["Hello world!"], new TextSelection(1, 13, 1, 13), "Hello world!", 12, 12, new TextRange(1, 13, 1, 13), 0);
        TestPagedScreenReaderStrategy(["Hello world!"], new TextSelection(1, 1, 1, 1), "Hello world!", 0, 0, new TextRange(1, 1, 1, 1), 0);
        TestPagedScreenReaderStrategy(["Hello world!"], new TextSelection(1, 1, 1, 6), "Hello world!", 0, 5, new TextRange(1, 1, 1, 6), 0);
    }

    [Fact]
    public void Multiline()
    {
        TestPagedScreenReaderStrategy(["Hello world!", "How are you?"], new TextSelection(1, 1, 1, 1), "Hello world!\nHow are you?", 0, 0, new TextRange(1, 1, 1, 1), 0);
        TestPagedScreenReaderStrategy(["Hello world!", "How are you?"], new TextSelection(2, 1, 2, 1), "Hello world!\nHow are you?", 13, 13, new TextRange(2, 1, 2, 1), 1);
    }

    [Fact]
    public void Page()
    {
        const string Text = "L1\nL2\nL3\nL4\nL5\nL6\nL7\nL8\nL9\nL10\nL11\nL12\nL13\nL14\nL15\nL16\nL17\nL18\nL19\nL20\nL21";
        TestPagedScreenReaderStrategy([Text], new TextSelection(1, 1, 1, 1), "L1\nL2\nL3\nL4\nL5\nL6\nL7\nL8\nL9\nL10\n", 0, 0, new TextRange(1, 1, 1, 1), 0);
        TestPagedScreenReaderStrategy([Text], new TextSelection(11, 1, 11, 1), "L11\nL12\nL13\nL14\nL15\nL16\nL17\nL18\nL19\nL20\n", 0, 0, new TextRange(11, 1, 11, 1), 0);
        TestPagedScreenReaderStrategy([Text], new TextSelection(12, 1, 12, 1), "L11\nL12\nL13\nL14\nL15\nL16\nL17\nL18\nL19\nL20\n", 4, 4, new TextRange(12, 1, 12, 1), 1);
        TestPagedScreenReaderStrategy([Text], new TextSelection(21, 1, 21, 1), "L21", 0, 0, new TextRange(21, 1, 21, 1), 0);
    }

    [Fact]
    public void Right_to_left_selection_keeps_upstreams_offset_order()
    {
        using var model = TestTextModel.Create("Hello world!");
        var state = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), new TextSelection(1, 6, 1, 1), 10, true);

        state.SelectionStart.ShouldBe(5);
        state.SelectionEnd.ShouldBe(0);
        state.SelectedText.ShouldBe("Hello");
    }

    [Fact]
    public void Long_text_is_cut_at_500_characters_on_each_side()
    {
        var line = new string('a', 1200);
        using var model = TestTextModel.Create(line);
        var state = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), new TextSelection(1, 601, 1, 601), 10, true);

        state.Value.Length.ShouldBe(1000);
        state.SelectionStart.ShouldBe(500);
        state.StartPositionWithinEditor.ShouldBe(new TextPosition(1, 101));

        // a long selection keeps its first and last 500 characters around an ellipsis
        var selected = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), new TextSelection(1, 1, 1, 1201), 10, true);
        selected.SelectedText.Length.ShouldBe(1001);
        selected.SelectedText[500].ShouldBe('…');

        // untrimmed, everything
        SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), new TextSelection(1, 601, 1, 601), 10, false)
            .Value.Length.ShouldBe(1200);
    }

    [Fact]
    public void A_selection_over_several_pages_reads_its_first_and_last_page()
    {
        var lines = Enumerable.Range(1, 40).Select(i => "L" + i).ToArray();
        using var model = TestTextModel.Create(string.Join('\n', lines));
        var state = SimplePagedScreenReaderStrategy.FromEditorSelection(new TextModelScreenReaderModel(model), new TextSelection(2, 1, 35, 1), 10, true);

        state.SelectedText.ShouldStartWith("L2\nL3\n");
        state.SelectedText.ShouldContain("L10\n…L31\n");
        state.SelectedText.ShouldEndWith("L34\n");
        state.Value.ShouldStartWith("L1\n");
        state.Value.ShouldEndWith("L40");
    }

    [Fact]
    public void Newline_count()
    {
        SimplePagedScreenReaderStrategy.NewlineCount("").ShouldBe(0);
        SimplePagedScreenReaderStrategy.NewlineCount("a\nb\n\nc").ShouldBe(3);
    }
}
