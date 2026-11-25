// Ported from vscode (src/vs/editor/common/model/guidesTextModelPart.ts, commit b57d5c9e), MIT license.
//
// Differences: the part reads the bracket pairs and language configurations of the LanguageConfigurationService it is
// created with (TextModel.GetGuides); a disposed model throws ObjectDisposedException (upstream: assertNotDisposed);
// an illegal line number throws ArgumentOutOfRangeException (upstream: BugIndicatingError / Error).

using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model.BracketPairs;

namespace TKCode.Arc.Editor.Model;

/// <summary>The indent guides and bracket pair guides of a model (upstream's model.guides).</summary>
internal sealed class GuidesTextModelPart : IGuidesTextModelPart
{
    private readonly TextModel _textModel;
    private readonly LanguageConfigurationService _languageConfigurationService;

    public GuidesTextModelPart(TextModel textModel, LanguageConfigurationService languageConfigurationService)
    {
        _textModel = textModel;
        _languageConfigurationService = languageConfigurationService;
    }

    private ResolvedLanguageConfiguration GetLanguageConfiguration(string languageId) =>
        _languageConfigurationService.GetLanguageConfiguration(languageId);

    private int ComputeIndentLevel(int lineIndex) =>
        TextModelUtils.ComputeIndentLevel(_textModel.GetLineContent(lineIndex + 1), _textModel.GetOptions().TabSize);

    private static int CeilDiv(int value, int divisor) => (int)Math.Ceiling(value / (double)divisor);

    public ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber)
    {
        _textModel.AssertNotDisposed();
        var lineCount = _textModel.GetLineCount();

        if (lineNumber < 1 || lineNumber > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Illegal value for lineNumber");
        }

        var offSide = GetLanguageConfiguration(_textModel.GetLanguageId()).FoldingRules.OffSide == true;

        var upAboveContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var upAboveContentLineIndent = -1;
        var upBelowContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var upBelowContentLineIndent = -1;
        void UpResolveIndents(int lineNumber)
        {
            if (upAboveContentLineIndex != -1 && (upAboveContentLineIndex == -2 || upAboveContentLineIndex > lineNumber - 1))
            {
                upAboveContentLineIndex = -1;
                upAboveContentLineIndent = -1;

                // must find previous line with content
                for (var lineIndex = lineNumber - 2; lineIndex >= 0; lineIndex--)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        upAboveContentLineIndex = lineIndex;
                        upAboveContentLineIndent = indent;
                        break;
                    }
                }
            }

            if (upBelowContentLineIndex == -2)
            {
                upBelowContentLineIndex = -1;
                upBelowContentLineIndent = -1;

                // must find next line with content
                for (var lineIndex = lineNumber; lineIndex < lineCount; lineIndex++)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        upBelowContentLineIndex = lineIndex;
                        upBelowContentLineIndent = indent;
                        break;
                    }
                }
            }
        }

        var downAboveContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var downAboveContentLineIndent = -1;
        var downBelowContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var downBelowContentLineIndent = -1;
        void DownResolveIndents(int lineNumber)
        {
            if (downAboveContentLineIndex == -2)
            {
                downAboveContentLineIndex = -1;
                downAboveContentLineIndent = -1;

                // must find previous line with content
                for (var lineIndex = lineNumber - 2; lineIndex >= 0; lineIndex--)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        downAboveContentLineIndex = lineIndex;
                        downAboveContentLineIndent = indent;
                        break;
                    }
                }
            }

            if (downBelowContentLineIndex != -1 && (downBelowContentLineIndex == -2 || downBelowContentLineIndex < lineNumber - 1))
            {
                downBelowContentLineIndex = -1;
                downBelowContentLineIndent = -1;

                // must find next line with content
                for (var lineIndex = lineNumber; lineIndex < lineCount; lineIndex++)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        downBelowContentLineIndex = lineIndex;
                        downBelowContentLineIndent = indent;
                        break;
                    }
                }
            }
        }

        var startLineNumber = 0;
        var goUp = true;
        var endLineNumber = 0;
        var goDown = true;
        var indentLevel = 0;

        var initialIndent = 0;

        for (var distance = 0; goUp || goDown; distance++)
        {
            var upLineNumber = lineNumber - distance;
            var downLineNumber = lineNumber + distance;

            if (distance > 1 && (upLineNumber < 1 || upLineNumber < minLineNumber))
            {
                goUp = false;
            }

            if (distance > 1 && (downLineNumber > lineCount || downLineNumber > maxLineNumber))
            {
                goDown = false;
            }

            if (distance > 50000)
            {
                // stop processing
                goUp = false;
                goDown = false;
            }

            var upLineIndentLevel = -1;
            if (goUp && upLineNumber >= 1)
            {
                // compute indent level going up
                var currentIndent = ComputeIndentLevel(upLineNumber - 1);
                if (currentIndent >= 0)
                {
                    // This line has content (besides whitespace)
                    // Use the line's indent
                    upBelowContentLineIndex = upLineNumber - 1;
                    upBelowContentLineIndent = currentIndent;
                    upLineIndentLevel = CeilDiv(currentIndent, _textModel.GetOptions().IndentSize);
                }
                else
                {
                    UpResolveIndents(upLineNumber);
                    upLineIndentLevel = GetIndentLevelForWhitespaceLine(offSide, upAboveContentLineIndent, upBelowContentLineIndent);
                }
            }

            var downLineIndentLevel = -1;
            if (goDown && downLineNumber <= lineCount)
            {
                // compute indent level going down
                var currentIndent = ComputeIndentLevel(downLineNumber - 1);
                if (currentIndent >= 0)
                {
                    // This line has content (besides whitespace)
                    // Use the line's indent
                    downAboveContentLineIndex = downLineNumber - 1;
                    downAboveContentLineIndent = currentIndent;
                    downLineIndentLevel = CeilDiv(currentIndent, _textModel.GetOptions().IndentSize);
                }
                else
                {
                    DownResolveIndents(downLineNumber);
                    downLineIndentLevel = GetIndentLevelForWhitespaceLine(offSide, downAboveContentLineIndent, downBelowContentLineIndent);
                }
            }

            if (distance == 0)
            {
                initialIndent = upLineIndentLevel;
                continue;
            }

            if (distance == 1)
            {
                if (downLineNumber <= lineCount && downLineIndentLevel >= 0 && initialIndent + 1 == downLineIndentLevel)
                {
                    // This is the beginning of a scope, we have special handling here, since we want the
                    // child scope indent to be active, not the parent scope
                    goUp = false;
                    startLineNumber = downLineNumber;
                    endLineNumber = downLineNumber;
                    indentLevel = downLineIndentLevel;
                    continue;
                }

                if (upLineNumber >= 1 && upLineIndentLevel >= 0 && upLineIndentLevel - 1 == initialIndent)
                {
                    // This is the end of a scope, just like above
                    goDown = false;
                    startLineNumber = upLineNumber;
                    endLineNumber = upLineNumber;
                    indentLevel = upLineIndentLevel;
                    continue;
                }

                startLineNumber = lineNumber;
                endLineNumber = lineNumber;
                indentLevel = initialIndent;
                if (indentLevel == 0)
                {
                    // No need to continue
                    return new ActiveIndentGuideInfo(startLineNumber, endLineNumber, indentLevel);
                }
            }

            if (goUp)
            {
                if (upLineIndentLevel >= indentLevel)
                {
                    startLineNumber = upLineNumber;
                }
                else
                {
                    goUp = false;
                }
            }

            if (goDown)
            {
                if (downLineIndentLevel >= indentLevel)
                {
                    endLineNumber = downLineNumber;
                }
                else
                {
                    goDown = false;
                }
            }
        }

        return new ActiveIndentGuideInfo(startLineNumber, endLineNumber, indentLevel);
    }

    public IReadOnlyList<IReadOnlyList<IndentGuide>> GetLinesBracketGuides(
        int startLineNumber,
        int endLineNumber,
        TextPosition? activePosition,
        BracketGuideOptions options)
    {
        var result = new List<IndentGuide>[endLineNumber - startLineNumber + 1];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = [];
        }

        // If requested, this could be made configurable.
        const bool includeSingleLinePairs = true;

        var bracketPairsPart = _textModel.GetBracketPairs(_languageConfigurationService);
        List<BracketPairWithMinIndentationInfo> bracketPairs = bracketPairsPart.GetBracketPairsInRangeWithMinIndentation(
            new TextRange(startLineNumber, 1, endLineNumber, _textModel.GetLineMaxColumn(endLineNumber))).ToArray();

        TextRange? activeBracketPairRange = null;
        if (activePosition is { } active && bracketPairs.Count > 0)
        {
            var bracketsContainingActivePosition = (startLineNumber <= active.LineNumber && active.LineNumber <= endLineNumber
                    // We don't need to query the brackets again if the cursor is in the viewport
                    ? bracketPairs.Cast<BracketPairInfo>().ToList()
                    : bracketPairsPart.GetBracketPairsInRange(new TextRange(active.LineNumber, active.Column, active.LineNumber, active.Column)).ToArray())
                .Where(bp => bp.Range.StrictContainsPosition(active));

            activeBracketPairRange = bracketsContainingActivePosition
                .LastOrDefault(i => includeSingleLinePairs || i.Range.StartLineNumber != i.Range.EndLineNumber)?.Range;
        }

        var independentColorPoolPerBracketType = _textModel.GetOptions().BracketPairColorizationOptions.IndependentColorPoolPerBracketType;

        foreach (var pair in bracketPairs)
        {
            /*
                    {
                    |
                    }

                    {
                    |
                    ----}

                ____{
                |test
                ----}

                renderHorizontalEndLineAtTheBottom:
                    {
                    |
                    |x}
                    --
                renderHorizontalEndLineAtTheBottom:
                ____{
                |test
                | x }
                ----
            */

            if (pair.ClosingBracketRange is not { } closingBracketRange)
            {
                continue;
            }

            var isActive = activeBracketPairRange is { } activeRange && pair.Range.Equals(activeRange);

            if (!isActive && !options.IncludeInactive)
            {
                continue;
            }

            var className = BracketPairGuidesClassNames.GetInlineClassName(pair.NestingLevel, pair.NestingLevelOfEqualBracketType, independentColorPoolPerBracketType)
                + (options.HighlightActive && isActive ? " " + BracketPairGuidesClassNames.ActiveClassName : "");

            var start = pair.OpeningBracketRange.Start;
            var end = closingBracketRange.Start;

            var horizontalGuides = options.HorizontalGuides == HorizontalGuidesState.Enabled
                || (options.HorizontalGuides == HorizontalGuidesState.EnabledForActive && isActive);

            if (pair.Range.StartLineNumber == pair.Range.EndLineNumber)
            {
                if (includeSingleLinePairs && horizontalGuides)
                {
                    result[pair.Range.StartLineNumber - startLineNumber].Add(
                        new IndentGuide(
                            -1,
                            pair.OpeningBracketRange.End.Column,
                            className,
                            new IndentGuideHorizontalLine(false, end.Column),
                            -1,
                            -1));
                }

                continue;
            }

            var endVisibleColumn = GetVisibleColumnFromPosition(end);
            var startVisibleColumn = GetVisibleColumnFromPosition(pair.OpeningBracketRange.Start);
            var guideVisibleColumn = Math.Min(Math.Min(startVisibleColumn, endVisibleColumn), pair.MinVisibleColumnIndentation + 1);

            var renderHorizontalEndLineAtTheBottom = false;

            var firstNonWsIndex = Strings.FirstNonWhitespaceIndex(_textModel.GetLineContent(closingBracketRange.StartLineNumber));
            var hasTextBeforeClosingBracket = firstNonWsIndex < closingBracketRange.StartColumn - 1;
            if (hasTextBeforeClosingBracket)
            {
                renderHorizontalEndLineAtTheBottom = true;
            }

            var visibleGuideStartLineNumber = Math.Max(start.LineNumber, startLineNumber);
            var visibleGuideEndLineNumber = Math.Min(end.LineNumber, endLineNumber);

            var offset = renderHorizontalEndLineAtTheBottom ? 1 : 0;

            for (var l = visibleGuideStartLineNumber; l < visibleGuideEndLineNumber + offset; l++)
            {
                result[l - startLineNumber].Add(
                    new IndentGuide(
                        guideVisibleColumn,
                        -1,
                        className,
                        null,
                        l == start.LineNumber ? start.Column : -1,
                        l == end.LineNumber ? end.Column : -1));
            }

            if (horizontalGuides)
            {
                if (start.LineNumber >= startLineNumber && startVisibleColumn > guideVisibleColumn)
                {
                    result[start.LineNumber - startLineNumber].Add(
                        new IndentGuide(
                            guideVisibleColumn,
                            -1,
                            className,
                            new IndentGuideHorizontalLine(false, start.Column),
                            -1,
                            -1));
                }

                if (end.LineNumber <= endLineNumber && endVisibleColumn > guideVisibleColumn)
                {
                    result[end.LineNumber - startLineNumber].Add(
                        new IndentGuide(
                            guideVisibleColumn,
                            -1,
                            className,
                            new IndentGuideHorizontalLine(!renderHorizontalEndLineAtTheBottom, end.Column),
                            -1,
                            -1));
                }
            }
        }

        foreach (var guides in result)
        {
            // a stable sort, as upstream's Array.prototype.sort
            var sorted = guides.OrderBy(g => g.VisibleColumn).ToList();
            guides.Clear();
            guides.AddRange(sorted);
        }

        return result;
    }

    private int GetVisibleColumnFromPosition(TextPosition position) =>
        CursorColumns.VisibleColumnFromColumn(
            _textModel.GetLineContent(position.LineNumber),
            position.Column,
            _textModel.GetOptions().TabSize) + 1;

    public IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber)
    {
        _textModel.AssertNotDisposed();
        var lineCount = _textModel.GetLineCount();

        if (startLineNumber < 1 || startLineNumber > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(startLineNumber), "Illegal value for startLineNumber");
        }

        if (endLineNumber < 1 || endLineNumber > lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(endLineNumber), "Illegal value for endLineNumber");
        }

        var options = _textModel.GetOptions();
        var offSide = GetLanguageConfiguration(_textModel.GetLanguageId()).FoldingRules.OffSide == true;

        var result = new int[endLineNumber - startLineNumber + 1];

        var aboveContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var aboveContentLineIndent = -1;

        var belowContentLineIndex = -2; /* -2 is a marker for not having computed it */
        var belowContentLineIndent = -1;

        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var resultIndex = lineNumber - startLineNumber;

            var currentIndent = ComputeIndentLevel(lineNumber - 1);
            if (currentIndent >= 0)
            {
                // This line has content (besides whitespace)
                // Use the line's indent
                aboveContentLineIndex = lineNumber - 1;
                aboveContentLineIndent = currentIndent;
                result[resultIndex] = CeilDiv(currentIndent, options.IndentSize);
                continue;
            }

            if (aboveContentLineIndex == -2)
            {
                aboveContentLineIndex = -1;
                aboveContentLineIndent = -1;

                // must find previous line with content
                for (var lineIndex = lineNumber - 2; lineIndex >= 0; lineIndex--)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        aboveContentLineIndex = lineIndex;
                        aboveContentLineIndent = indent;
                        break;
                    }
                }
            }

            if (belowContentLineIndex != -1 && (belowContentLineIndex == -2 || belowContentLineIndex < lineNumber - 1))
            {
                belowContentLineIndex = -1;
                belowContentLineIndent = -1;

                // must find next line with content
                for (var lineIndex = lineNumber; lineIndex < lineCount; lineIndex++)
                {
                    var indent = ComputeIndentLevel(lineIndex);
                    if (indent >= 0)
                    {
                        belowContentLineIndex = lineIndex;
                        belowContentLineIndent = indent;
                        break;
                    }
                }
            }

            result[resultIndex] = GetIndentLevelForWhitespaceLine(offSide, aboveContentLineIndent, belowContentLineIndent);
        }

        return result;
    }

    private int GetIndentLevelForWhitespaceLine(bool offSide, int aboveContentLineIndent, int belowContentLineIndent)
    {
        var options = _textModel.GetOptions();

        if (aboveContentLineIndent == -1 || belowContentLineIndent == -1)
        {
            // At the top or bottom of the file
            return 0;
        }
        else if (aboveContentLineIndent < belowContentLineIndent)
        {
            // we are inside the region above
            return 1 + (aboveContentLineIndent / options.IndentSize);
        }
        else if (aboveContentLineIndent == belowContentLineIndent)
        {
            // we are in between two regions
            return CeilDiv(belowContentLineIndent, options.IndentSize);
        }
        else
        {
            if (offSide)
            {
                // same level as region below
                return CeilDiv(belowContentLineIndent, options.IndentSize);
            }
            else
            {
                // we are inside the region that ends below
                return 1 + (belowContentLineIndent / options.IndentSize);
            }
        }
    }
}

/// <summary>The class names of bracket pair guides.</summary>
internal static class BracketPairGuidesClassNames
{
    public const string ActiveClassName = "indent-active";

    public static string GetInlineClassName(int nestingLevel, int nestingLevelOfEqualBracketType, bool independentColorPoolPerBracketType) =>
        GetInlineClassNameOfLevel(independentColorPoolPerBracketType ? nestingLevelOfEqualBracketType : nestingLevel);

    // To support a dynamic amount of colors up to 6 colors, we use a number that is a lcm of all numbers from 1 to 6.
    public static string GetInlineClassNameOfLevel(int level) => "bracket-indent-guide lvl-" + (level % 30);
}
