// Ported from vscode (src/vs/editor/common/commands/shiftCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IShiftCommandOpts is the record ShiftCommandOpts;
//  - cachedStringRepeat keeps no module-level cache (no static mutable state here): CachedStringRepeat builds the
//    string each time;
//  - the language configuration service is a constructor parameter instead of an injected service.

using System.Text;
using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Commands;

/// <summary>The options of a <see cref="ShiftCommand"/>.</summary>
internal sealed record ShiftCommandOpts(
    bool IsUnshift,
    int TabSize,
    int IndentSize,
    bool InsertSpaces,
    bool UseTabStops,
    EditorAutoIndentStrategy AutoIndent);

/// <summary>Indents (Tab) or outdents (Shift+Tab) the lines of a selection.</summary>
internal sealed class ShiftCommand : ICommand
{
    // /^\s*$/, with JavaScript's \s and $.
    private static readonly Regex s_whitespaceOrEmpty = RegExps.CreateRegExp(@"^\s*$", isRegex: true, new RegExpOptions(MatchCase: true));

    private readonly ShiftCommandOpts _opts;
    private readonly TextSelection _selection;
    private readonly LanguageConfigurationService _languageConfigurationService;
    private string? _selectionId;
    private bool _useLastEditRangeForCursorEndPosition;
    private bool _selectionStartColumnStaysPut;

    public ShiftCommand(TextSelection range, ShiftCommandOpts opts, LanguageConfigurationService languageConfigurationService)
    {
        _opts = opts;
        _selection = range;
        _languageConfigurationService = languageConfigurationService;
        _selectionId = null;
        _useLastEditRangeForCursorEndPosition = false;
        _selectionStartColumnStaysPut = false;
    }

    private static string CachedStringRepeat(string str, int count)
    {
        if (count <= 0)
        {
            return "";
        }
        var result = new StringBuilder(str.Length * count);
        for (var i = 0; i < count; i++)
        {
            result.Append(str);
        }
        return result.ToString();
    }

    /// <summary>The indentation one level less deep than where the content of <paramref name="line"/> starts at <paramref name="column"/>.</summary>
    public static string UnshiftIndent(string line, int column, int tabSize, int indentSize, bool insertSpaces)
    {
        // Determine the visible column where the content starts
        var contentStartVisibleColumn = CursorColumns.VisibleColumnFromColumn(line, column, tabSize);

        if (insertSpaces)
        {
            var indent = CachedStringRepeat(" ", indentSize);
            var desiredTabStop = CursorColumns.PrevIndentTabStop(contentStartVisibleColumn, indentSize);
            var indentCount = desiredTabStop / indentSize; // will be an integer
            return CachedStringRepeat(indent, indentCount);
        }
        else
        {
            var indent = "\t";
            var desiredTabStop = CursorColumns.PrevRenderTabStop(contentStartVisibleColumn, tabSize);
            var indentCount = desiredTabStop / tabSize; // will be an integer
            return CachedStringRepeat(indent, indentCount);
        }
    }

    /// <summary>The indentation one level deeper than where the content of <paramref name="line"/> starts at <paramref name="column"/>.</summary>
    public static string ShiftIndent(string line, int column, int tabSize, int indentSize, bool insertSpaces)
    {
        // Determine the visible column where the content starts
        var contentStartVisibleColumn = CursorColumns.VisibleColumnFromColumn(line, column, tabSize);

        if (insertSpaces)
        {
            var indent = CachedStringRepeat(" ", indentSize);
            var desiredTabStop = CursorColumns.NextIndentTabStop(contentStartVisibleColumn, indentSize);
            var indentCount = desiredTabStop / indentSize; // will be an integer
            return CachedStringRepeat(indent, indentCount);
        }
        else
        {
            var indent = "\t";
            var desiredTabStop = CursorColumns.NextRenderTabStop(contentStartVisibleColumn, tabSize);
            var indentCount = desiredTabStop / tabSize; // will be an integer
            return CachedStringRepeat(indent, indentCount);
        }
    }

    private void AddEditOperation(IEditOperationBuilder builder, TextRange range, string text)
    {
        if (_useLastEditRangeForCursorEndPosition)
        {
            builder.AddTrackedEditOperation(range, text);
        }
        else
        {
            builder.AddEditOperation(range, text);
        }
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var startLine = _selection.StartLineNumber;

        var endLine = _selection.EndLineNumber;
        if (_selection.EndColumn == 1 && startLine != endLine)
        {
            endLine = endLine - 1;
        }

        var tabSize = _opts.TabSize;
        var indentSize = _opts.IndentSize;
        var insertSpaces = _opts.InsertSpaces;
        var shouldIndentEmptyLines = startLine == endLine;

        if (_opts.UseTabStops)
        {
            // if indenting or outdenting on a whitespace only line
            if (_selection.IsEmpty)
            {
                if (s_whitespaceOrEmpty.IsMatch(model.GetLineContent(startLine)))
                {
                    _useLastEditRangeForCursorEndPosition = true;
                }
            }

            // keep track of previous line's "miss-alignment"
            int previousLineExtraSpaces = 0, extraSpaces = 0;
            for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++, previousLineExtraSpaces = extraSpaces)
            {
                extraSpaces = 0;
                var lineText = model.GetLineContent(lineNumber);
                var indentationEndIndex = Strings.FirstNonWhitespaceIndex(lineText);

                if (_opts.IsUnshift && (lineText.Length == 0 || indentationEndIndex == 0))
                {
                    // empty line or line with no leading whitespace => nothing to do
                    continue;
                }

                if (!shouldIndentEmptyLines && !_opts.IsUnshift && lineText.Length == 0)
                {
                    // do not indent empty lines => nothing to do
                    continue;
                }

                if (indentationEndIndex == -1)
                {
                    // the entire line is whitespace
                    indentationEndIndex = lineText.Length;
                }

                if (lineNumber > 1)
                {
                    var contentStartVisibleColumn = CursorColumns.VisibleColumnFromColumn(lineText, indentationEndIndex + 1, tabSize);
                    if (contentStartVisibleColumn % indentSize != 0)
                    {
                        // The current line is "miss-aligned", so let's see if this is expected...
                        // This can only happen when it has trailing commas in the indent
                        if (model.Tokenization.IsCheapToTokenize(lineNumber - 1))
                        {
                            var enterAction = EnterActions.GetEnterAction(
                                _opts.AutoIndent,
                                model,
                                new TextRange(lineNumber - 1, model.GetLineMaxColumn(lineNumber - 1), lineNumber - 1, model.GetLineMaxColumn(lineNumber - 1)),
                                _languageConfigurationService);
                            if (enterAction is not null)
                            {
                                extraSpaces = previousLineExtraSpaces;
                                if (enterAction.AppendText.Length > 0)
                                {
                                    for (int j = 0, lenJ = enterAction.AppendText.Length; j < lenJ && extraSpaces < indentSize; j++)
                                    {
                                        if (enterAction.AppendText[j] == ' ')
                                        {
                                            extraSpaces++;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                                if (enterAction.RemoveText != 0)
                                {
                                    extraSpaces = Math.Max(0, extraSpaces - enterAction.RemoveText);
                                }

                                // Act as if `prefixSpaces` is not part of the indentation
                                for (var j = 0; j < extraSpaces; j++)
                                {
                                    if (indentationEndIndex == 0 || lineText[indentationEndIndex - 1] != ' ')
                                    {
                                        break;
                                    }
                                    indentationEndIndex--;
                                }
                            }
                        }
                    }
                }

                if (_opts.IsUnshift && indentationEndIndex == 0)
                {
                    // line with no leading whitespace => nothing to do
                    continue;
                }

                string desiredIndent;
                if (_opts.IsUnshift)
                {
                    desiredIndent = UnshiftIndent(lineText, indentationEndIndex + 1, tabSize, indentSize, insertSpaces);
                }
                else
                {
                    desiredIndent = ShiftIndent(lineText, indentationEndIndex + 1, tabSize, indentSize, insertSpaces);
                }

                AddEditOperation(builder, new TextRange(lineNumber, 1, lineNumber, indentationEndIndex + 1), desiredIndent);
                if (lineNumber == startLine && !_selection.IsEmpty)
                {
                    // Force the startColumn to stay put because we're inserting after it
                    _selectionStartColumnStaysPut = _selection.StartColumn <= indentationEndIndex + 1;
                }
            }
        }
        else
        {
            // if indenting or outdenting on a whitespace only line
            if (!_opts.IsUnshift && _selection.IsEmpty && model.GetLineLength(startLine) == 0)
            {
                _useLastEditRangeForCursorEndPosition = true;
            }

            var oneIndent = insertSpaces ? CachedStringRepeat(" ", indentSize) : "\t";

            for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                var lineText = model.GetLineContent(lineNumber);
                var indentationEndIndex = Strings.FirstNonWhitespaceIndex(lineText);

                if (_opts.IsUnshift && (lineText.Length == 0 || indentationEndIndex == 0))
                {
                    // empty line or line with no leading whitespace => nothing to do
                    continue;
                }

                if (!shouldIndentEmptyLines && !_opts.IsUnshift && lineText.Length == 0)
                {
                    // do not indent empty lines => nothing to do
                    continue;
                }

                if (indentationEndIndex == -1)
                {
                    // the entire line is whitespace
                    indentationEndIndex = lineText.Length;
                }

                if (_opts.IsUnshift && indentationEndIndex == 0)
                {
                    // line with no leading whitespace => nothing to do
                    continue;
                }

                if (_opts.IsUnshift)
                {
                    indentationEndIndex = Math.Min(indentationEndIndex, indentSize);
                    for (var i = 0; i < indentationEndIndex; i++)
                    {
                        var chr = lineText[i];
                        if (chr == '\t')
                        {
                            indentationEndIndex = i + 1;
                            break;
                        }
                    }

                    AddEditOperation(builder, new TextRange(lineNumber, 1, lineNumber, indentationEndIndex + 1), "");
                }
                else
                {
                    AddEditOperation(builder, new TextRange(lineNumber, 1, lineNumber, 1), oneIndent);
                    if (lineNumber == startLine && !_selection.IsEmpty)
                    {
                        // Force the startColumn to stay put because we're inserting after it
                        _selectionStartColumnStaysPut = _selection.StartColumn == 1;
                    }
                }
            }
        }

        _selectionId = builder.TrackSelection(_selection);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        if (_useLastEditRangeForCursorEndPosition)
        {
            var lastOp = helper.GetInverseEditOperations()[0];
            return new TextSelection(lastOp.Range.EndLineNumber, lastOp.Range.EndColumn, lastOp.Range.EndLineNumber, lastOp.Range.EndColumn);
        }
        var result = helper.GetTrackedSelection(_selectionId!);

        if (_selectionStartColumnStaysPut)
        {
            // The selection start should not move
            var initialStartColumn = _selection.StartColumn;
            var resultStartColumn = result.StartColumn;
            if (resultStartColumn <= initialStartColumn)
            {
                return result;
            }

            if (result.Direction == SelectionDirection.LeftToRight)
            {
                return new TextSelection(result.StartLineNumber, initialStartColumn, result.EndLineNumber, result.EndColumn);
            }
            return new TextSelection(result.EndLineNumber, result.EndColumn, result.StartLineNumber, initialStartColumn);
        }

        return result;
    }
}
