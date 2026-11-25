// Ported from vscode (src/vs/editor/browser/controller/editContext/screenReaderUtils.ts: ISimpleScreenReaderContentState,
// SimplePagedScreenReaderStrategy, newlinecount; and src/vs/editor/common/viewModel/screenReaderSimpleModel.ts:
// ISimpleModel; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - ISimpleModel is IScreenReaderSimpleModel (the view model namespace already has an ISimpleModel);
//    TextModelScreenReaderModel adapts a TextModel to it;
//  - the content state is a record (SimpleScreenReaderContentState); upstream's textarea selection offsets are kept in
//    the same direction-dependent form (selectionStart > selectionEnd for a right-to-left selection);
//  - ariaLabelForScreenReaderContent is not ported: the control has no accessibilitySupport option (an automation
//    peer exists only while assistive technology asks for one), so its label is always the ariaLabel.
//  - the control does not mirror this content into a hidden text area (there is none); it uses the strategy for the
//    text it announces (CodeEditor.Accessibility.cs).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Accessibility;

/// <summary>What the screen reader strategy reads from a model (upstream's ISimpleModel of screenReaderSimpleModel.ts).</summary>
internal interface IScreenReaderSimpleModel
{
    /// <summary>The text of a line.</summary>
    string GetLineContent(int lineNumber);

    /// <summary>The number of lines.</summary>
    int GetLineCount();

    /// <summary>The column after the last character of a line.</summary>
    int GetLineMaxColumn(int lineNumber);

    /// <summary>The text in a range.</summary>
    string GetValueInRange(TextRange range, EndOfLinePreference eol);

    /// <summary>The length of the text in a range.</summary>
    int GetValueLengthInRange(TextRange range, EndOfLinePreference eol);

    /// <summary>The position <paramref name="offset"/> characters away from a position.</summary>
    TextPosition ModifyPosition(TextPosition position, int offset);
}

/// <summary>A <see cref="TextModel"/> as an <see cref="IScreenReaderSimpleModel"/>.</summary>
internal sealed class TextModelScreenReaderModel(TextModel model) : IScreenReaderSimpleModel
{
    /// <inheritdoc />
    public string GetLineContent(int lineNumber) => model.GetLineContent(lineNumber);

    /// <inheritdoc />
    public int GetLineCount() => model.GetLineCount();

    /// <inheritdoc />
    public int GetLineMaxColumn(int lineNumber) => model.GetLineMaxColumn(lineNumber);

    /// <inheritdoc />
    public string GetValueInRange(TextRange range, EndOfLinePreference eol) => model.GetValueInRange(range, eol);

    /// <inheritdoc />
    public int GetValueLengthInRange(TextRange range, EndOfLinePreference eol) => model.GetValueLengthInRange(range, eol);

    /// <inheritdoc />
    public TextPosition ModifyPosition(TextPosition position, int offset) => model.ModifyPosition(position, offset);
}

/// <summary>The text a screen reader gets around the selection (upstream's ISimpleScreenReaderContentState).</summary>
/// <param name="Value">The text: the page (or pages) of lines around the selection.</param>
/// <param name="SelectionStart">The offset in <see cref="Value"/> where the selection starts (where it is anchored for a right-to-left selection, see upstream).</param>
/// <param name="SelectionEnd">The offset in <see cref="Value"/> where the selection ends.</param>
/// <param name="Selection">The editor selection the offsets stand for.</param>
/// <param name="StartPositionWithinEditor">The position of the start of <see cref="Value"/> in the editor.</param>
/// <param name="NewlineCountBeforeSelection">The number of line breaks in <see cref="Value"/> before the selection.</param>
internal sealed record SimpleScreenReaderContentState(
    string Value,
    int SelectionStart,
    int SelectionEnd,
    TextSelection Selection,
    TextPosition StartPositionWithinEditor,
    int NewlineCountBeforeSelection)
{
    /// <summary>The selected part of <see cref="Value"/> (possibly cut in the middle with an ellipsis for a long selection).</summary>
    public string SelectedText => Value.Substring(Math.Min(SelectionStart, SelectionEnd), Math.Abs(SelectionEnd - SelectionStart));
}

/// <summary>
/// Pages of lines for a screen reader (upstream's SimplePagedScreenReaderStrategy): the page of <c>linesPerPage</c> lines
/// that holds the selection's start, up to the page that holds its end, with long texts cut at 500 characters each side.
/// </summary>
internal static class SimplePagedScreenReaderStrategy
{
    // Chromium handles very poorly text even of a few thousand chars (upstream's reason; screen readers do too)
    private const int LimitChars = 500;

    private const char Ellipsis = '…';

    private static int GetPageOfLine(int lineNumber, int linesPerPage) => (lineNumber - 1) / linesPerPage;

    private static TextRange GetRangeForPage(int page, int linesPerPage)
    {
        var offset = page * linesPerPage;
        var startLineNumber = offset + 1;
        var endLineNumber = offset + linesPerPage;
        return new TextRange(startLineNumber, 1, endLineNumber + 1, 1);
    }

    /// <summary>The content for <paramref name="selection"/> (upstream's fromEditorSelection).</summary>
    public static SimpleScreenReaderContentState FromEditorSelection(IScreenReaderSimpleModel model, TextSelection selection, int linesPerPage, bool trimLongText)
    {
        var selectionStartPage = GetPageOfLine(selection.StartLineNumber, linesPerPage);
        var selectionStartPageRange = GetRangeForPage(selectionStartPage, linesPerPage);

        var selectionEndPage = GetPageOfLine(selection.EndLineNumber, linesPerPage);
        var selectionEndPageRange = GetRangeForPage(selectionEndPage, linesPerPage);

        var pretextRange = selectionStartPageRange.IntersectRanges(new TextRange(1, 1, selection.StartLineNumber, selection.StartColumn))!.Value;
        if (trimLongText && model.GetValueLengthInRange(pretextRange, EndOfLinePreference.LF) > LimitChars)
        {
            var pretextStart = model.ModifyPosition(pretextRange.End, -LimitChars);
            pretextRange = new TextRange(pretextStart, pretextRange.End);
        }

        var pretext = model.GetValueInRange(pretextRange, EndOfLinePreference.LF);

        var lastLine = model.GetLineCount();
        var lastLineMaxColumn = model.GetLineMaxColumn(lastLine);
        var posttextRange = selectionEndPageRange.IntersectRanges(new TextRange(selection.EndLineNumber, selection.EndColumn, lastLine, lastLineMaxColumn))!.Value;
        if (trimLongText && model.GetValueLengthInRange(posttextRange, EndOfLinePreference.LF) > LimitChars)
        {
            var posttextEnd = model.ModifyPosition(posttextRange.Start, LimitChars);
            posttextRange = new TextRange(posttextRange.Start, posttextEnd);
        }

        var posttext = model.GetValueInRange(posttextRange, EndOfLinePreference.LF);

        string text;
        if (selectionStartPage == selectionEndPage || selectionStartPage + 1 == selectionEndPage)
        {
            // take full selection
            text = model.GetValueInRange(selection.Range, EndOfLinePreference.LF);
        }
        else
        {
            var selectionRange1 = selectionStartPageRange.IntersectRanges(selection.Range)!.Value;
            var selectionRange2 = selectionEndPageRange.IntersectRanges(selection.Range)!.Value;
            text = model.GetValueInRange(selectionRange1, EndOfLinePreference.LF)
                + Ellipsis
                + model.GetValueInRange(selectionRange2, EndOfLinePreference.LF);
        }

        if (trimLongText && text.Length > 2 * LimitChars)
        {
            text = string.Concat(text.AsSpan(0, LimitChars), Ellipsis.ToString(), text.AsSpan(text.Length - LimitChars, LimitChars));
        }

        int selectionStart;
        int selectionEnd;
        if (selection.Direction == SelectionDirection.LeftToRight)
        {
            selectionStart = pretext.Length;
            selectionEnd = pretext.Length + text.Length;
        }
        else
        {
            selectionEnd = pretext.Length;
            selectionStart = pretext.Length + text.Length;
        }

        return new SimpleScreenReaderContentState(
            pretext + text + posttext,
            selectionStart,
            selectionEnd,
            selection,
            pretextRange.Start,
            pretextRange.EndLineNumber - pretextRange.StartLineNumber);
    }

    /// <summary>The number of <c>\n</c> in <paramref name="text"/> (upstream's newlinecount).</summary>
    public static int NewlineCount(string text)
    {
        var result = 0;
        var startIndex = -1;
        while (true)
        {
            startIndex = text.IndexOf('\n', startIndex + 1);
            if (startIndex == -1)
            {
                break;
            }

            result++;
        }

        return result;
    }
}
