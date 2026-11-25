// Ported from vscode (src/vs/editor/common/viewModel/viewModelLines.ts: IViewModelLines, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - onModelLineChanged returns a ModelLineChangedResult instead of a tuple;
//  - wrappingStrategy and wordBreak are the enums WrappingStrategy and WordBreak.

using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>The view lines of a model: which model lines are hidden, how they wrap, what text is injected into them.</summary>
internal interface IViewModelLines : IDisposable
{
    ICoordinatesConverter CreateCoordinatesConverter();

    bool SetWrappingSettings(FontInfo fontInfo, WrappingStrategy wrappingStrategy, int wrappingColumn, WrappingIndent wrappingIndent, WordBreak wordBreak, bool useTwoCellFullwidthCharacters);

    bool SetTabSize(int newTabSize);

    IReadOnlyList<TextRange> GetHiddenAreas();

    bool SetHiddenAreas(IReadOnlyList<TextRange> ranges);

    ILineBreaksComputer CreateLineBreaksComputer(ILineBreaksComputerContext? context = null);

    void OnModelFlushed();

    ViewLinesDeletedEvent? OnModelLinesDeleted(int? versionId, int fromLineNumber, int toLineNumber);

    ViewLinesInsertedEvent? OnModelLinesInserted(int? versionId, int fromLineNumber, int toLineNumber, IReadOnlyList<ModelLineProjectionData?> lineBreaks);

    ModelLineChangedResult OnModelLineChanged(int? versionId, int lineNumber, ModelLineProjectionData? lineBreakData);

    void AcceptVersionId(int versionId);

    int GetViewLineCount();

    ActiveIndentGuideInfo GetActiveIndentGuide(int viewLineNumber, int minLineNumber, int maxLineNumber);

    IReadOnlyList<int> GetViewLinesIndentGuides(int viewStartLineNumber, int viewEndLineNumber);

    IReadOnlyList<IReadOnlyList<IndentGuide>> GetViewLinesBracketGuides(int startLineNumber, int endLineNumber, TextPosition? activePosition, BracketGuideOptions options);

    string GetViewLineContent(int viewLineNumber);

    int GetViewLineLength(int viewLineNumber);

    int GetViewLineMinColumn(int viewLineNumber);

    int GetViewLineMaxColumn(int viewLineNumber);

    bool GetViewLineContinuesWithWrappedLine(int viewLineNumber);

    ViewLineData GetViewLineData(int viewLineNumber);

    ViewLineData?[] GetViewLinesData(int viewStartLineNumber, int viewEndLineNumber, IReadOnlyList<bool> needed);

    IReadOnlyList<ModelDecoration> GetDecorationsInRange(TextRange range, int ownerId, bool filterOutValidation, bool filterFontDecorations, bool onlyMinimapDecorations, bool onlyMarginDecorations);

    InjectedText? GetInjectedTextAt(TextPosition viewPosition);

    TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity);

    /// <summary>Gets the column at which indentation stops at a given line.</summary>
    int GetLineIndentColumn(int lineNumber);
}

/// <summary>
/// The result of <see cref="IViewModelLines.OnModelLineChanged"/>: whether the line mapping changed, and the view lines
/// that changed, were inserted and were deleted.
/// </summary>
internal readonly record struct ModelLineChangedResult(
    bool LineMappingChanged,
    ViewLinesChangedEvent? LinesChangedEvent,
    ViewLinesInsertedEvent? LinesInsertedEvent,
    ViewLinesDeletedEvent? LinesDeletedEvent);
