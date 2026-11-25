// Ported from vscode (src/vs/editor/common/viewModel/viewModelLines.ts: ViewModelLinesFromModelAsIs, commit b57d5c9e),
// MIT license.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The view lines of a model shown as it is: no hidden lines, no wrapping, no injected text.</summary>
internal sealed class ViewModelLinesFromModelAsIs : IViewModelLines
{
    public ViewModelLinesFromModelAsIs(TextModel model)
    {
        Model = model;
    }

    public TextModel Model { get; }

    public void Dispose()
    {
    }

    public ICoordinatesConverter CreateCoordinatesConverter() => new IdentityCoordinatesConverter(Model);

    public IReadOnlyList<TextRange> GetHiddenAreas() => [];

    public bool SetHiddenAreas(IReadOnlyList<TextRange> ranges) => false;

    public bool SetTabSize(int newTabSize) => false;

    public bool SetWrappingSettings(FontInfo fontInfo, WrappingStrategy wrappingStrategy, int wrappingColumn, WrappingIndent wrappingIndent, WordBreak wordBreak, bool useTwoCellFullwidthCharacters) => false;

    public ILineBreaksComputer CreateLineBreaksComputer(ILineBreaksComputerContext? context = null) => new NullLineBreaksComputer();

    public void OnModelFlushed()
    {
    }

    public ViewLinesDeletedEvent? OnModelLinesDeleted(int? versionId, int fromLineNumber, int toLineNumber) =>
        new(fromLineNumber, toLineNumber);

    public ViewLinesInsertedEvent? OnModelLinesInserted(int? versionId, int fromLineNumber, int toLineNumber, IReadOnlyList<ModelLineProjectionData?> lineBreaks) =>
        new(fromLineNumber, toLineNumber);

    public ModelLineChangedResult OnModelLineChanged(int? versionId, int lineNumber, ModelLineProjectionData? lineBreakData) =>
        new(false, new ViewLinesChangedEvent(lineNumber, 1), null, null);

    public void AcceptVersionId(int versionId)
    {
    }

    public int GetViewLineCount() => Model.GetLineCount();

    public ActiveIndentGuideInfo GetActiveIndentGuide(int viewLineNumber, int minLineNumber, int maxLineNumber) =>
        new(viewLineNumber, viewLineNumber, 0);

    public IReadOnlyList<IReadOnlyList<IndentGuide>> GetViewLinesBracketGuides(int startLineNumber, int endLineNumber, TextPosition? activePosition, BracketGuideOptions options)
    {
        var result = new IReadOnlyList<IndentGuide>[Math.Max(0, endLineNumber - startLineNumber + 1)];
        Array.Fill(result, Array.Empty<IndentGuide>());
        return result;
    }

    public IReadOnlyList<int> GetViewLinesIndentGuides(int viewStartLineNumber, int viewEndLineNumber)
    {
        var viewLineCount = viewEndLineNumber - viewStartLineNumber + 1;
        return new int[Math.Max(0, viewLineCount)];
    }

    public string GetViewLineContent(int viewLineNumber) => Model.GetLineContent(viewLineNumber);

    public int GetViewLineLength(int viewLineNumber) => Model.GetLineLength(viewLineNumber);

    public int GetViewLineMinColumn(int viewLineNumber) => Model.GetLineMinColumn(viewLineNumber);

    public int GetViewLineMaxColumn(int viewLineNumber) => Model.GetLineMaxColumn(viewLineNumber);

    public bool GetViewLineContinuesWithWrappedLine(int viewLineNumber) => false;

    public ViewLineData GetViewLineData(int viewLineNumber)
    {
        var lineTokens = Model.Tokenization.GetLineTokens(viewLineNumber);
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

    public ViewLineData?[] GetViewLinesData(int viewStartLineNumber, int viewEndLineNumber, IReadOnlyList<bool> needed)
    {
        var lineCount = Model.GetLineCount();
        viewStartLineNumber = Math.Min(Math.Max(1, viewStartLineNumber), lineCount);
        viewEndLineNumber = Math.Min(Math.Max(1, viewEndLineNumber), lineCount);

        var result = new ViewLineData?[Math.Max(0, viewEndLineNumber - viewStartLineNumber + 1)];
        for (var lineNumber = viewStartLineNumber; lineNumber <= viewEndLineNumber; lineNumber++)
        {
            var idx = lineNumber - viewStartLineNumber;
            result[idx] = idx < needed.Count && needed[idx] ? GetViewLineData(lineNumber) : null;
        }

        return result;
    }

    public IReadOnlyList<ModelDecoration> GetDecorationsInRange(TextRange range, int ownerId, bool filterOutValidation, bool filterFontDecorations, bool onlyMinimapDecorations, bool onlyMarginDecorations) =>
        Model.GetDecorationsInRange(range, ownerId, filterOutValidation, filterFontDecorations, onlyMinimapDecorations, onlyMarginDecorations);

    public TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity) => Model.NormalizePosition(position, affinity);

    public int GetLineIndentColumn(int lineNumber) => Model.GetLineIndentColumn(lineNumber);

    public InjectedText? GetInjectedTextAt(TextPosition viewPosition)
    {
        // Identity lines collection does not support injected text.
        return null;
    }

    private sealed class NullLineBreaksComputer : ILineBreaksComputer
    {
        private int _count;

        public void AddRequest(int lineNumber, ModelLineProjectionData? previousLineBreakData) => _count++;

        public ModelLineProjectionData?[] Finalize() => new ModelLineProjectionData?[_count];
    }
}
