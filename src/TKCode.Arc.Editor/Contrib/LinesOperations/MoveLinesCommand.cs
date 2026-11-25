// Ported from vscode (src/vs/editor/contrib/linesOperations/browser/moveLinesCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the virtual model upstream builds from closures is the nested class MappedVirtualModel;
//  - trimStart (/^\s+/) trims the characters .NET considers white space.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Contrib.LinesOperations;

/// <summary>Moves the lines of a selection up or down by one line (Alt+Up / Down), adjusting their indentation.</summary>
internal sealed class MoveLinesCommand : ICommand
{
    private readonly TextSelection _selection;
    private readonly bool _isMovingDown;
    private readonly EditorAutoIndentStrategy _autoIndent;
    private readonly LanguageConfigurationService _languageConfigurationService;

    private string? _selectionId;
    private bool _moveEndPositionDown;
    private bool _moveEndLineSelectionShrink;

    public MoveLinesCommand(
        TextSelection selection,
        bool isMovingDown,
        EditorAutoIndentStrategy autoIndent,
        LanguageConfigurationService languageConfigurationService)
    {
        _selection = selection;
        _isMovingDown = isMovingDown;
        _autoIndent = autoIndent;
        _languageConfigurationService = languageConfigurationService;
        _selectionId = null;
        _moveEndLineSelectionShrink = false;
    }

    private static IVirtualModel CreateVirtualModel(
        TextModel model,
        Func<int, int> lineNumberMapper,
        Func<int, string?>? contentOverride = null) =>
        new MappedVirtualModel(model, lineNumberMapper, contentOverride);

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var modelLineCount = model.GetLineCount();

        if (_isMovingDown && _selection.EndLineNumber == modelLineCount)
        {
            _selectionId = builder.TrackSelection(_selection);
            return;
        }
        if (!_isMovingDown && _selection.StartLineNumber == 1)
        {
            _selectionId = builder.TrackSelection(_selection);
            return;
        }

        _moveEndPositionDown = false;
        var s = _selection;

        if (s.StartLineNumber < s.EndLineNumber && s.EndColumn == 1)
        {
            _moveEndPositionDown = true;
            s = s.SetEndPosition(s.EndLineNumber - 1, model.GetLineMaxColumn(s.EndLineNumber - 1));
        }

        var options = model.GetOptions();
        var tabSize = options.TabSize;
        var indentSize = options.IndentSize;
        var insertSpaces = options.InsertSpaces;
        var indentConverter = BuildIndentConverter(tabSize, indentSize, insertSpaces);

        if (s.StartLineNumber == s.EndLineNumber && model.GetLineMaxColumn(s.StartLineNumber) == 1)
        {
            // Current line is empty
            var lineNumber = s.StartLineNumber;
            var otherLineNumber = _isMovingDown ? lineNumber + 1 : lineNumber - 1;

            if (model.GetLineMaxColumn(otherLineNumber) == 1)
            {
                // Other line number is empty too, so no editing is needed
                // Add a no-op to force running by the model
                builder.AddEditOperation(new TextRange(1, 1, 1, 1), null);
            }
            else
            {
                // Type content from other line number on line number
                builder.AddEditOperation(new TextRange(lineNumber, 1, lineNumber, 1), model.GetLineContent(otherLineNumber));

                // Remove content from other line number
                builder.AddEditOperation(new TextRange(otherLineNumber, 1, otherLineNumber, model.GetLineMaxColumn(otherLineNumber)), null);
            }
            // Track selection at the other line number
            s = new TextSelection(otherLineNumber, 1, otherLineNumber, 1);
        }
        else
        {
            int movingLineNumber;
            string movingLineText;

            if (_isMovingDown)
            {
                movingLineNumber = s.EndLineNumber + 1;
                movingLineText = model.GetLineContent(movingLineNumber);
                // Delete line that needs to be moved
                builder.AddEditOperation(new TextRange(movingLineNumber - 1, model.GetLineMaxColumn(movingLineNumber - 1), movingLineNumber, model.GetLineMaxColumn(movingLineNumber)), null);

                var insertingText = movingLineText;

                if (ShouldAutoIndent(model, s))
                {
                    var movingLineMatchResult = MatchEnterRule(model, indentConverter, tabSize, movingLineNumber, s.StartLineNumber - 1);
                    // if s.startLineNumber - 1 matches onEnter rule, we still honor that.
                    if (movingLineMatchResult is { } matchResult)
                    {
                        var oldIndentation = AutoIndent.GetLeadingWhitespace(model.GetLineContent(movingLineNumber));
                        var newSpaceCnt = matchResult + IndentUtils.GetSpaceCnt(oldIndentation, tabSize);
                        var newIndentation = IndentUtils.GenerateIndent(newSpaceCnt, tabSize, insertSpaces);
                        insertingText = newIndentation + TrimStart(movingLineText);
                    }
                    else
                    {
                        // no enter rule matches, let's check indentatin rules then.
                        var startLine = s.StartLineNumber;
                        var virtualModel = CreateVirtualModel(
                            model,
                            ln => ln == startLine ? movingLineNumber : ln);
                        var indentOfMovingLine = AutoIndent.GetGoodIndentForLine(
                            _autoIndent,
                            virtualModel,
                            model.GetLanguageIdAtPosition(movingLineNumber, 1),
                            s.StartLineNumber,
                            indentConverter,
                            _languageConfigurationService);
                        if (indentOfMovingLine is not null)
                        {
                            var oldIndentation = AutoIndent.GetLeadingWhitespace(model.GetLineContent(movingLineNumber));
                            var newSpaceCnt = IndentUtils.GetSpaceCnt(indentOfMovingLine, tabSize);
                            var oldSpaceCnt = IndentUtils.GetSpaceCnt(oldIndentation, tabSize);
                            if (newSpaceCnt != oldSpaceCnt)
                            {
                                var newIndentation = IndentUtils.GenerateIndent(newSpaceCnt, tabSize, insertSpaces);
                                insertingText = newIndentation + TrimStart(movingLineText);
                            }
                        }
                    }

                    // add edit operations for moving line first to make sure it's executed after we make indentation change
                    // to s.startLineNumber
                    builder.AddEditOperation(new TextRange(s.StartLineNumber, 1, s.StartLineNumber, 1), insertingText + "\n");

                    var ret = MatchEnterRuleMovingDown(model, indentConverter, tabSize, s.StartLineNumber, movingLineNumber, insertingText);

                    // check if the line being moved before matches onEnter rules, if so let's adjust the indentation by onEnter rules.
                    if (ret is { } offset)
                    {
                        if (offset != 0)
                        {
                            GetIndentEditsOfMovingBlock(model, builder, s, tabSize, insertSpaces, offset);
                        }
                    }
                    else
                    {
                        // it doesn't match onEnter rules, let's check indentation rules then.
                        var sel = s;
                        var text = insertingText;
                        var virtualModel = CreateVirtualModel(
                            model,
                            ln =>
                            {
                                if (ln == sel.StartLineNumber)
                                {
                                    // TODO@aiday-mar: the tokens here don't correspond exactly to the corresponding content (after indentation adjustment), have to fix this.
                                    return movingLineNumber;
                                }
                                else if (ln >= sel.StartLineNumber + 1 && ln <= sel.EndLineNumber + 1)
                                {
                                    return ln - 1;
                                }
                                else
                                {
                                    return ln;
                                }
                            },
                            ln => ln == sel.StartLineNumber ? text : null);

                        var newIndentatOfMovingBlock = AutoIndent.GetGoodIndentForLine(
                            _autoIndent,
                            virtualModel,
                            model.GetLanguageIdAtPosition(movingLineNumber, 1),
                            s.StartLineNumber + 1,
                            indentConverter,
                            _languageConfigurationService);

                        if (newIndentatOfMovingBlock is not null)
                        {
                            var oldIndentation = AutoIndent.GetLeadingWhitespace(model.GetLineContent(s.StartLineNumber));
                            var newSpaceCnt = IndentUtils.GetSpaceCnt(newIndentatOfMovingBlock, tabSize);
                            var oldSpaceCnt = IndentUtils.GetSpaceCnt(oldIndentation, tabSize);
                            if (newSpaceCnt != oldSpaceCnt)
                            {
                                var spaceCntOffset = newSpaceCnt - oldSpaceCnt;

                                GetIndentEditsOfMovingBlock(model, builder, s, tabSize, insertSpaces, spaceCntOffset);
                            }
                        }
                    }
                }
                else
                {
                    // Insert line that needs to be moved before
                    builder.AddEditOperation(new TextRange(s.StartLineNumber, 1, s.StartLineNumber, 1), insertingText + "\n");
                }
            }
            else
            {
                movingLineNumber = s.StartLineNumber - 1;
                movingLineText = model.GetLineContent(movingLineNumber);

                // Delete line that needs to be moved
                builder.AddEditOperation(new TextRange(movingLineNumber, 1, movingLineNumber + 1, 1), null);

                // Insert line that needs to be moved after
                builder.AddEditOperation(new TextRange(s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber), s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber)), "\n" + movingLineText);

                if (ShouldAutoIndent(model, s))
                {
                    var startLine = s.StartLineNumber;
                    var virtualModel = CreateVirtualModel(
                        model,
                        ln => ln == movingLineNumber ? startLine : ln);

                    var ret = MatchEnterRule(model, indentConverter, tabSize, s.StartLineNumber, s.StartLineNumber - 2);
                    // check if s.startLineNumber - 2 matches onEnter rules, if so adjust the moving block by onEnter rules.
                    if (ret is { } offset)
                    {
                        if (offset != 0)
                        {
                            GetIndentEditsOfMovingBlock(model, builder, s, tabSize, insertSpaces, offset);
                        }
                    }
                    else
                    {
                        // it doesn't match any onEnter rule, let's check indentation rules then.
                        var indentOfFirstLine = AutoIndent.GetGoodIndentForLine(
                            _autoIndent,
                            virtualModel,
                            model.GetLanguageIdAtPosition(s.StartLineNumber, 1),
                            movingLineNumber,
                            indentConverter,
                            _languageConfigurationService);
                        if (indentOfFirstLine is not null)
                        {
                            // adjust the indentation of the moving block
                            var oldIndent = AutoIndent.GetLeadingWhitespace(model.GetLineContent(s.StartLineNumber));
                            var newSpaceCnt = IndentUtils.GetSpaceCnt(indentOfFirstLine, tabSize);
                            var oldSpaceCnt = IndentUtils.GetSpaceCnt(oldIndent, tabSize);
                            if (newSpaceCnt != oldSpaceCnt)
                            {
                                var spaceCntOffset = newSpaceCnt - oldSpaceCnt;

                                GetIndentEditsOfMovingBlock(model, builder, s, tabSize, insertSpaces, spaceCntOffset);
                            }
                        }
                    }
                }
            }
        }

        _selectionId = builder.TrackSelection(s);
    }

    private static IIndentConverter BuildIndentConverter(int tabSize, int indentSize, bool insertSpaces) =>
        new IndentConverter(tabSize, indentSize, insertSpaces);

    private int? ParseEnterResult(TextModel model, IIndentConverter indentConverter, int tabSize, int line, CompleteEnterAction? enter)
    {
        if (enter is not null)
        {
            var enterPrefix = enter.Indentation;

            if (enter.IndentAction == IndentAction.None)
            {
                enterPrefix = enter.Indentation + enter.AppendText;
            }
            else if (enter.IndentAction == IndentAction.Indent)
            {
                enterPrefix = enter.Indentation + enter.AppendText;
            }
            else if (enter.IndentAction == IndentAction.IndentOutdent)
            {
                enterPrefix = enter.Indentation;
            }
            else if (enter.IndentAction == IndentAction.Outdent)
            {
                enterPrefix = indentConverter.UnshiftIndent(enter.Indentation) + enter.AppendText;
            }
            var movingLineText = model.GetLineContent(line);
            if (TrimStart(movingLineText).IndexOf(TrimStart(enterPrefix), StringComparison.Ordinal) >= 0)
            {
                var oldIndentation = AutoIndent.GetLeadingWhitespace(model.GetLineContent(line));
                var newIndentation = AutoIndent.GetLeadingWhitespace(enterPrefix);
                var indentMetadataOfMovelingLine = AutoIndent.GetIndentMetadata(model, line, _languageConfigurationService);
                if (indentMetadataOfMovelingLine is { } metadata && (metadata & IndentConsts.DecreaseMask) != 0)
                {
                    newIndentation = indentConverter.UnshiftIndent(newIndentation);
                }
                var newSpaceCnt = IndentUtils.GetSpaceCnt(newIndentation, tabSize);
                var oldSpaceCnt = IndentUtils.GetSpaceCnt(oldIndentation, tabSize);
                return newSpaceCnt - oldSpaceCnt;
            }
        }

        return null;
    }

    // line: the line moving down; futureAboveLineNumber: the line which will be at the `line` position
    private int? MatchEnterRuleMovingDown(TextModel model, IIndentConverter indentConverter, int tabSize, int line, int futureAboveLineNumber, string futureAboveLineText)
    {
        if (Strings.LastNonWhitespaceIndex(futureAboveLineText) >= 0)
        {
            // break
            var maxColumn = model.GetLineMaxColumn(futureAboveLineNumber);
            var enter = EnterActions.GetEnterAction(_autoIndent, model, new TextRange(futureAboveLineNumber, maxColumn, futureAboveLineNumber, maxColumn), _languageConfigurationService);
            return ParseEnterResult(model, indentConverter, tabSize, line, enter);
        }
        else
        {
            // go upwards, starting from `line - 1`
            var validPrecedingLine = line - 1;
            while (validPrecedingLine >= 1)
            {
                var lineContent = model.GetLineContent(validPrecedingLine);
                var nonWhitespaceIdx = Strings.LastNonWhitespaceIndex(lineContent);

                if (nonWhitespaceIdx >= 0)
                {
                    break;
                }

                validPrecedingLine--;
            }

            if (validPrecedingLine < 1 || line > model.GetLineCount())
            {
                return null;
            }

            var maxColumn = model.GetLineMaxColumn(validPrecedingLine);
            var enter = EnterActions.GetEnterAction(_autoIndent, model, new TextRange(validPrecedingLine, maxColumn, validPrecedingLine, maxColumn), _languageConfigurationService);
            return ParseEnterResult(model, indentConverter, tabSize, line, enter);
        }
    }

    private int? MatchEnterRule(TextModel model, IIndentConverter indentConverter, int tabSize, int line, int oneLineAbove, string? previousLineText = null)
    {
        var validPrecedingLine = oneLineAbove;
        while (validPrecedingLine >= 1)
        {
            // ship empty lines as empty lines just inherit indentation
            string lineContent;
            if (validPrecedingLine == oneLineAbove && previousLineText is not null)
            {
                lineContent = previousLineText;
            }
            else
            {
                lineContent = model.GetLineContent(validPrecedingLine);
            }

            var nonWhitespaceIdx = Strings.LastNonWhitespaceIndex(lineContent);
            if (nonWhitespaceIdx >= 0)
            {
                break;
            }
            validPrecedingLine--;
        }

        if (validPrecedingLine < 1 || line > model.GetLineCount())
        {
            return null;
        }

        var maxColumn = model.GetLineMaxColumn(validPrecedingLine);
        var enter = EnterActions.GetEnterAction(_autoIndent, model, new TextRange(validPrecedingLine, maxColumn, validPrecedingLine, maxColumn), _languageConfigurationService);
        return ParseEnterResult(model, indentConverter, tabSize, line, enter);
    }

    private static string TrimStart(string str) => str.TrimStart();

    private bool ShouldAutoIndent(TextModel model, TextSelection selection)
    {
        if (_autoIndent < EditorAutoIndentStrategy.Full)
        {
            return false;
        }
        // if it's not easy to tokenize, we stop auto indent.
        if (!model.Tokenization.IsCheapToTokenize(selection.StartLineNumber))
        {
            return false;
        }
        var languageAtSelectionStart = model.GetLanguageIdAtPosition(selection.StartLineNumber, 1);
        var languageAtSelectionEnd = model.GetLanguageIdAtPosition(selection.EndLineNumber, 1);

        if (languageAtSelectionStart != languageAtSelectionEnd)
        {
            return false;
        }

        if (_languageConfigurationService.GetLanguageConfiguration(languageAtSelectionStart).IndentRulesSupport is null)
        {
            return false;
        }

        return true;
    }

    private void GetIndentEditsOfMovingBlock(TextModel model, IEditOperationBuilder builder, TextSelection s, int tabSize, bool insertSpaces, int offset)
    {
        for (var i = s.StartLineNumber; i <= s.EndLineNumber; i++)
        {
            var lineContent = model.GetLineContent(i);
            var originalIndent = AutoIndent.GetLeadingWhitespace(lineContent);
            var originalSpacesCnt = IndentUtils.GetSpaceCnt(originalIndent, tabSize);
            var newSpacesCnt = originalSpacesCnt + offset;
            var newIndent = IndentUtils.GenerateIndent(newSpacesCnt, tabSize, insertSpaces);

            if (newIndent != originalIndent)
            {
                builder.AddEditOperation(new TextRange(i, 1, i, originalIndent.Length + 1), newIndent);

                if (i == s.EndLineNumber && s.EndColumn <= originalIndent.Length + 1 && newIndent.Length == 0)
                {
                    // as users select part of the original indent white spaces
                    // when we adjust the indentation of endLine, we should adjust the cursor position as well.
                    _moveEndLineSelectionShrink = true;
                }
            }
        }
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var result = helper.GetTrackedSelection(_selectionId!);

        if (_moveEndPositionDown)
        {
            result = result.SetEndPosition(result.EndLineNumber + 1, 1);
        }

        if (_moveEndLineSelectionShrink && result.StartLineNumber < result.EndLineNumber)
        {
            result = result.SetEndPosition(result.EndLineNumber, 2);
        }

        return result;
    }

    /// <summary>Shifts and unshifts by one level with the model's options (upstream's buildIndentConverter).</summary>
    internal sealed class IndentConverter(int tabSize, int indentSize, bool insertSpaces) : IIndentConverter
    {
        public string ShiftIndent(string indentation) =>
            ShiftCommand.ShiftIndent(indentation, indentation.Length + 1, tabSize, indentSize, insertSpaces);

        public string UnshiftIndent(string indentation) =>
            ShiftCommand.UnshiftIndent(indentation, indentation.Length + 1, tabSize, indentSize, insertSpaces);
    }

    /// <summary>The model with its lines mapped (and some replaced) for the indentation rules.</summary>
    internal sealed class MappedVirtualModel : IVirtualModel, IVirtualModelTokenization
    {
        private readonly TextModel _model;
        private readonly Func<int, int> _lineNumberMapper;
        private readonly Func<int, string?>? _contentOverride;

        public MappedVirtualModel(TextModel model, Func<int, int> lineNumberMapper, Func<int, string?>? contentOverride)
        {
            _model = model;
            _lineNumberMapper = lineNumberMapper;
            _contentOverride = contentOverride;
        }

        public IVirtualModelTokenization Tokenization => this;

        public string GetLineContent(int lineNumber)
        {
            var customContent = _contentOverride?.Invoke(lineNumber);
            if (customContent is not null)
            {
                return customContent;
            }
            return _model.GetLineContent(_lineNumberMapper(lineNumber));
        }

        public IViewLineTokens GetLineTokens(int lineNumber) => _model.Tokenization.GetLineTokens(_lineNumberMapper(lineNumber));

        public string GetLanguageId() => _model.GetLanguageId();

        public string GetLanguageIdAtPosition(int lineNumber, int column) => _model.GetLanguageIdAtPosition(lineNumber, column);

        public void ForceTokenization(int lineNumber)
        {
        }
    }
}
