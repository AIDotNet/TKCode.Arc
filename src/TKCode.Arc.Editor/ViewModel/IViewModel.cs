// Ported from vscode (src/vs/editor/common/viewModel.ts: IViewModel, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the two members the text model calls (onDidChangeContentOrInjectedText and emitContentChangeEvent) are in a base
//    interface of their own, IViewModelContentListener, which is what the text model registers;
//  - upstream's IViewModel extends ICursorSimpleModel and the screen reader's ISimpleModel
//    (viewModel/screenReaderSimpleModel.ts); ICursorSimpleModel is a base interface here, and ISimpleModel's members
//    that ICursorSimpleModel does not have (getValueInRange, getValueLengthInRange, modifyPosition) are declared here,
//    since this namespace already has an ISimpleModel (the one the model line projections read from);
//  - getEditorOption(id) is the Configuration property (ViewModelConfiguration.cs), whose Options record has the options
//    the view model reads; glyphLanes (IGlyphMarginLanesModel) is left out until that type is ported;
//  - getAllOverviewRulerDecorations takes the theme as an opaque object (as ViewThemeChangedEvent carries it; upstream's
//    EditorTheme is not ported);
//  - model is the TextModel (upstream's ITextModel); arrays are read-only lists; pixel values are doubles;
//  - getPlainTextToCopy returns a tuple with one text (SourceText) or one text per range (SourceTexts) where upstream
//    returns string | string[]; getRichTextToCopy returns a nullable tuple.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>
/// The part of a view model that the text model calls. The model tells it about every content and injected text change
/// first and at once, before the deferred <see cref="TextModel.DidChangeContent"/> event, so that it can keep its line
/// mapping in step.
/// </summary>
internal interface IViewModelContentListener
{
    /// <summary>Update the view model's own state for the change.</summary>
    void OnDidChangeContentOrInjectedText(IModelContentOrInjectedTextChangedEvent e);

    /// <summary>Raise the view model's events for the change; called after every view model has updated.</summary>
    void EmitContentChangeEvent(IModelContentOrInjectedTextChangedEvent e);
}

/// <summary>
/// The view model of an editor: the model's lines as the view shows them (wrapped, folded, with injected text), the
/// cursors and the decorations, in view coordinates.
/// </summary>
internal interface IViewModel : ICursorSimpleModel, IViewModelContentListener
{
    /// <summary>The text model this view model shows.</summary>
    TextModel Model { get; }

    /// <summary>Converts between model and view coordinates.</summary>
    ICoordinatesConverter CoordinatesConverter { get; }

    /// <summary>The cursor configuration (options and language rules) for the model's language.</summary>
    CursorConfiguration CursorConfig { get; }

    /// <summary>The vertical layout of the view lines and the scroll state over it.</summary>
    IViewLayout ViewLayout { get; }

    /// <summary>The editor configuration the view model reads (upstream's getEditorOption reads from it).</summary>
    ViewModelConfiguration Configuration { get; }

    void AddViewEventHandler(ViewEventHandler eventHandler);

    void RemoveViewEventHandler(ViewEventHandler eventHandler);

    /// <summary>Gives a hint that a lot of requests are about to come in for these line numbers.</summary>
    void SetViewport(int startLineNumber, int endLineNumber, int centeredLineNumber);

    void VisibleLinesStabilized();

    void SetHasFocus(bool hasFocus);

    void SetHasWidgetFocus(bool hasWidgetFocus);

    void OnCompositionStart();

    void OnCompositionEnd();

    string? GetFontSizeAtPosition(TextPosition position);

    IReadOnlyList<ViewModelDecoration> GetMinimapDecorationsInRange(TextRange range);

    IReadOnlyList<ViewModelDecoration> GetDecorationsInViewport(TextRange visibleRange);

    TextDirection GetTextDirection(int lineNumber);

    ViewLineRenderingData GetViewportViewLineRenderingData(TextRange visibleRange, int lineNumber);

    ViewLineRenderingData GetViewLineRenderingData(int lineNumber);

    bool GetViewLineContinuesWithWrappedLine(int lineNumber);

    ViewLineData GetViewLineData(int lineNumber);

    MinimapLinesRenderingData GetMinimapLinesRenderingData(int startLineNumber, int endLineNumber, IReadOnlyList<bool> needed);

    /// <summary>The overview ruler decorations grouped by color; <paramref name="theme"/> is opaque (upstream's EditorTheme).</summary>
    IReadOnlyList<OverviewRulerDecorationsGroup> GetAllOverviewRulerDecorations(object theme);

    TextRange GetCompletelyVisibleViewRange();

    TextRange GetCompletelyVisibleViewRangeAtScrollTop(double scrollTop);

    TextRange GetViewRangeWithCursorPadding(TextRange viewRange);

    /// <summary>The model ranges hidden by folding.</summary>
    IReadOnlyList<TextRange> GetHiddenAreas();

    int GetLineLength(int lineNumber);

    ActiveIndentGuideInfo GetActiveIndentGuide(int lineNumber, int minLineNumber, int maxLineNumber);

    IReadOnlyList<int> GetLinesIndentGuides(int startLineNumber, int endLineNumber);

    IReadOnlyList<IReadOnlyList<IndentGuide>> GetBracketGuidesInRangeByLine(
        int startLineNumber,
        int endLineNumber,
        TextPosition? activePosition,
        BracketGuideOptions options);

    string GetValueInRange(TextRange range, EndOfLinePreference eol);

    int GetValueLengthInRange(TextRange range, EndOfLinePreference eol);

    TextPosition ModifyPosition(TextPosition position, int offset);

    InjectedText? GetInjectedTextAt(TextPosition viewPosition);

    TextPosition DeduceModelPositionRelativeToViewPosition(TextPosition viewAnchorPosition, int deltaOffset, int lineFeedCnt);

    /// <summary>
    /// The text to copy for <paramref name="modelRanges"/>: either one text (<c>SourceText</c>) or one text per range
    /// (<c>SourceTexts</c>, for a multi cursor paste); the other is null.
    /// </summary>
    (IReadOnlyList<TextRange> SourceRanges, string? SourceText, IReadOnlyList<string>? SourceTexts) GetPlainTextToCopy(
        IReadOnlyList<TextRange> modelRanges,
        bool emptySelectionClipboard,
        bool forceCRLF);

    (string Html, string Mode)? GetRichTextToCopy(IReadOnlyList<TextRange> modelRanges, bool emptySelectionClipboard);

    ILineBreaksComputer CreateLineBreaksComputer(ILineBreaksComputerContext? context = null);

    // #region cursor

    CursorState GetPrimaryCursorState();

    int GetLastAddedCursorIndex();

    /// <summary>The cursor states; the first one is the primary cursor.</summary>
    IReadOnlyList<CursorState> GetCursorStates();

    /// <summary>Sets the cursor states (null leaves them as they are); returns whether the cursors changed.</summary>
    bool SetCursorStates(string? source, CursorChangeReason reason, IReadOnlyList<PartialCursorState>? states);

    ColumnSelectData GetCursorColumnSelectData();

    IReadOnlyList<TextRange> GetCursorAutoClosedCharacters();

    void SetCursorColumnSelectData(ColumnSelectData columnSelectData);

    EditOperationType GetPrevEditOperationType();

    void SetPrevEditOperationType(EditOperationType type);

    void RevealAllCursors(string? source, bool revealHorizontal, bool minimalReveal = false);

    void RevealPrimaryCursor(string? source, bool revealHorizontal, bool minimalReveal = false);

    void RevealTopMostCursor(string? source);

    void RevealBottomMostCursor(string? source);

    void RevealRange(string? source, bool revealHorizontal, TextRange viewRange, VerticalRevealType verticalType, ScrollType scrollType);

    // #endregion

    // #region viewLayout

    void ChangeWhitespace(Action<IWhitespaceChangeAccessor> callback);

    // #endregion

    void BatchEvents(Action callback);
}
