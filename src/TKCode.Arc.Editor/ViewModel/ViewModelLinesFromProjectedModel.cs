// Ported from vscode (src/vs/editor/common/viewModel/viewModelLines.ts: ViewModelLinesFromProjectedModel,
// normalizeLineRanges, ViewLineInfo, ViewLineInfoGroupedByModelRange and CoordinatesConverter, commit b57d5c9e),
// MIT license. The guide methods are in ViewModelLinesFromProjectedModel.Guides.cs.
// Differences from upstream:
//  - the constructor takes the IGuidesTextModelPart to read (the view model passes TextModel.GetGuides, upstream's
//    model.guides); without one the model has no guides: no indent guides, an active guide of indent 0 on the line
//    itself and no bracket guides;
//  - the projections read the model through a TextModelSimpleModel (upstream passes the model itself);
//  - getViewLinesData returns an array as long as the requested range; a line it does not reach stays null (upstream's
//    array is shorter then);
//  - an index past the end of `needed` reads as false (upstream's undefined).

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The view lines of a model whose lines can be hidden, wrapped and have text injected into them.</summary>
internal sealed partial class ViewModelLinesFromProjectedModel : IViewModelLines
{
    private readonly int _editorId;
    private readonly TextModel _model;
    private readonly TextModelSimpleModel _simpleModel;
    private readonly IGuidesTextModelPart _guides;
    private int _validModelVersionId;

    private readonly ILineBreaksComputerFactory _domLineBreaksComputerFactory;
    private readonly ILineBreaksComputerFactory _monospaceLineBreaksComputerFactory;

    private FontInfo _fontInfo;
    private int _tabSize;
    private int _wrappingColumn;
    private WrappingIndent _wrappingIndent;
    private WordBreak _wordBreak;
    private WrappingStrategy _wrappingStrategy;
    private readonly bool _wrapOnEscapedLineFeeds;
    private bool _useTwoCellFullwidthCharacters;

    private List<IModelLineProjection> _modelLineProjections = new();

    /// <summary>Reflects the sum of the line counts of all projected model lines.</summary>
    private ConstantTimePrefixSumComputer _projectedModelLineLineCounts = new([]);

    private string[] _hiddenAreasDecorationIds = [];

    public ViewModelLinesFromProjectedModel(
        int editorId,
        TextModel model,
        ILineBreaksComputerFactory domLineBreaksComputerFactory,
        ILineBreaksComputerFactory monospaceLineBreaksComputerFactory,
        FontInfo fontInfo,
        int tabSize,
        WrappingStrategy wrappingStrategy,
        int wrappingColumn,
        WrappingIndent wrappingIndent,
        WordBreak wordBreak,
        bool wrapOnEscapedLineFeeds,
        bool useTwoCellFullwidthCharacters,
        IGuidesTextModelPart? guides = null)
    {
        _editorId = editorId;
        _model = model;
        _simpleModel = new TextModelSimpleModel(model);
        _guides = guides ?? new NoGuides();
        _validModelVersionId = -1;
        _domLineBreaksComputerFactory = domLineBreaksComputerFactory;
        _monospaceLineBreaksComputerFactory = monospaceLineBreaksComputerFactory;
        _fontInfo = fontInfo;
        _tabSize = tabSize;
        _wrappingStrategy = wrappingStrategy;
        _wrappingColumn = wrappingColumn;
        _wrappingIndent = wrappingIndent;
        _wordBreak = wordBreak;
        _wrapOnEscapedLineFeeds = wrapOnEscapedLineFeeds;
        _useTwoCellFullwidthCharacters = useTwoCellFullwidthCharacters;

        ConstructLines(resetHiddenAreas: true, null);
    }

    public void Dispose()
    {
        _hiddenAreasDecorationIds = _model.DeltaDecorations(_hiddenAreasDecorationIds, []);
    }

    public ICoordinatesConverter CreateCoordinatesConverter() => new CoordinatesConverter(this);

    private void ConstructLines(bool resetHiddenAreas, IReadOnlyList<ModelLineProjectionData?>? previousLineBreaks)
    {
        _modelLineProjections = new List<IModelLineProjection>();

        if (resetHiddenAreas)
        {
            _hiddenAreasDecorationIds = _model.DeltaDecorations(_hiddenAreasDecorationIds, []);
        }

        var linesContent = _model.GetLinesContent();
        var lineCount = linesContent.Length;
        var lineBreaksComputer = CreateLineBreaksComputer();

        for (var i = 0; i < lineCount; i++)
        {
            lineBreaksComputer.AddRequest(i + 1, previousLineBreaks is not null ? previousLineBreaks[i] : null);
        }

        var linesBreaks = lineBreaksComputer.Finalize();

        var values = new int[lineCount];

        var hiddenAreas = GetHiddenAreaRanges();
        hiddenAreas.Sort(TextRange.CompareRangesUsingStarts);
        int hiddenAreaStart = 1, hiddenAreaEnd = 0;
        var hiddenAreaIdx = -1;
        var nextLineNumberToUpdateHiddenArea = hiddenAreaIdx + 1 < hiddenAreas.Count ? hiddenAreaEnd + 1 : lineCount + 2;

        for (var i = 0; i < lineCount; i++)
        {
            var lineNumber = i + 1;

            if (lineNumber == nextLineNumberToUpdateHiddenArea)
            {
                hiddenAreaIdx++;
                hiddenAreaStart = hiddenAreas[hiddenAreaIdx].StartLineNumber;
                hiddenAreaEnd = hiddenAreas[hiddenAreaIdx].EndLineNumber;
                nextLineNumberToUpdateHiddenArea = hiddenAreaIdx + 1 < hiddenAreas.Count ? hiddenAreaEnd + 1 : lineCount + 2;
            }

            var isInHiddenArea = lineNumber >= hiddenAreaStart && lineNumber <= hiddenAreaEnd;
            var line = ModelLineProjections.Create(linesBreaks[i], !isInHiddenArea);
            values[i] = line.GetViewLineCount();
            _modelLineProjections.Add(line);
        }

        _validModelVersionId = _model.GetVersionId();

        _projectedModelLineLineCounts = new ConstantTimePrefixSumComputer(values);

        EnsureAtLeastOneVisibleLine();
    }

    private List<TextRange> GetHiddenAreaRanges()
    {
        var result = new List<TextRange>(_hiddenAreasDecorationIds.Length);
        foreach (var areaId in _hiddenAreasDecorationIds)
        {
            result.Add(_model.GetDecorationRange(areaId)!.Value);
        }

        return result;
    }

    public IReadOnlyList<TextRange> GetHiddenAreas() => GetHiddenAreaRanges();

    public bool SetHiddenAreas(IReadOnlyList<TextRange> ranges)
    {
        var validatedRanges = new List<TextRange>(ranges.Count);
        foreach (var r in ranges)
        {
            validatedRanges.Add(_model.ValidateRange(r));
        }

        var newRanges = NormalizeLineRanges(validatedRanges);

        // TODO@Martin: Please stop calling this method on each model change!

        // This checks if there really was a change
        var oldRanges = GetHiddenAreaRanges();
        oldRanges.Sort(TextRange.CompareRangesUsingStarts);
        if (newRanges.Count == oldRanges.Count)
        {
            var hasDifference = false;
            for (var i = 0; i < newRanges.Count; i++)
            {
                if (!newRanges[i].Equals(oldRanges[i]))
                {
                    hasDifference = true;
                    break;
                }
            }

            if (!hasDifference)
            {
                return false;
            }
        }

        var newDecorations = new List<ModelDeltaDecoration>(newRanges.Count);
        foreach (var r in newRanges)
        {
            newDecorations.Add(new ModelDeltaDecoration(r, ModelDecorationOptions.Empty));
        }

        _hiddenAreasDecorationIds = _model.DeltaDecorations(_hiddenAreasDecorationIds, newDecorations);

        var hiddenAreas = newRanges;
        int hiddenAreaStart = 1, hiddenAreaEnd = 0;
        var hiddenAreaIdx = -1;
        var nextLineNumberToUpdateHiddenArea = hiddenAreaIdx + 1 < hiddenAreas.Count ? hiddenAreaEnd + 1 : _modelLineProjections.Count + 2;

        var hasVisibleLine = false;
        for (var i = 0; i < _modelLineProjections.Count; i++)
        {
            var lineNumber = i + 1;

            if (lineNumber == nextLineNumberToUpdateHiddenArea)
            {
                hiddenAreaIdx++;
                hiddenAreaStart = hiddenAreas[hiddenAreaIdx].StartLineNumber;
                hiddenAreaEnd = hiddenAreas[hiddenAreaIdx].EndLineNumber;
                nextLineNumberToUpdateHiddenArea = hiddenAreaIdx + 1 < hiddenAreas.Count ? hiddenAreaEnd + 1 : _modelLineProjections.Count + 2;
            }

            var lineChanged = false;
            if (lineNumber >= hiddenAreaStart && lineNumber <= hiddenAreaEnd)
            {
                // Line should be hidden
                if (_modelLineProjections[i].IsVisible())
                {
                    _modelLineProjections[i] = _modelLineProjections[i].SetVisible(false);
                    lineChanged = true;
                }
            }
            else
            {
                hasVisibleLine = true;
                // Line should be visible
                if (!_modelLineProjections[i].IsVisible())
                {
                    _modelLineProjections[i] = _modelLineProjections[i].SetVisible(true);
                    lineChanged = true;
                }
            }

            if (lineChanged)
            {
                var newOutputLineCount = _modelLineProjections[i].GetViewLineCount();
                _projectedModelLineLineCounts.SetValue(i, newOutputLineCount);
            }
        }

        if (!hasVisibleLine)
        {
            // Cannot have everything be hidden => reveal everything!
            SetHiddenAreas([]);
        }

        return true;
    }

    public bool ModelPositionIsVisible(int modelLineNumber, int modelColumn)
    {
        if (modelLineNumber < 1 || modelLineNumber > _modelLineProjections.Count)
        {
            // invalid arguments
            return false;
        }

        return _modelLineProjections[modelLineNumber - 1].IsVisible();
    }

    public int GetModelLineViewLineCount(int modelLineNumber)
    {
        if (modelLineNumber < 1 || modelLineNumber > _modelLineProjections.Count)
        {
            // invalid arguments
            return 1;
        }

        return _modelLineProjections[modelLineNumber - 1].GetViewLineCount();
    }

    public bool SetTabSize(int newTabSize)
    {
        if (_tabSize == newTabSize)
        {
            return false;
        }

        _tabSize = newTabSize;

        ConstructLines(resetHiddenAreas: false, null);

        return true;
    }

    public bool SetWrappingSettings(FontInfo fontInfo, WrappingStrategy wrappingStrategy, int wrappingColumn, WrappingIndent wrappingIndent, WordBreak wordBreak, bool useTwoCellFullwidthCharacters)
    {
        var equalFontInfo = _fontInfo.Equals(fontInfo);
        var equalWrappingStrategy = _wrappingStrategy == wrappingStrategy;
        var equalWrappingColumn = _wrappingColumn == wrappingColumn;
        var equalWrappingIndent = _wrappingIndent == wrappingIndent;
        var equalWordBreak = _wordBreak == wordBreak;
        var equalUseTwoCellFullwidthCharacters = _useTwoCellFullwidthCharacters == useTwoCellFullwidthCharacters;
        if (equalFontInfo && equalWrappingStrategy && equalWrappingColumn && equalWrappingIndent && equalWordBreak && equalUseTwoCellFullwidthCharacters)
        {
            return false;
        }

        var onlyWrappingColumnChanged = equalFontInfo && equalWrappingStrategy && !equalWrappingColumn && equalWrappingIndent && equalWordBreak && equalUseTwoCellFullwidthCharacters;

        _fontInfo = fontInfo;
        _wrappingStrategy = wrappingStrategy;
        _wrappingColumn = wrappingColumn;
        _wrappingIndent = wrappingIndent;
        _wordBreak = wordBreak;
        _useTwoCellFullwidthCharacters = useTwoCellFullwidthCharacters;

        ModelLineProjectionData?[]? previousLineBreaks = null;
        if (onlyWrappingColumnChanged)
        {
            previousLineBreaks = new ModelLineProjectionData?[_modelLineProjections.Count];
            for (int i = 0, len = _modelLineProjections.Count; i < len; i++)
            {
                previousLineBreaks[i] = _modelLineProjections[i].GetProjectionData();
            }
        }

        ConstructLines(resetHiddenAreas: false, previousLineBreaks);

        return true;
    }

    public ILineBreaksComputer CreateLineBreaksComputer(ILineBreaksComputerContext? context = null)
    {
        var lineBreaksComputerFactory = _wrappingStrategy == WrappingStrategy.Advanced
            ? _domLineBreaksComputerFactory
            : _monospaceLineBreaksComputerFactory;
        context ??= new ModelLineBreaksComputerContext(_model, _editorId);
        return lineBreaksComputerFactory.CreateLineBreaksComputer(context, _fontInfo, _tabSize, _wrappingColumn, _wrappingIndent, _wordBreak, _wrapOnEscapedLineFeeds, _useTwoCellFullwidthCharacters);
    }

    public void OnModelFlushed()
    {
        ConstructLines(resetHiddenAreas: true, null);
    }

    public ViewLinesDeletedEvent? OnModelLinesDeleted(int? versionId, int fromLineNumber, int toLineNumber)
    {
        if (versionId is null or 0 || versionId <= _validModelVersionId)
        {
            // Here we check for versionId in case the lines were reconstructed in the meantime.
            // We don't want to apply stale change events on top of a newer read model state.
            return null;
        }

        var outputFromLineNumber = fromLineNumber == 1 ? 1 : _projectedModelLineLineCounts.GetPrefixSum(fromLineNumber - 1) + 1;
        var outputToLineNumber = _projectedModelLineLineCounts.GetPrefixSum(toLineNumber);

        _modelLineProjections.RemoveRange(fromLineNumber - 1, toLineNumber - fromLineNumber + 1);
        _projectedModelLineLineCounts.RemoveValues(fromLineNumber - 1, toLineNumber - fromLineNumber + 1);

        return new ViewLinesDeletedEvent(outputFromLineNumber, outputToLineNumber);
    }

    public ViewLinesInsertedEvent? OnModelLinesInserted(int? versionId, int fromLineNumber, int toLineNumber, IReadOnlyList<ModelLineProjectionData?> lineBreaks)
    {
        if (versionId is null or 0 || versionId <= _validModelVersionId)
        {
            // Here we check for versionId in case the lines were reconstructed in the meantime.
            // We don't want to apply stale change events on top of a newer read model state.
            return null;
        }

        // cannot use this.getHiddenAreas() because those decorations have already seen the effect of this model change
        var isInHiddenArea = fromLineNumber > 2 && !_modelLineProjections[fromLineNumber - 2].IsVisible();

        var outputFromLineNumber = fromLineNumber == 1 ? 1 : _projectedModelLineLineCounts.GetPrefixSum(fromLineNumber - 1) + 1;

        var totalOutputLineCount = 0;
        var insertLines = new List<IModelLineProjection>(lineBreaks.Count);
        var insertPrefixSumValues = new int[lineBreaks.Count];

        for (int i = 0, len = lineBreaks.Count; i < len; i++)
        {
            var line = ModelLineProjections.Create(lineBreaks[i], !isInHiddenArea);
            insertLines.Add(line);

            var outputLineCount = line.GetViewLineCount();
            totalOutputLineCount += outputLineCount;
            insertPrefixSumValues[i] = outputLineCount;
        }

        // TODO@Alex: use arrays.arrayInsert
        _modelLineProjections.InsertRange(Math.Min(fromLineNumber - 1, _modelLineProjections.Count), insertLines);

        _projectedModelLineLineCounts.InsertValues(fromLineNumber - 1, insertPrefixSumValues);

        return new ViewLinesInsertedEvent(outputFromLineNumber, outputFromLineNumber + totalOutputLineCount - 1);
    }

    public ModelLineChangedResult OnModelLineChanged(int? versionId, int lineNumber, ModelLineProjectionData? lineBreakData)
    {
        if (versionId is not null && versionId <= _validModelVersionId)
        {
            // Here we check for versionId in case the lines were reconstructed in the meantime.
            // We don't want to apply stale change events on top of a newer read model state.
            return new ModelLineChangedResult(false, null, null, null);
        }

        var lineIndex = lineNumber - 1;

        var oldOutputLineCount = _modelLineProjections[lineIndex].GetViewLineCount();
        var isVisible = _modelLineProjections[lineIndex].IsVisible();
        var line = ModelLineProjections.Create(lineBreakData, isVisible);
        _modelLineProjections[lineIndex] = line;
        var newOutputLineCount = _modelLineProjections[lineIndex].GetViewLineCount();

        var lineMappingChanged = false;
        int changeFrom;
        int changeTo;
        var insertFrom = 0;
        var insertTo = -1;
        var deleteFrom = 0;
        var deleteTo = -1;

        if (oldOutputLineCount > newOutputLineCount)
        {
            changeFrom = _projectedModelLineLineCounts.GetPrefixSum(lineNumber - 1) + 1;
            changeTo = changeFrom + newOutputLineCount - 1;
            deleteFrom = changeTo + 1;
            deleteTo = deleteFrom + (oldOutputLineCount - newOutputLineCount) - 1;
            lineMappingChanged = true;
        }
        else if (oldOutputLineCount < newOutputLineCount)
        {
            changeFrom = _projectedModelLineLineCounts.GetPrefixSum(lineNumber - 1) + 1;
            changeTo = changeFrom + oldOutputLineCount - 1;
            insertFrom = changeTo + 1;
            insertTo = insertFrom + (newOutputLineCount - oldOutputLineCount) - 1;
            lineMappingChanged = true;
        }
        else
        {
            changeFrom = _projectedModelLineLineCounts.GetPrefixSum(lineNumber - 1) + 1;
            changeTo = changeFrom + newOutputLineCount - 1;
        }

        _projectedModelLineLineCounts.SetValue(lineIndex, newOutputLineCount);

        var viewLinesChangedEvent = changeFrom <= changeTo ? new ViewLinesChangedEvent(changeFrom, changeTo - changeFrom + 1) : null;
        var viewLinesInsertedEvent = insertFrom <= insertTo ? new ViewLinesInsertedEvent(insertFrom, insertTo) : null;
        var viewLinesDeletedEvent = deleteFrom <= deleteTo ? new ViewLinesDeletedEvent(deleteFrom, deleteTo) : null;

        return new ModelLineChangedResult(lineMappingChanged, viewLinesChangedEvent, viewLinesInsertedEvent, viewLinesDeletedEvent);
    }

    public void AcceptVersionId(int versionId)
    {
        _validModelVersionId = versionId;
        EnsureAtLeastOneVisibleLine();
    }

    private void EnsureAtLeastOneVisibleLine()
    {
        if (GetViewLineCount() == 0 && _modelLineProjections.Count > 0)
        {
            _modelLineProjections[0] = _modelLineProjections[0].SetVisible(true);
            _projectedModelLineLineCounts.SetValue(0, _modelLineProjections[0].GetViewLineCount());
        }
    }

    public int GetViewLineCount() => _projectedModelLineLineCounts.GetTotalSum();

    private int ToValidViewLineNumber(int viewLineNumber)
    {
        if (viewLineNumber < 1)
        {
            return 1;
        }

        var viewLineCount = GetViewLineCount();
        if (viewLineNumber > viewLineCount)
        {
            return viewLineCount;
        }

        return viewLineNumber;
    }

    #region ViewLineInfo

    private ViewLineInfo GetViewLineInfo(int viewLineNumber)
    {
        viewLineNumber = ToValidViewLineNumber(viewLineNumber);
        var r = _projectedModelLineLineCounts.GetIndexOf(viewLineNumber - 1);
        var lineIndex = r.Index;
        var remainder = r.Remainder;
        return new ViewLineInfo(lineIndex + 1, remainder);
    }

    private int GetMinColumnOfViewLine(ViewLineInfo viewLineInfo) =>
        _modelLineProjections[viewLineInfo.ModelLineNumber - 1].GetViewLineMinColumn(
            _simpleModel,
            viewLineInfo.ModelLineNumber,
            viewLineInfo.ModelLineWrappedLineIdx);

    private int GetMaxColumnOfViewLine(ViewLineInfo viewLineInfo) =>
        _modelLineProjections[viewLineInfo.ModelLineNumber - 1].GetViewLineMaxColumn(
            _simpleModel,
            viewLineInfo.ModelLineNumber,
            viewLineInfo.ModelLineWrappedLineIdx);

    private TextPosition GetModelStartPositionOfViewLine(ViewLineInfo viewLineInfo)
    {
        var line = _modelLineProjections[viewLineInfo.ModelLineNumber - 1];
        var minViewColumn = line.GetViewLineMinColumn(
            _simpleModel,
            viewLineInfo.ModelLineNumber,
            viewLineInfo.ModelLineWrappedLineIdx);
        var column = line.GetModelColumnOfViewPosition(
            viewLineInfo.ModelLineWrappedLineIdx,
            minViewColumn);
        return new TextPosition(viewLineInfo.ModelLineNumber, column);
    }

    private TextPosition GetModelEndPositionOfViewLine(ViewLineInfo viewLineInfo)
    {
        var line = _modelLineProjections[viewLineInfo.ModelLineNumber - 1];
        var maxViewColumn = line.GetViewLineMaxColumn(
            _simpleModel,
            viewLineInfo.ModelLineNumber,
            viewLineInfo.ModelLineWrappedLineIdx);
        var column = line.GetModelColumnOfViewPosition(
            viewLineInfo.ModelLineWrappedLineIdx,
            maxViewColumn);
        return new TextPosition(viewLineInfo.ModelLineNumber, column);
    }

    private List<ViewLineInfoGroupedByModelRange> GetViewLineInfosGroupedByModelRanges(int viewStartLineNumber, int viewEndLineNumber)
    {
        var startViewLine = GetViewLineInfo(viewStartLineNumber);
        var endViewLine = GetViewLineInfo(viewEndLineNumber);

        var result = new List<ViewLineInfoGroupedByModelRange>();
        TextPosition? lastVisibleModelPos = GetModelStartPositionOfViewLine(startViewLine);
        var viewLines = new List<ViewLineInfo>();

        for (var curModelLine = startViewLine.ModelLineNumber; curModelLine <= endViewLine.ModelLineNumber; curModelLine++)
        {
            var line = _modelLineProjections[curModelLine - 1];

            if (line.IsVisible())
            {
                var startOffset = curModelLine == startViewLine.ModelLineNumber
                    ? startViewLine.ModelLineWrappedLineIdx
                    : 0;

                var endOffset = curModelLine == endViewLine.ModelLineNumber
                    ? endViewLine.ModelLineWrappedLineIdx + 1
                    : line.GetViewLineCount();

                for (var i = startOffset; i < endOffset; i++)
                {
                    viewLines.Add(new ViewLineInfo(curModelLine, i));
                }
            }

            if (!line.IsVisible() && lastVisibleModelPos is { } lastPos)
            {
                var lastVisibleModelPos2 = new TextPosition(curModelLine - 1, _model.GetLineMaxColumn(curModelLine - 1) + 1);

                var modelRange = new TextRange(lastPos, lastVisibleModelPos2);
                result.Add(new ViewLineInfoGroupedByModelRange(modelRange, viewLines));
                viewLines = new List<ViewLineInfo>();

                lastVisibleModelPos = null;
            }
            else if (line.IsVisible() && lastVisibleModelPos is null)
            {
                lastVisibleModelPos = new TextPosition(curModelLine, 1);
            }
        }

        if (lastVisibleModelPos is { } finalPos)
        {
            var modelRange = new TextRange(finalPos, GetModelEndPositionOfViewLine(endViewLine));
            result.Add(new ViewLineInfoGroupedByModelRange(modelRange, viewLines));
        }

        return result;
    }

    #endregion

    public string GetViewLineContent(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineContent(_simpleModel, info.ModelLineNumber, info.ModelLineWrappedLineIdx);
    }

    public int GetViewLineLength(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineLength(_simpleModel, info.ModelLineNumber, info.ModelLineWrappedLineIdx);
    }

    public int GetViewLineMinColumn(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineMinColumn(_simpleModel, info.ModelLineNumber, info.ModelLineWrappedLineIdx);
    }

    public int GetViewLineMaxColumn(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineMaxColumn(_simpleModel, info.ModelLineNumber, info.ModelLineWrappedLineIdx);
    }

    public bool GetViewLineContinuesWithWrappedLine(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineContinuesWithWrappedLine(info.ModelLineWrappedLineIdx);
    }

    public ViewLineData GetViewLineData(int viewLineNumber)
    {
        var info = GetViewLineInfo(viewLineNumber);
        var baseViewLineNumber = _projectedModelLineLineCounts.GetPrefixSum(info.ModelLineNumber - 1) + 1;
        return _modelLineProjections[info.ModelLineNumber - 1].GetViewLineData(_simpleModel, info.ModelLineNumber, info.ModelLineWrappedLineIdx, baseViewLineNumber);
    }

    public ViewLineData?[] GetViewLinesData(int viewStartLineNumber, int viewEndLineNumber, IReadOnlyList<bool> needed)
    {
        viewStartLineNumber = ToValidViewLineNumber(viewStartLineNumber);
        viewEndLineNumber = ToValidViewLineNumber(viewEndLineNumber);

        var start = _projectedModelLineLineCounts.GetIndexOf(viewStartLineNumber - 1);
        var viewLineNumber = viewStartLineNumber;
        var startModelLineIndex = start.Index;
        var startRemainder = start.Remainder;

        var result = new ViewLineData?[Math.Max(0, viewEndLineNumber - viewStartLineNumber + 1)];
        for (int modelLineIndex = startModelLineIndex, len = _model.GetLineCount(); modelLineIndex < len; modelLineIndex++)
        {
            var line = _modelLineProjections[modelLineIndex];
            if (!line.IsVisible())
            {
                continue;
            }

            var fromViewLineIndex = modelLineIndex == startModelLineIndex ? startRemainder : 0;
            var remainingViewLineCount = line.GetViewLineCount() - fromViewLineIndex;

            var lastLine = false;
            if (viewLineNumber + remainingViewLineCount > viewEndLineNumber)
            {
                lastLine = true;
                remainingViewLineCount = viewEndLineNumber - viewLineNumber + 1;
            }

            var baseViewLineNumber = _projectedModelLineLineCounts.GetPrefixSum(modelLineIndex) + 1;
            line.GetViewLinesData(_simpleModel, modelLineIndex + 1, fromViewLineIndex, remainingViewLineCount, baseViewLineNumber, viewLineNumber - viewStartLineNumber, needed, result);

            viewLineNumber += remainingViewLineCount;

            if (lastLine)
            {
                break;
            }
        }

        return result;
    }

    public TextPosition ValidateViewPosition(int viewLineNumber, int viewColumn, TextPosition expectedModelPosition)
    {
        viewLineNumber = ToValidViewLineNumber(viewLineNumber);

        var r = _projectedModelLineLineCounts.GetIndexOf(viewLineNumber - 1);
        var lineIndex = r.Index;
        var remainder = r.Remainder;

        var line = _modelLineProjections[lineIndex];

        var minColumn = line.GetViewLineMinColumn(_simpleModel, lineIndex + 1, remainder);
        var maxColumn = line.GetViewLineMaxColumn(_simpleModel, lineIndex + 1, remainder);
        if (viewColumn < minColumn)
        {
            viewColumn = minColumn;
        }

        if (viewColumn > maxColumn)
        {
            viewColumn = maxColumn;
        }

        var computedModelColumn = line.GetModelColumnOfViewPosition(remainder, viewColumn);
        var computedModelPosition = _model.ValidatePosition(new TextPosition(lineIndex + 1, computedModelColumn));

        if (computedModelPosition.Equals(expectedModelPosition))
        {
            return new TextPosition(viewLineNumber, viewColumn);
        }

        return ConvertModelPositionToViewPosition(expectedModelPosition.LineNumber, expectedModelPosition.Column);
    }

    public TextRange ValidateViewRange(TextRange viewRange, TextRange expectedModelRange)
    {
        var validViewStart = ValidateViewPosition(viewRange.StartLineNumber, viewRange.StartColumn, expectedModelRange.Start);
        var validViewEnd = ValidateViewPosition(viewRange.EndLineNumber, viewRange.EndColumn, expectedModelRange.End);
        return new TextRange(validViewStart.LineNumber, validViewStart.Column, validViewEnd.LineNumber, validViewEnd.Column);
    }

    public TextPosition ConvertViewPositionToModelPosition(int viewLineNumber, int viewColumn)
    {
        var info = GetViewLineInfo(viewLineNumber);

        var inputColumn = _modelLineProjections[info.ModelLineNumber - 1].GetModelColumnOfViewPosition(info.ModelLineWrappedLineIdx, viewColumn);
        return _model.ValidatePosition(new TextPosition(info.ModelLineNumber, inputColumn));
    }

    public TextRange ConvertViewRangeToModelRange(TextRange viewRange)
    {
        var start = ConvertViewPositionToModelPosition(viewRange.StartLineNumber, viewRange.StartColumn);
        var end = ConvertViewPositionToModelPosition(viewRange.EndLineNumber, viewRange.EndColumn);
        return new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column);
    }

    public TextPosition ConvertModelPositionToViewPosition(
        int modelLineNumber,
        int modelColumn,
        PositionAffinity affinity = PositionAffinity.None,
        bool allowZeroLineNumber = false,
        bool belowHiddenRanges = false)
    {
        var validPosition = _model.ValidatePosition(new TextPosition(modelLineNumber, modelColumn));
        var inputLineNumber = validPosition.LineNumber;
        var inputColumn = validPosition.Column;

        var lineIndex = inputLineNumber - 1;
        var lineIndexChanged = false;
        if (belowHiddenRanges)
        {
            while (lineIndex < _modelLineProjections.Count && !_modelLineProjections[lineIndex].IsVisible())
            {
                lineIndex++;
                lineIndexChanged = true;
            }
        }
        else
        {
            while (lineIndex > 0 && !_modelLineProjections[lineIndex].IsVisible())
            {
                lineIndex--;
                lineIndexChanged = true;
            }
        }

        if (lineIndex == 0 && !_modelLineProjections[lineIndex].IsVisible())
        {
            // Could not reach a real line
            // TODO@alexdima@hediet this isn't soo pretty
            return new TextPosition(allowZeroLineNumber ? 0 : 1, 1);
        }

        var deltaLineNumber = 1 + _projectedModelLineLineCounts.GetPrefixSum(lineIndex);

        TextPosition r;
        if (lineIndexChanged)
        {
            if (belowHiddenRanges)
            {
                r = _modelLineProjections[lineIndex].GetViewPositionOfModelPosition(deltaLineNumber, 1, affinity);
            }
            else
            {
                r = _modelLineProjections[lineIndex].GetViewPositionOfModelPosition(deltaLineNumber, _model.GetLineMaxColumn(lineIndex + 1), affinity);
            }
        }
        else
        {
            r = _modelLineProjections[inputLineNumber - 1].GetViewPositionOfModelPosition(deltaLineNumber, inputColumn, affinity);
        }

        return r;
    }

    /// <param name="modelRange">The model range.</param>
    /// <param name="affinity">The affinity in case of an empty range. Has no effect for non-empty ranges.</param>
    public TextRange ConvertModelRangeToViewRange(TextRange modelRange, PositionAffinity affinity = PositionAffinity.Left)
    {
        if (modelRange.IsEmpty)
        {
            var start = ConvertModelPositionToViewPosition(modelRange.StartLineNumber, modelRange.StartColumn, affinity);
            return new TextRange(start);
        }
        else
        {
            var start = ConvertModelPositionToViewPosition(modelRange.StartLineNumber, modelRange.StartColumn, PositionAffinity.Right);
            var end = ConvertModelPositionToViewPosition(modelRange.EndLineNumber, modelRange.EndColumn, PositionAffinity.Left);
            return new TextRange(start.LineNumber, start.Column, end.LineNumber, end.Column);
        }
    }

    public int GetViewLineNumberOfModelPosition(int modelLineNumber, int modelColumn)
    {
        var lineIndex = modelLineNumber - 1;
        if (_modelLineProjections[lineIndex].IsVisible())
        {
            // this model line is visible
            var visibleDeltaLineNumber = 1 + _projectedModelLineLineCounts.GetPrefixSum(lineIndex);
            return _modelLineProjections[lineIndex].GetViewLineNumberOfModelPosition(visibleDeltaLineNumber, modelColumn);
        }

        // this model line is not visible
        while (lineIndex > 0 && !_modelLineProjections[lineIndex].IsVisible())
        {
            lineIndex--;
        }

        if (lineIndex == 0 && !_modelLineProjections[lineIndex].IsVisible())
        {
            // Could not reach a real line
            return 1;
        }

        var deltaLineNumber = 1 + _projectedModelLineLineCounts.GetPrefixSum(lineIndex);
        return _modelLineProjections[lineIndex].GetViewLineNumberOfModelPosition(deltaLineNumber, _model.GetLineMaxColumn(lineIndex + 1));
    }

    public IReadOnlyList<ModelDecoration> GetDecorationsInRange(TextRange range, int ownerId, bool filterOutValidation, bool filterFontDecorations, bool onlyMinimapDecorations, bool onlyMarginDecorations)
    {
        var modelStart = ConvertViewPositionToModelPosition(range.StartLineNumber, range.StartColumn);
        var modelEnd = ConvertViewPositionToModelPosition(range.EndLineNumber, range.EndColumn);

        if (modelEnd.LineNumber - modelStart.LineNumber <= range.EndLineNumber - range.StartLineNumber)
        {
            // most likely there are no hidden lines => fast path
            // fetch decorations from column 1 to cover the case of wrapped lines that have whole line decorations at column 1
            return _model.GetDecorationsInRange(new TextRange(modelStart.LineNumber, 1, modelEnd.LineNumber, modelEnd.Column), ownerId, filterOutValidation, filterFontDecorations, onlyMinimapDecorations, onlyMarginDecorations);
        }

        var result = new List<ModelDecoration>();
        var modelStartLineIndex = modelStart.LineNumber - 1;
        var modelEndLineIndex = modelEnd.LineNumber - 1;

        TextPosition? reqStart = null;
        for (var modelLineIndex = modelStartLineIndex; modelLineIndex <= modelEndLineIndex; modelLineIndex++)
        {
            var line = _modelLineProjections[modelLineIndex];
            if (line.IsVisible())
            {
                // merge into previous request
                reqStart ??= new TextPosition(modelLineIndex + 1, modelLineIndex == modelStartLineIndex ? modelStart.Column : 1);
            }
            else
            {
                // hit invisible line => flush request
                if (reqStart is { } start)
                {
                    var maxLineColumn = _model.GetLineMaxColumn(modelLineIndex);
                    result.AddRange(_model.GetDecorationsInRange(new TextRange(start.LineNumber, start.Column, modelLineIndex, maxLineColumn), ownerId, filterOutValidation, filterFontDecorations, onlyMinimapDecorations));
                    reqStart = null;
                }
            }
        }

        if (reqStart is { } lastStart)
        {
            result.AddRange(_model.GetDecorationsInRange(new TextRange(lastStart.LineNumber, lastStart.Column, modelEnd.LineNumber, modelEnd.Column), ownerId, filterOutValidation, filterFontDecorations, onlyMinimapDecorations));
        }

        result.Sort((a, b) =>
        {
            var res = TextRange.CompareRangesUsingStarts(a.Range, b.Range);
            if (res == 0)
            {
                return string.CompareOrdinal(a.Id, b.Id) switch
                {
                    < 0 => -1,
                    > 0 => 1,
                    _ => 0,
                };
            }

            return res;
        });

        // Eliminate duplicate decorations that might have intersected our visible ranges multiple times
        var finalResult = new List<ModelDecoration>(result.Count);
        string? prevDecId = null;
        foreach (var dec in result)
        {
            var decId = dec.Id;
            if (prevDecId == decId)
            {
                // skip
                continue;
            }

            prevDecId = decId;
            finalResult.Add(dec);
        }

        return finalResult;
    }

    public InjectedText? GetInjectedTextAt(TextPosition viewPosition)
    {
        var info = GetViewLineInfo(viewPosition.LineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].GetInjectedTextAt(info.ModelLineWrappedLineIdx, viewPosition.Column);
    }

    public TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity)
    {
        var info = GetViewLineInfo(position.LineNumber);
        return _modelLineProjections[info.ModelLineNumber - 1].NormalizePosition(info.ModelLineWrappedLineIdx, position, affinity);
    }

    public int GetLineIndentColumn(int lineNumber)
    {
        var info = GetViewLineInfo(lineNumber);
        if (info.ModelLineWrappedLineIdx == 0)
        {
            return _model.GetLineIndentColumn(info.ModelLineNumber);
        }

        // wrapped lines have no indentation.
        // We deliberately don't handle the case that indentation is wrapped
        // to avoid two view lines reporting indentation for the very same model line.
        return 0;
    }

    /// <summary>
    /// Overlapping unsorted ranges <c>[   ) [ ) [  )</c> and <c>[    ) [       )</c> become non overlapping sorted ranges
    /// <c>[       )  [ ) [        )</c>. Note: This function only considers line information! Columns are ignored.
    /// </summary>
    private static List<TextRange> NormalizeLineRanges(List<TextRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return [];
        }

        var sortedRanges = new List<TextRange>(ranges);
        sortedRanges.Sort(TextRange.CompareRangesUsingStarts);

        var result = new List<TextRange>();
        var currentRangeStart = sortedRanges[0].StartLineNumber;
        var currentRangeEnd = sortedRanges[0].EndLineNumber;

        for (int i = 1, len = sortedRanges.Count; i < len; i++)
        {
            var range = sortedRanges[i];

            if (range.StartLineNumber > currentRangeEnd + 1)
            {
                result.Add(new TextRange(currentRangeStart, 1, currentRangeEnd, 1));
                currentRangeStart = range.StartLineNumber;
                currentRangeEnd = range.EndLineNumber;
            }
            else if (range.EndLineNumber > currentRangeEnd)
            {
                currentRangeEnd = range.EndLineNumber;
            }
        }

        result.Add(new TextRange(currentRangeStart, 1, currentRangeEnd, 1));
        return result;
    }

    /// <summary>The model as the line breaks computers read it: its lines and the text this editor injects into them.</summary>
    private sealed class ModelLineBreaksComputerContext : ILineBreaksComputerContext
    {
        private readonly TextModel _model;
        private readonly int _editorId;

        public ModelLineBreaksComputerContext(TextModel model, int editorId)
        {
            _model = model;
            _editorId = editorId;
        }

        public string GetLineContent(int lineNumber) => _model.GetLineContent(lineNumber);

        public IReadOnlyList<LineInjectedText>? GetLineInjectedText(int lineNumber) => _model.GetLineInjectedText(lineNumber, _editorId);
    }

    /// <summary>Represents a view line. Can be used to efficiently query more information about it.</summary>
    private readonly record struct ViewLineInfo(int ModelLineNumber, int ModelLineWrappedLineIdx)
    {
        public bool IsWrappedLineContinuation => ModelLineWrappedLineIdx > 0;
    }

    /// <summary>A list of view lines that have a contiguous span in the model.</summary>
    private sealed record ViewLineInfoGroupedByModelRange(TextRange ModelRange, List<ViewLineInfo> ViewLines);

    private sealed class CoordinatesConverter : ICoordinatesConverter
    {
        private readonly ViewModelLinesFromProjectedModel _lines;

        public CoordinatesConverter(ViewModelLinesFromProjectedModel lines)
        {
            _lines = lines;
        }

        // View -> Model conversion and related methods

        public TextPosition ConvertViewPositionToModelPosition(TextPosition viewPosition) =>
            _lines.ConvertViewPositionToModelPosition(viewPosition.LineNumber, viewPosition.Column);

        public TextRange ConvertViewRangeToModelRange(TextRange viewRange) => _lines.ConvertViewRangeToModelRange(viewRange);

        public TextPosition ValidateViewPosition(TextPosition viewPosition, TextPosition expectedModelPosition) =>
            _lines.ValidateViewPosition(viewPosition.LineNumber, viewPosition.Column, expectedModelPosition);

        public TextRange ValidateViewRange(TextRange viewRange, TextRange expectedModelRange) => _lines.ValidateViewRange(viewRange, expectedModelRange);

        // Model -> View conversion and related methods

        public TextPosition ConvertModelPositionToViewPosition(
            TextPosition modelPosition,
            PositionAffinity affinity = PositionAffinity.None,
            bool allowZeroLineNumber = false,
            bool belowHiddenRanges = false) =>
            _lines.ConvertModelPositionToViewPosition(modelPosition.LineNumber, modelPosition.Column, affinity, allowZeroLineNumber, belowHiddenRanges);

        public TextRange ConvertModelRangeToViewRange(TextRange modelRange, PositionAffinity affinity = PositionAffinity.Left) =>
            _lines.ConvertModelRangeToViewRange(modelRange, affinity);

        public bool ModelPositionIsVisible(TextPosition modelPosition) => _lines.ModelPositionIsVisible(modelPosition.LineNumber, modelPosition.Column);

        public int GetModelLineViewLineCount(int modelLineNumber) => _lines.GetModelLineViewLineCount(modelLineNumber);

        public int GetViewLineNumberOfModelPosition(int modelLineNumber, int modelColumn) => _lines.GetViewLineNumberOfModelPosition(modelLineNumber, modelColumn);
    }
}
