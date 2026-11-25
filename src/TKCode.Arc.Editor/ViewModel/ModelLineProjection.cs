// Ported from vscode (src/vs/editor/common/viewModel/modelLineProjection.ts, and model/textModel.ts:
// getLineTokensWithInjections, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - ISimpleModel has GetLineTokens where upstream has tokenization.getLineTokens; TextModelSimpleModel adapts a
//    TextModel to it;
//  - createModelLineProjection is ModelLineProjections.Create; the identity and hidden projections are singletons
//    without state;
//  - the padding of wrapped lines is built with new string(' ', n) instead of upstream's module-level cache of space
//    strings (static mutable state);
//  - getLineTokensWithInjections lives here (upstream: model/textModel.ts) and always gives injected text the default
//    token metadata: ModelDecorationInjectedTextOptions has no tokens yet;
//  - "Not supported" is an InvalidOperationException.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>How a model line shows in the view: hidden, as it is, or wrapped and with injected text.</summary>
internal interface IModelLineProjection
{
    bool IsVisible();

    /// <summary>This invalidates the current instance (potentially reuses and returns it again).</summary>
    IModelLineProjection SetVisible(bool isVisible);

    ModelLineProjectionData? GetProjectionData();

    int GetViewLineCount();

    string GetViewLineContent(ISimpleModel model, int modelLineNumber, int outputLineIndex);

    int GetViewLineLength(ISimpleModel model, int modelLineNumber, int outputLineIndex);

    int GetViewLineMinColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex);

    int GetViewLineMaxColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex);

    bool GetViewLineContinuesWithWrappedLine(int outputLineIndex);

    ViewLineData GetViewLineData(ISimpleModel model, int modelLineNumber, int outputLineIndex, int baseViewLineNumber);

    void GetViewLinesData(
        ISimpleModel model,
        int modelLineNumber,
        int outputLineIdx,
        int lineCount,
        int baseViewLineNumber,
        int globalStartIndex,
        IReadOnlyList<bool> needed,
        ViewLineData?[] result);

    int GetModelColumnOfViewPosition(int outputLineIndex, int outputColumn);

    TextPosition GetViewPositionOfModelPosition(int deltaLineNumber, int inputColumn, PositionAffinity affinity = PositionAffinity.None);

    int GetViewLineNumberOfModelPosition(int deltaLineNumber, int inputColumn);

    TextPosition NormalizePosition(int outputLineIndex, TextPosition outputPosition, PositionAffinity affinity);

    InjectedText? GetInjectedTextAt(int outputLineIndex, int column);
}

/// <summary>What a model line projection reads from the model.</summary>
internal interface ISimpleModel
{
    /// <summary>Upstream's <c>tokenization.getLineTokens</c>.</summary>
    LineTokens GetLineTokens(int lineNumber);

    string GetLineContent(int lineNumber);

    int GetLineLength(int lineNumber);

    int GetLineMinColumn(int lineNumber);

    int GetLineMaxColumn(int lineNumber);

    string GetValueInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined);
}

/// <summary>A <see cref="TextModel"/> as an <see cref="ISimpleModel"/>.</summary>
internal sealed class TextModelSimpleModel : ISimpleModel
{
    public TextModelSimpleModel(TextModel model)
    {
        Model = model;
    }

    public TextModel Model { get; }

    public LineTokens GetLineTokens(int lineNumber) => Model.Tokenization.GetLineTokens(lineNumber);

    public string GetLineContent(int lineNumber) => Model.GetLineContent(lineNumber);

    public int GetLineLength(int lineNumber) => Model.GetLineLength(lineNumber);

    public int GetLineMinColumn(int lineNumber) => Model.GetLineMinColumn(lineNumber);

    public int GetLineMaxColumn(int lineNumber) => Model.GetLineMaxColumn(lineNumber);

    public string GetValueInRange(TextRange range, EndOfLinePreference eol = EndOfLinePreference.TextDefined) => Model.GetValueInRange(range, eol);
}

internal static class ModelLineProjections
{
    /// <summary>Upstream's <c>createModelLineProjection</c>.</summary>
    public static IModelLineProjection Create(ModelLineProjectionData? lineBreakData, bool isVisible)
    {
        if (lineBreakData is null)
        {
            // No mapping needed
            if (isVisible)
            {
                return IdentityModelLineProjection.Instance;
            }

            return HiddenModelLineProjection.Instance;
        }

        return new ModelLineProjection(lineBreakData, isVisible);
    }

    /// <summary>Upstream's <c>getLineTokensWithInjections</c> (model/textModel.ts).</summary>
    public static LineTokens GetLineTokensWithInjections(
        LineTokens tokens,
        IReadOnlyList<ModelDecorationInjectedTextOptions>? injectionOptions,
        IReadOnlyList<int>? injectionOffsets)
    {
        if (injectionOffsets is null)
        {
            return tokens;
        }

        var tokensToInsert = new List<InsertedToken>(injectionOffsets.Count);
        for (var idx = 0; idx < injectionOffsets.Count; idx++)
        {
            tokensToInsert.Add(new InsertedToken(injectionOffsets[idx], injectionOptions![idx].Content, LineTokens.DefaultTokenMetadata));
        }

        return tokens.WithInserted(tokensToInsert);
    }

    internal static string Spaces(int count) => count <= 0 ? string.Empty : new string(' ', count);
}

/// <summary>This projection is used to wrap model lines and to inject text.</summary>
internal sealed class ModelLineProjection : IModelLineProjection
{
    private readonly ModelLineProjectionData _projectionData;
    private bool _isVisible;

    public ModelLineProjection(ModelLineProjectionData lineBreakData, bool isVisible)
    {
        _projectionData = lineBreakData;
        _isVisible = isVisible;
    }

    public bool IsVisible() => _isVisible;

    public IModelLineProjection SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        return this;
    }

    public ModelLineProjectionData? GetProjectionData() => _projectionData;

    public int GetViewLineCount()
    {
        if (!_isVisible)
        {
            return 0;
        }

        return _projectionData.GetOutputLineCount();
    }

    public string GetViewLineContent(ISimpleModel model, int modelLineNumber, int outputLineIndex)
    {
        AssertVisible();

        var startOffsetInInputWithInjections = outputLineIndex > 0 ? _projectionData.BreakOffsets[outputLineIndex - 1] : 0;
        var endOffsetInInputWithInjections = _projectionData.BreakOffsets[outputLineIndex];

        string r;
        if (_projectionData.InjectionOffsets is { } injectionOffsets)
        {
            var injectedTexts = new LineInjectedText[injectionOffsets.Length];
            for (var idx = 0; idx < injectionOffsets.Length; idx++)
            {
                injectedTexts[idx] = new LineInjectedText(0, 0, injectionOffsets[idx] + 1, _projectionData.InjectionOptions![idx], 0);
            }

            var lineWithInjections = LineInjectedText.ApplyInjectedText(model.GetLineContent(modelLineNumber), injectedTexts);
            r = lineWithInjections[startOffsetInInputWithInjections..endOffsetInInputWithInjections];
        }
        else
        {
            r = model.GetValueInRange(new TextRange(modelLineNumber, startOffsetInInputWithInjections + 1, modelLineNumber, endOffsetInInputWithInjections + 1));
        }

        if (outputLineIndex > 0)
        {
            r = ModelLineProjections.Spaces(_projectionData.WrappedTextIndentLength) + r;
        }

        return r;
    }

    public int GetViewLineLength(ISimpleModel model, int modelLineNumber, int outputLineIndex)
    {
        AssertVisible();
        return _projectionData.GetLineLength(outputLineIndex);
    }

    public int GetViewLineMinColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex)
    {
        AssertVisible();
        return _projectionData.GetMinOutputOffset(outputLineIndex) + 1;
    }

    public int GetViewLineMaxColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex)
    {
        AssertVisible();
        return _projectionData.GetMaxOutputOffset(outputLineIndex) + 1;
    }

    public bool GetViewLineContinuesWithWrappedLine(int outputLineIndex) => outputLineIndex + 1 < GetViewLineCount();

    /// <summary>Try using <see cref="GetViewLinesData"/> instead.</summary>
    public ViewLineData GetViewLineData(ISimpleModel model, int modelLineNumber, int outputLineIndex, int baseViewLineNumber)
    {
        var arr = new ViewLineData?[1];
        GetViewLinesData(model, modelLineNumber, outputLineIndex, 1, baseViewLineNumber, 0, [true], arr);
        return arr[0]!;
    }

    public void GetViewLinesData(
        ISimpleModel model,
        int modelLineNumber,
        int outputLineIdx,
        int lineCount,
        int baseViewLineNumber,
        int globalStartIndex,
        IReadOnlyList<bool> needed,
        ViewLineData?[] result)
    {
        AssertVisible();

        var lineBreakData = _projectionData;

        var injectionOffsets = lineBreakData.InjectionOffsets;
        var injectionOptions = lineBreakData.InjectionOptions;

        var context = new InjectedTextContext(lineBreakData, baseViewLineNumber);
        var computer = new InjectedTextInlineDecorationsComputer(context);
        var lineInlineDecorations = computer.GetInlineDecorations(modelLineNumber);
        var lineTokens = model.GetLineTokens(modelLineNumber);
        var lineWithInjections = ModelLineProjections.GetLineTokensWithInjections(lineTokens, injectionOptions, injectionOffsets);

        for (var outputLineIndex = outputLineIdx; outputLineIndex < outputLineIdx + lineCount; outputLineIndex++)
        {
            var globalIndex = globalStartIndex + outputLineIndex - outputLineIdx;
            if (globalIndex >= needed.Count || !needed[globalIndex])
            {
                result[globalIndex] = null;
                continue;
            }

            // Upstream indexes the array of the line's inline decorations, which is empty without injected text.
            var inlineDecorations = outputLineIndex < lineInlineDecorations.Count ? lineInlineDecorations[outputLineIndex] : null;
            result[globalIndex] = GetViewLineDataCore(lineWithInjections, inlineDecorations, outputLineIndex);
        }
    }

    private ViewLineData GetViewLineDataCore(LineTokens lineWithInjections, IReadOnlyList<InlineDecoration>? inlineDecorations, int outputLineIndex)
    {
        AssertVisible();
        var lineBreakData = _projectionData;
        var deltaStartIndex = outputLineIndex > 0 ? lineBreakData.WrappedTextIndentLength : 0;

        var lineStartOffsetInInputWithInjections = outputLineIndex > 0 ? lineBreakData.BreakOffsets[outputLineIndex - 1] : 0;
        var lineEndOffsetInInputWithInjections = lineBreakData.BreakOffsets[outputLineIndex];
        var tokens = lineWithInjections.SliceAndInflate(lineStartOffsetInInputWithInjections, lineEndOffsetInInputWithInjections, deltaStartIndex);

        var lineContent = tokens.GetLineContent();
        if (outputLineIndex > 0)
        {
            lineContent = ModelLineProjections.Spaces(lineBreakData.WrappedTextIndentLength) + lineContent;
        }

        var minColumn = _projectionData.GetMinOutputOffset(outputLineIndex) + 1;
        var maxColumn = lineContent.Length + 1;
        var continuesWithWrappedLine = GetViewLineContinuesWithWrappedLine(outputLineIndex);
        var startVisibleColumn = outputLineIndex == 0 ? 0 : lineBreakData.BreakOffsetsVisibleColumn[outputLineIndex - 1];

        return new ViewLineData(
            lineContent,
            continuesWithWrappedLine,
            minColumn,
            maxColumn,
            startVisibleColumn,
            tokens,
            inlineDecorations);
    }

    public int GetModelColumnOfViewPosition(int outputLineIndex, int outputColumn)
    {
        AssertVisible();
        return _projectionData.TranslateToInputOffset(outputLineIndex, outputColumn - 1) + 1;
    }

    public TextPosition GetViewPositionOfModelPosition(int deltaLineNumber, int inputColumn, PositionAffinity affinity = PositionAffinity.None)
    {
        AssertVisible();
        var r = _projectionData.TranslateToOutputPosition(inputColumn - 1, affinity);
        return r.ToPosition(deltaLineNumber);
    }

    public int GetViewLineNumberOfModelPosition(int deltaLineNumber, int inputColumn)
    {
        AssertVisible();
        var r = _projectionData.TranslateToOutputPosition(inputColumn - 1);
        return deltaLineNumber + r.OutputLineIndex;
    }

    public TextPosition NormalizePosition(int outputLineIndex, TextPosition outputPosition, PositionAffinity affinity)
    {
        var baseViewLineNumber = outputPosition.LineNumber - outputLineIndex;
        var normalizedOutputPosition = _projectionData.NormalizeOutputPosition(outputLineIndex, outputPosition.Column - 1, affinity);
        var result = normalizedOutputPosition.ToPosition(baseViewLineNumber);
        return result;
    }

    public InjectedText? GetInjectedTextAt(int outputLineIndex, int column) => _projectionData.GetInjectedText(outputLineIndex, column - 1);

    private void AssertVisible()
    {
        if (!_isVisible)
        {
            throw new InvalidOperationException("Not supported");
        }
    }

    private sealed class InjectedTextContext : IInjectedTextInlineDecorationsComputerContext
    {
        private readonly ModelLineProjectionData _lineBreakData;
        private readonly int _baseViewLineNumber;

        public InjectedTextContext(ModelLineProjectionData lineBreakData, int baseViewLineNumber)
        {
            _lineBreakData = lineBreakData;
            _baseViewLineNumber = baseViewLineNumber;
        }

        public IReadOnlyList<ModelDecorationInjectedTextOptions>? GetInjectionOptions(int modelLineNumber) => _lineBreakData.InjectionOptions;

        public IReadOnlyList<int>? GetInjectionOffsets(int modelLineNumber) => _lineBreakData.InjectionOffsets;

        public IReadOnlyList<int> GetBreakOffsets(int modelLineNumber) => _lineBreakData.BreakOffsets;

        public int GetWrappedTextIndentLength(int modelLineNumber) => _lineBreakData.WrappedTextIndentLength;

        public int GetBaseViewLineNumber(int modelLineNumber) => _baseViewLineNumber;
    }
}

/// <summary>This projection does not change the model line.</summary>
internal sealed class IdentityModelLineProjection : IModelLineProjection
{
    public static readonly IdentityModelLineProjection Instance = new();

    private IdentityModelLineProjection()
    {
    }

    public bool IsVisible() => true;

    public IModelLineProjection SetVisible(bool isVisible)
    {
        if (isVisible)
        {
            return this;
        }

        return HiddenModelLineProjection.Instance;
    }

    public ModelLineProjectionData? GetProjectionData() => null;

    public int GetViewLineCount() => 1;

    public string GetViewLineContent(ISimpleModel model, int modelLineNumber, int outputLineIndex) => model.GetLineContent(modelLineNumber);

    public int GetViewLineLength(ISimpleModel model, int modelLineNumber, int outputLineIndex) => model.GetLineLength(modelLineNumber);

    public int GetViewLineMinColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex) => model.GetLineMinColumn(modelLineNumber);

    public int GetViewLineMaxColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex) => model.GetLineMaxColumn(modelLineNumber);

    public bool GetViewLineContinuesWithWrappedLine(int outputLineIndex) => false;

    public ViewLineData GetViewLineData(ISimpleModel model, int modelLineNumber, int outputLineIndex, int baseViewLineNumber)
    {
        var lineTokens = model.GetLineTokens(modelLineNumber);
        var lineContent = lineTokens.GetLineContent();
        return new ViewLineData(
            lineContent,
            false,
            1,
            lineContent.Length + 1,
            0,
            lineTokens.Inflate(),
            null);
    }

    public void GetViewLinesData(
        ISimpleModel model,
        int modelLineNumber,
        int outputLineIdx,
        int lineCount,
        int baseViewLineNumber,
        int globalStartIndex,
        IReadOnlyList<bool> needed,
        ViewLineData?[] result)
    {
        if (globalStartIndex >= needed.Count || !needed[globalStartIndex])
        {
            result[globalStartIndex] = null;
            return;
        }

        result[globalStartIndex] = GetViewLineData(model, modelLineNumber, 0, baseViewLineNumber);
    }

    public int GetModelColumnOfViewPosition(int outputLineIndex, int outputColumn) => outputColumn;

    public TextPosition GetViewPositionOfModelPosition(int deltaLineNumber, int inputColumn, PositionAffinity affinity = PositionAffinity.None) =>
        new(deltaLineNumber, inputColumn);

    public int GetViewLineNumberOfModelPosition(int deltaLineNumber, int inputColumn) => deltaLineNumber;

    public TextPosition NormalizePosition(int outputLineIndex, TextPosition outputPosition, PositionAffinity affinity) => outputPosition;

    public InjectedText? GetInjectedTextAt(int outputLineIndex, int column) => null;
}

/// <summary>This projection hides the model line.</summary>
internal sealed class HiddenModelLineProjection : IModelLineProjection
{
    public static readonly HiddenModelLineProjection Instance = new();

    private HiddenModelLineProjection()
    {
    }

    public bool IsVisible() => false;

    public IModelLineProjection SetVisible(bool isVisible)
    {
        if (!isVisible)
        {
            return this;
        }

        return IdentityModelLineProjection.Instance;
    }

    public ModelLineProjectionData? GetProjectionData() => null;

    public int GetViewLineCount() => 0;

    public string GetViewLineContent(ISimpleModel model, int modelLineNumber, int outputLineIndex) => throw NotSupported();

    public int GetViewLineLength(ISimpleModel model, int modelLineNumber, int outputLineIndex) => throw NotSupported();

    public int GetViewLineMinColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex) => throw NotSupported();

    public int GetViewLineMaxColumn(ISimpleModel model, int modelLineNumber, int outputLineIndex) => throw NotSupported();

    public bool GetViewLineContinuesWithWrappedLine(int outputLineIndex) => throw NotSupported();

    public ViewLineData GetViewLineData(ISimpleModel model, int modelLineNumber, int outputLineIndex, int baseViewLineNumber) => throw NotSupported();

    public void GetViewLinesData(
        ISimpleModel model,
        int modelLineNumber,
        int outputLineIdx,
        int lineCount,
        int baseViewLineNumber,
        int globalStartIndex,
        IReadOnlyList<bool> needed,
        ViewLineData?[] result) => throw NotSupported();

    public int GetModelColumnOfViewPosition(int outputLineIndex, int outputColumn) => throw NotSupported();

    public TextPosition GetViewPositionOfModelPosition(int deltaLineNumber, int inputColumn, PositionAffinity affinity = PositionAffinity.None) =>
        throw NotSupported();

    public int GetViewLineNumberOfModelPosition(int deltaLineNumber, int inputColumn) => throw NotSupported();

    public TextPosition NormalizePosition(int outputLineIndex, TextPosition outputPosition, PositionAffinity affinity) => throw NotSupported();

    public InjectedText? GetInjectedTextAt(int outputLineIndex, int column) => throw NotSupported();

    private static InvalidOperationException NotSupported() => new("Not supported");
}
