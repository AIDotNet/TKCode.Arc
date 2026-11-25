// Ported from vscode (src/vs/editor/common/viewModel/viewModelLines.ts: the guide methods of
// ViewModelLinesFromProjectedModel and IndentGuideRepeatOption, commit b57d5c9e), MIT license.
// Differences from upstream: the guides come from the IGuidesTextModelPart the lines were given (upstream's
// model.guides); NoGuides stands in when there is none.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

internal sealed partial class ViewModelLinesFromProjectedModel
{
    public ActiveIndentGuideInfo GetActiveIndentGuide(int viewLineNumber, int minLineNumber, int maxLineNumber)
    {
        viewLineNumber = ToValidViewLineNumber(viewLineNumber);
        minLineNumber = ToValidViewLineNumber(minLineNumber);
        maxLineNumber = ToValidViewLineNumber(maxLineNumber);

        var modelPosition = ConvertViewPositionToModelPosition(viewLineNumber, GetViewLineMinColumn(viewLineNumber));
        var modelMinPosition = ConvertViewPositionToModelPosition(minLineNumber, GetViewLineMinColumn(minLineNumber));
        var modelMaxPosition = ConvertViewPositionToModelPosition(maxLineNumber, GetViewLineMinColumn(maxLineNumber));
        var result = _guides.GetActiveIndentGuide(modelPosition.LineNumber, modelMinPosition.LineNumber, modelMaxPosition.LineNumber);

        var viewStartPosition = ConvertModelPositionToViewPosition(result.StartLineNumber, 1);
        var viewEndPosition = ConvertModelPositionToViewPosition(result.EndLineNumber, _model.GetLineMaxColumn(result.EndLineNumber));
        return new ActiveIndentGuideInfo(viewStartPosition.LineNumber, viewEndPosition.LineNumber, result.Indent);
    }

    public IReadOnlyList<IReadOnlyList<IndentGuide>> GetViewLinesBracketGuides(int viewStartLineNumber, int viewEndLineNumber, TextPosition? activeViewPosition, BracketGuideOptions options)
    {
        TextPosition? modelActivePosition = activeViewPosition is { } active
            ? ConvertViewPositionToModelPosition(active.LineNumber, active.Column)
            : null;
        var resultPerViewLine = new List<IReadOnlyList<IndentGuide>>();

        foreach (var group in GetViewLineInfosGroupedByModelRanges(viewStartLineNumber, viewEndLineNumber))
        {
            var modelRangeStartLineNumber = group.ModelRange.StartLineNumber;

            var bracketGuidesPerModelLine = _guides.GetLinesBracketGuides(
                modelRangeStartLineNumber,
                group.ModelRange.EndLineNumber,
                modelActivePosition,
                options);

            foreach (var viewLineInfo in group.ViewLines)
            {
                var bracketGuides = bracketGuidesPerModelLine[viewLineInfo.ModelLineNumber - modelRangeStartLineNumber];

                // visibleColumns stay as they are (this is a bug and needs to be fixed, but it is not a regression)
                // model-columns must be converted to view-model columns.
                var result = new List<IndentGuide>();
                foreach (var g in bracketGuides)
                {
                    if (MapBracketGuide(g, viewLineInfo) is { } mapped)
                    {
                        result.Add(mapped);
                    }
                }

                resultPerViewLine.Add(result);
            }
        }

        return resultPerViewLine;
    }

    private IndentGuide? MapBracketGuide(IndentGuide g, ViewLineInfo viewLineInfo)
    {
        var projection = _modelLineProjections[viewLineInfo.ModelLineNumber - 1];
        if (g.ForWrappedLinesAfterColumn != -1)
        {
            var p = projection.GetViewPositionOfModelPosition(0, g.ForWrappedLinesAfterColumn);
            if (p.LineNumber >= viewLineInfo.ModelLineWrappedLineIdx)
            {
                return null;
            }
        }

        if (g.ForWrappedLinesBeforeOrAtColumn != -1)
        {
            var p = projection.GetViewPositionOfModelPosition(0, g.ForWrappedLinesBeforeOrAtColumn);
            if (p.LineNumber < viewLineInfo.ModelLineWrappedLineIdx)
            {
                return null;
            }
        }

        if (g.HorizontalLine is not { } horizontalLine)
        {
            return g;
        }

        var column = -1;
        if (g.Column != -1)
        {
            var p = projection.GetViewPositionOfModelPosition(0, g.Column);
            if (p.LineNumber == viewLineInfo.ModelLineWrappedLineIdx)
            {
                column = p.Column;
            }
            else if (p.LineNumber < viewLineInfo.ModelLineWrappedLineIdx)
            {
                column = GetMinColumnOfViewLine(viewLineInfo);
            }
            else if (p.LineNumber > viewLineInfo.ModelLineWrappedLineIdx)
            {
                return null;
            }
        }

        var viewPosition = ConvertModelPositionToViewPosition(viewLineInfo.ModelLineNumber, horizontalLine.EndColumn);
        var endPosition = projection.GetViewPositionOfModelPosition(0, horizontalLine.EndColumn);
        if (endPosition.LineNumber == viewLineInfo.ModelLineWrappedLineIdx)
        {
            return new IndentGuide(
                g.VisibleColumn,
                column,
                g.ClassName,
                new IndentGuideHorizontalLine(horizontalLine.Top, viewPosition.Column),
                -1,
                -1);
        }
        else if (endPosition.LineNumber < viewLineInfo.ModelLineWrappedLineIdx)
        {
            return null;
        }
        else
        {
            if (g.VisibleColumn != -1)
            {
                // Don't repeat horizontal lines that use visibleColumn for unrelated lines.
                return null;
            }

            return new IndentGuide(
                g.VisibleColumn,
                column,
                g.ClassName,
                new IndentGuideHorizontalLine(horizontalLine.Top, GetMaxColumnOfViewLine(viewLineInfo)),
                -1,
                -1);
        }
    }

    public IReadOnlyList<int> GetViewLinesIndentGuides(int viewStartLineNumber, int viewEndLineNumber)
    {
        // TODO: Use the same code as in `getViewLinesBracketGuides`.
        // Future TODO: Merge with `getViewLinesBracketGuides`.
        // However, this requires more refactoring of indent guides.
        viewStartLineNumber = ToValidViewLineNumber(viewStartLineNumber);
        viewEndLineNumber = ToValidViewLineNumber(viewEndLineNumber);

        var modelStart = ConvertViewPositionToModelPosition(viewStartLineNumber, GetViewLineMinColumn(viewStartLineNumber));
        var modelEnd = ConvertViewPositionToModelPosition(viewEndLineNumber, GetViewLineMaxColumn(viewEndLineNumber));

        var result = new List<int>();
        var resultRepeatCount = new List<int>();
        var resultRepeatOption = new List<IndentGuideRepeatOption>();
        var modelStartLineIndex = modelStart.LineNumber - 1;
        var modelEndLineIndex = modelEnd.LineNumber - 1;

        TextPosition? reqStart = null;
        for (var modelLineIndex = modelStartLineIndex; modelLineIndex <= modelEndLineIndex; modelLineIndex++)
        {
            var line = _modelLineProjections[modelLineIndex];
            if (line.IsVisible())
            {
                var viewLineStartIndex = line.GetViewLineNumberOfModelPosition(0, modelLineIndex == modelStartLineIndex ? modelStart.Column : 1);
                var viewLineEndIndex = line.GetViewLineNumberOfModelPosition(0, _model.GetLineMaxColumn(modelLineIndex + 1));
                var count = viewLineEndIndex - viewLineStartIndex + 1;
                var option = IndentGuideRepeatOption.BlockNone;
                if (count > 1 && line.GetViewLineMinColumn(_simpleModel, modelLineIndex + 1, viewLineEndIndex) == 1)
                {
                    // wrapped lines should block indent guides
                    option = viewLineStartIndex == 0 ? IndentGuideRepeatOption.BlockSubsequent : IndentGuideRepeatOption.BlockAll;
                }

                resultRepeatCount.Add(count);
                resultRepeatOption.Add(option);
                // merge into previous request
                reqStart ??= new TextPosition(modelLineIndex + 1, 0);
            }
            else
            {
                // hit invisible line => flush request
                if (reqStart is { } start)
                {
                    result.AddRange(_guides.GetLinesIndentGuides(start.LineNumber, modelLineIndex));
                    reqStart = null;
                }
            }
        }

        if (reqStart is { } lastStart)
        {
            result.AddRange(_guides.GetLinesIndentGuides(lastStart.LineNumber, modelEnd.LineNumber));
        }

        var viewLineCount = viewEndLineNumber - viewStartLineNumber + 1;
        var viewIndents = new int[viewLineCount];
        var currIndex = 0;
        for (int i = 0, len = result.Count; i < len; i++)
        {
            var value = result[i];
            var count = Math.Min(viewLineCount - currIndex, resultRepeatCount[i]);
            var option = resultRepeatOption[i];
            int blockAtIndex;
            if (option == IndentGuideRepeatOption.BlockAll)
            {
                blockAtIndex = 0;
            }
            else if (option == IndentGuideRepeatOption.BlockSubsequent)
            {
                blockAtIndex = 1;
            }
            else
            {
                blockAtIndex = count;
            }

            for (var j = 0; j < count; j++)
            {
                if (j == blockAtIndex)
                {
                    value = 0;
                }

                viewIndents[currIndex++] = value;
            }
        }

        return viewIndents;
    }

    private enum IndentGuideRepeatOption
    {
        BlockNone = 0,
        BlockSubsequent = 1,
        BlockAll = 2,
    }

    /// <summary>The guides of a model that has none: no indent guides, no bracket guides.</summary>
    private sealed class NoGuides : IGuidesTextModelPart
    {
        public ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber) =>
            new(lineNumber, lineNumber, 0);

        public IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber) =>
            new int[Math.Max(0, endLineNumber - startLineNumber + 1)];

        public IReadOnlyList<IReadOnlyList<IndentGuide>> GetLinesBracketGuides(int startLineNumber, int endLineNumber, TextPosition? activePosition, BracketGuideOptions options)
        {
            var result = new IReadOnlyList<IndentGuide>[Math.Max(0, endLineNumber - startLineNumber + 1)];
            Array.Fill(result, Array.Empty<IndentGuide>());
            return result;
        }
    }
}
