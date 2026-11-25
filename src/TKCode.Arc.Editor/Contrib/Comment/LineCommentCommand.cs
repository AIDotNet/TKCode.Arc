// Ported from vscode (src/vs/editor/contrib/comment/browser/lineCommentCommand.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IInsertionPoint and ILinePreflightData are the class LinePreflightData (mutable, as upstream mutates them);
//    IPreflightData is PreflightData with Supported false for upstream's IPreflightDataUnsupported;
//  - ISimpleModel is ILineCommentSimpleModel (a TextModel is wrapped by TextModelLines);
//  - Constants.MAX_SAFE_SMALL_INTEGER is int.MaxValue >> 1.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Comment;

/// <summary>What a line comment command does (upstream's Type).</summary>
internal enum LineCommentType
{
    Toggle = 0,
    ForceAdd = 1,
    ForceRemove = 2,
}

/// <summary>A line's comment state while toggling (upstream's ILinePreflightData and IInsertionPoint).</summary>
internal sealed class LinePreflightData
{
    public bool Ignore { get; set; }

    public string CommentStr { get; set; } = "";

    public int CommentStrOffset { get; set; }

    public int CommentStrLength { get; set; }
}

/// <summary>The result of the analysis of the lines (upstream's IPreflightData).</summary>
internal sealed class PreflightData(bool supported, bool shouldRemoveComments, IReadOnlyList<LinePreflightData> lines)
{
    public bool Supported { get; } = supported;
    public bool ShouldRemoveComments { get; } = shouldRemoveComments;
    public IReadOnlyList<LinePreflightData> Lines { get; } = lines;

    public static PreflightData Unsupported { get; } = new(false, false, []);
}

/// <summary>The lines a comment command reads (upstream's ISimpleModel).</summary>
internal interface ILineCommentSimpleModel
{
    string GetLineContent(int lineNumber);
}

/// <summary>Toggles, adds or removes line comments on the lines of a selection (Ctrl+/).</summary>
internal sealed class LineCommentCommand : ICommand
{
    private const int MaxSafeSmallInteger = int.MaxValue >> 1;

    private readonly LanguageConfigurationService _languageConfigurationService;
    private readonly TextSelection _selection;
    private readonly int _indentSize;
    private readonly LineCommentType _type;
    private readonly bool _insertSpace;
    private readonly bool _ignoreEmptyLines;
    private string? _selectionId;
    private int _deltaColumn;
    private bool _moveEndPositionDown;
    private readonly bool _ignoreFirstLine;

    public LineCommentCommand(
        LanguageConfigurationService languageConfigurationService,
        TextSelection selection,
        int indentSize,
        LineCommentType type,
        bool insertSpace,
        bool ignoreEmptyLines,
        bool ignoreFirstLine = false)
    {
        _languageConfigurationService = languageConfigurationService;
        _selection = selection;
        _indentSize = indentSize;
        _type = type;
        _insertSpace = insertSpace;
        _selectionId = null;
        _deltaColumn = 0;
        _moveEndPositionDown = false;
        _ignoreEmptyLines = ignoreEmptyLines;
        _ignoreFirstLine = ignoreFirstLine;
    }

    /// <summary>
    /// Do an initial pass over the lines and gather info about the line comment string.
    /// Returns null if any of the lines doesn't support a line comment string.
    /// </summary>
    private static List<LinePreflightData>? GatherPreflightCommentStrings(TextModel model, int startLineNumber, int endLineNumber, LanguageConfigurationService languageConfigurationService)
    {
        model.Tokenization.TokenizeIfCheap(startLineNumber);
        var languageId = model.GetLanguageIdAtPosition(startLineNumber, 1);

        var config = languageConfigurationService.GetLanguageConfiguration(languageId).Comments;
        var commentStr = config?.LineCommentToken;
        if (string.IsNullOrEmpty(commentStr))
        {
            // Mode does not support line comments
            return null;
        }

        var lines = new List<LinePreflightData>();
        for (int i = 0, lineCount = endLineNumber - startLineNumber + 1; i < lineCount; i++)
        {
            lines.Add(new LinePreflightData
            {
                Ignore = false,
                CommentStr = commentStr,
                CommentStrOffset = 0,
                CommentStrLength = commentStr.Length,
            });
        }

        return lines;
    }

    /// <summary>
    /// Analyze lines and decide which lines are relevant and what the toggle should do.
    /// Also, build up several offsets and lengths useful in the generation of editor operations.
    /// </summary>
    public static PreflightData AnalyzeLines(LineCommentType type, bool insertSpace, ILineCommentSimpleModel model, IReadOnlyList<LinePreflightData> lines, int startLineNumber, bool ignoreEmptyLines, bool ignoreFirstLine, LanguageConfigurationService languageConfigurationService, string languageId)
    {
        var onlyWhitespaceLines = true;

        var config = languageConfigurationService.GetLanguageConfiguration(languageId).Comments;
        var lineCommentNoIndent = config?.LineCommentNoIndent ?? false;

        bool shouldRemoveComments;
        if (type == LineCommentType.Toggle)
        {
            shouldRemoveComments = true;
        }
        else if (type == LineCommentType.ForceAdd)
        {
            shouldRemoveComments = false;
        }
        else
        {
            shouldRemoveComments = true;
        }

        for (int i = 0, lineCount = lines.Count; i < lineCount; i++)
        {
            var lineData = lines[i];
            var lineNumber = startLineNumber + i;

            if (lineNumber == startLineNumber && ignoreFirstLine)
            {
                // first line ignored
                lineData.Ignore = true;
                continue;
            }

            var lineContent = model.GetLineContent(lineNumber);
            var lineContentStartOffset = Strings.FirstNonWhitespaceIndex(lineContent);

            if (lineContentStartOffset == -1)
            {
                // Empty or whitespace only line
                lineData.Ignore = ignoreEmptyLines;
                lineData.CommentStrOffset = lineCommentNoIndent ? 0 : lineContent.Length;
                continue;
            }

            onlyWhitespaceLines = false;
            var offset = lineCommentNoIndent ? 0 : lineContentStartOffset;
            lineData.Ignore = false;
            lineData.CommentStrOffset = offset;

            if (shouldRemoveComments && !BlockCommentCommand.HaystackHasNeedleAtOffset(lineContent, lineData.CommentStr, offset))
            {
                if (type == LineCommentType.Toggle)
                {
                    // Every line so far has been a line comment, but this one is not
                    shouldRemoveComments = false;
                }
                else if (type == LineCommentType.ForceAdd)
                {
                    // Will not happen
                }
                else
                {
                    lineData.Ignore = true;
                }
            }

            if (shouldRemoveComments && insertSpace)
            {
                // Remove a following space if present
                var commentStrEndOffset = lineContentStartOffset + lineData.CommentStrLength;
                if (commentStrEndOffset < lineContent.Length && lineContent[commentStrEndOffset] == ' ')
                {
                    lineData.CommentStrLength += 1;
                }
            }
        }

        if (type == LineCommentType.Toggle && onlyWhitespaceLines)
        {
            // For only whitespace lines, we insert comments
            shouldRemoveComments = false;

            // Also, no longer ignore them
            for (int i = 0, lineCount = lines.Count; i < lineCount; i++)
            {
                lines[i].Ignore = false;
            }
        }

        return new PreflightData(true, shouldRemoveComments, lines);
    }

    /// <summary>Analyze all lines and decide exactly what to do => not supported | insert line comments | remove line comments.</summary>
    public static PreflightData GatherPreflightData(LineCommentType type, bool insertSpace, TextModel model, int startLineNumber, int endLineNumber, bool ignoreEmptyLines, bool ignoreFirstLine, LanguageConfigurationService languageConfigurationService)
    {
        var lines = GatherPreflightCommentStrings(model, startLineNumber, endLineNumber, languageConfigurationService);
        var languageId = model.GetLanguageIdAtPosition(startLineNumber, 1);
        if (lines is null)
        {
            return PreflightData.Unsupported;
        }

        return AnalyzeLines(type, insertSpace, new TextModelLines(model), lines, startLineNumber, ignoreEmptyLines, ignoreFirstLine, languageConfigurationService, languageId);
    }

    /// <summary>Given a successful analysis, execute either insert line comments, either remove line comments.</summary>
    private void ExecuteLineComments(ILineCommentSimpleModel model, IEditOperationBuilder builder, PreflightData data, TextSelection s)
    {
        List<SingleEditOperation> ops;

        if (data.ShouldRemoveComments)
        {
            ops = CreateRemoveLineCommentsOperations(data.Lines, s.StartLineNumber);
        }
        else
        {
            NormalizeInsertionPoint(model, data.Lines, s.StartLineNumber, _indentSize);
            ops = CreateAddLineCommentsOperations(data.Lines, s.StartLineNumber);
        }

        var cursorPosition = new TextPosition(s.PositionLineNumber, s.PositionColumn);

        for (int i = 0, len = ops.Count; i < len; i++)
        {
            builder.AddEditOperation(ops[i].Range, ops[i].Text);
            if (ops[i].Range.IsEmpty && ops[i].Range.Start == cursorPosition)
            {
                var lineContent = model.GetLineContent(cursorPosition.LineNumber);
                if (lineContent.Length + 1 == cursorPosition.Column)
                {
                    _deltaColumn = (ops[i].Text ?? "").Length;
                }
            }
        }

        _selectionId = builder.TrackSelection(s);
    }

    private static List<SingleEditOperation>? AttemptRemoveBlockComment(TextModel model, TextSelection s, string startToken, string endToken)
    {
        var startLineNumber = s.StartLineNumber;
        var endLineNumber = s.EndLineNumber;

        var startTokenAllowedBeforeColumn = endToken.Length + Math.Max(
            model.GetLineFirstNonWhitespaceColumn(s.StartLineNumber),
            s.StartColumn);

        var startTokenIndex = BlockCommentCommand.JsLastIndexOf(model.GetLineContent(startLineNumber), startToken, startTokenAllowedBeforeColumn - 1);
        var endTokenIndex = BlockCommentCommand.JsIndexOf(model.GetLineContent(endLineNumber), endToken, s.EndColumn - 1 - startToken.Length);

        if (startTokenIndex != -1 && endTokenIndex == -1)
        {
            endTokenIndex = BlockCommentCommand.JsIndexOf(model.GetLineContent(startLineNumber), endToken, startTokenIndex + startToken.Length);
            endLineNumber = startLineNumber;
        }

        if (startTokenIndex == -1 && endTokenIndex != -1)
        {
            startTokenIndex = BlockCommentCommand.JsLastIndexOf(model.GetLineContent(endLineNumber), startToken, endTokenIndex);
            startLineNumber = endLineNumber;
        }

        if (s.IsEmpty && (startTokenIndex == -1 || endTokenIndex == -1))
        {
            startTokenIndex = model.GetLineContent(startLineNumber).IndexOf(startToken, StringComparison.Ordinal);
            if (startTokenIndex != -1)
            {
                endTokenIndex = BlockCommentCommand.JsIndexOf(model.GetLineContent(startLineNumber), endToken, startTokenIndex + startToken.Length);
            }
        }

        // We have to adjust to possible inner white space.
        // For Space after startToken, add Space to startToken - range math will work out.
        if (startTokenIndex != -1 && CharCodeAt(model.GetLineContent(startLineNumber), startTokenIndex + startToken.Length) == ' ')
        {
            startToken += " ";
        }

        // For Space before endToken, add Space before endToken and shift index one left.
        if (endTokenIndex != -1 && CharCodeAt(model.GetLineContent(endLineNumber), endTokenIndex - 1) == ' ')
        {
            endToken = " " + endToken;
            endTokenIndex -= 1;
        }

        if (startTokenIndex != -1 && endTokenIndex != -1)
        {
            return BlockCommentCommand.CreateRemoveBlockCommentOperations(
                new TextRange(startLineNumber, startTokenIndex + startToken.Length + 1, endLineNumber, endTokenIndex + 1), startToken, endToken);
        }

        return null;
    }

    // String.prototype.charCodeAt: NaN (here -1) outside the string.
    private static int CharCodeAt(string str, int index) => index >= 0 && index < str.Length ? str[index] : -1;

    /// <summary>Given an unsuccessful analysis, delegate to the block comment command.</summary>
    private void ExecuteBlockComment(TextModel model, IEditOperationBuilder builder, TextSelection s)
    {
        model.Tokenization.TokenizeIfCheap(s.StartLineNumber);
        var languageId = model.GetLanguageIdAtPosition(s.StartLineNumber, 1);
        var config = _languageConfigurationService.GetLanguageConfiguration(languageId).Comments;
        if (config is null || string.IsNullOrEmpty(config.BlockCommentStartToken) || string.IsNullOrEmpty(config.BlockCommentEndToken))
        {
            // Mode does not support block comments
            return;
        }

        var startToken = config.BlockCommentStartToken;
        var endToken = config.BlockCommentEndToken;

        var ops = AttemptRemoveBlockComment(model, s, startToken, endToken);
        if (ops is null)
        {
            if (s.IsEmpty)
            {
                var lineContent = model.GetLineContent(s.StartLineNumber);
                var firstNonWhitespaceIndex = Strings.FirstNonWhitespaceIndex(lineContent);
                if (firstNonWhitespaceIndex == -1)
                {
                    // Line is empty or contains only whitespace
                    firstNonWhitespaceIndex = lineContent.Length;
                }
                ops = BlockCommentCommand.CreateAddBlockCommentOperations(
                    new TextRange(s.StartLineNumber, firstNonWhitespaceIndex + 1, s.StartLineNumber, lineContent.Length + 1),
                    startToken,
                    endToken,
                    _insertSpace);
            }
            else
            {
                ops = BlockCommentCommand.CreateAddBlockCommentOperations(
                    new TextRange(s.StartLineNumber, model.GetLineFirstNonWhitespaceColumn(s.StartLineNumber), s.EndLineNumber, model.GetLineMaxColumn(s.EndLineNumber)),
                    startToken,
                    endToken,
                    _insertSpace);
            }

            if (ops.Count == 1)
            {
                // Leave cursor after token and Space
                _deltaColumn = startToken.Length + 1;
            }
        }
        _selectionId = builder.TrackSelection(s);
        foreach (var op in ops)
        {
            builder.AddEditOperation(op.Range, op.Text);
        }
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        var s = _selection;
        _moveEndPositionDown = false;

        if (s.StartLineNumber == s.EndLineNumber && _ignoreFirstLine)
        {
            builder.AddEditOperation(new TextRange(s.StartLineNumber, model.GetLineMaxColumn(s.StartLineNumber), s.StartLineNumber + 1, 1), s.StartLineNumber == model.GetLineCount() ? "" : "\n");
            _selectionId = builder.TrackSelection(s);
            return;
        }

        if (s.StartLineNumber < s.EndLineNumber && s.EndColumn == 1)
        {
            _moveEndPositionDown = true;
            s = s.SetEndPosition(s.EndLineNumber - 1, model.GetLineMaxColumn(s.EndLineNumber - 1));
        }

        var data = GatherPreflightData(
            _type,
            _insertSpace,
            model,
            s.StartLineNumber,
            s.EndLineNumber,
            _ignoreEmptyLines,
            _ignoreFirstLine,
            _languageConfigurationService);

        if (data.Supported)
        {
            ExecuteLineComments(new TextModelLines(model), builder, data, s);
            return;
        }

        ExecuteBlockComment(model, builder, s);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper)
    {
        var result = helper.GetTrackedSelection(_selectionId!);

        if (_moveEndPositionDown)
        {
            result = result.SetEndPosition(result.EndLineNumber + 1, 1);
        }

        return new TextSelection(
            result.SelectionStartLineNumber,
            result.SelectionStartColumn + _deltaColumn,
            result.PositionLineNumber,
            result.PositionColumn + _deltaColumn);
    }

    /// <summary>Generate edit operations in the remove line comment case.</summary>
    public static List<SingleEditOperation> CreateRemoveLineCommentsOperations(IReadOnlyList<LinePreflightData> lines, int startLineNumber)
    {
        var res = new List<SingleEditOperation>();

        for (int i = 0, len = lines.Count; i < len; i++)
        {
            var lineData = lines[i];

            if (lineData.Ignore)
            {
                continue;
            }

            res.Add(EditOperation.Delete(new TextRange(
                startLineNumber + i, lineData.CommentStrOffset + 1,
                startLineNumber + i, lineData.CommentStrOffset + lineData.CommentStrLength + 1)));
        }

        return res;
    }

    /// <summary>Generate edit operations in the add line comment case.</summary>
    private List<SingleEditOperation> CreateAddLineCommentsOperations(IReadOnlyList<LinePreflightData> lines, int startLineNumber)
    {
        var res = new List<SingleEditOperation>();
        var afterCommentStr = _insertSpace ? " " : "";

        for (int i = 0, len = lines.Count; i < len; i++)
        {
            var lineData = lines[i];

            if (lineData.Ignore)
            {
                continue;
            }

            res.Add(EditOperation.Insert(new TextPosition(startLineNumber + i, lineData.CommentStrOffset + 1), lineData.CommentStr + afterCommentStr));
        }

        return res;
    }

    private static int NextVisibleColumn(int currentVisibleColumn, int indentSize, bool isTab, int columnSize)
    {
        if (isTab)
        {
            return currentVisibleColumn + (indentSize - (currentVisibleColumn % indentSize));
        }
        return currentVisibleColumn + columnSize;
    }

    /// <summary>Adjust insertion points to have them vertically aligned in the add line comment case.</summary>
    public static void NormalizeInsertionPoint(ILineCommentSimpleModel model, IReadOnlyList<LinePreflightData> lines, int startLineNumber, int indentSize)
    {
        var minVisibleColumn = MaxSafeSmallInteger;
        int j;
        int lenJ;

        for (int i = 0, len = lines.Count; i < len; i++)
        {
            if (lines[i].Ignore)
            {
                continue;
            }

            var lineContent = model.GetLineContent(startLineNumber + i);

            var currentVisibleColumn = 0;
            for (int k = 0, lenK = lines[i].CommentStrOffset; currentVisibleColumn < minVisibleColumn && k < lenK; k++)
            {
                currentVisibleColumn = NextVisibleColumn(currentVisibleColumn, indentSize, lineContent[k] == '\t', 1);
            }

            if (currentVisibleColumn < minVisibleColumn)
            {
                minVisibleColumn = currentVisibleColumn;
            }
        }

        minVisibleColumn = minVisibleColumn / indentSize * indentSize;

        for (int i = 0, len = lines.Count; i < len; i++)
        {
            if (lines[i].Ignore)
            {
                continue;
            }

            var lineContent = model.GetLineContent(startLineNumber + i);

            var currentVisibleColumn = 0;
            for (j = 0, lenJ = lines[i].CommentStrOffset; currentVisibleColumn < minVisibleColumn && j < lenJ; j++)
            {
                currentVisibleColumn = NextVisibleColumn(currentVisibleColumn, indentSize, lineContent[j] == '\t', 1);
            }

            if (currentVisibleColumn > minVisibleColumn)
            {
                lines[i].CommentStrOffset = j - 1;
            }
            else
            {
                lines[i].CommentStrOffset = j;
            }
        }
    }

    /// <summary>A text model as an <see cref="ILineCommentSimpleModel"/>.</summary>
    private sealed class TextModelLines(TextModel model) : ILineCommentSimpleModel
    {
        public string GetLineContent(int lineNumber) => model.GetLineContent(lineNumber);
    }
}
