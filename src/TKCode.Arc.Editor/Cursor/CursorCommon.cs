// Ported from vscode (src/vs/editor/common/cursorCommon.ts and src/vs/editor/common/inputMode.ts, commit b57d5c9e),
// MIT license.
//
// Differences from upstream:
//  - upstream's InputMode is one process-wide mutable singleton; here the input mode (insert or overtype) is part of the
//    configuration (EditorInputMode), so every editor has its own and no state is shared;
//  - the options come from EditorOptions (the cursor's part of upstream's editor options) instead of an
//    IEditorConfiguration, and ShouldRecreate takes the old and the new options instead of a change event;
//  - CharacterMap is a dictionary; shouldAutoCloseBefore is three Func<char, bool>;
//  - cursor states keep upstream's shapes; PartialCursorState is one class with a model or a view state.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>What a column selection was started from and goes to, in view coordinates.</summary>
internal readonly record struct ColumnSelectData(
    bool IsReal,
    int FromViewLineNumber,
    int FromViewVisualColumn,
    int ToViewLineNumber,
    int ToViewVisualColumn);

/// <summary>
/// The kind of an edit, recorded for undo: the controller adds an undo stop when it switches from one kind to another.
/// </summary>
internal enum EditOperationType
{
    Other = 0,
    DeletingLeft = 2,
    DeletingRight = 3,
    TypingOther = 4,
    TypingFirstSpace = 5,
    TypingConsecutiveSpace = 6,
}

/// <summary>Whether typed characters are inserted or type over the existing ones.</summary>
internal enum EditorInputMode
{
    Insert,
    Overtype,
}

/// <summary>The options, model options and language rules the cursor operations read, resolved for one language.</summary>
internal sealed class CursorConfiguration
{
    private static readonly Func<char, bool> s_autoCloseAlways = static _ => true;
    private static readonly Func<char, bool> s_autoCloseNever = static _ => false;
    private static readonly Func<char, bool> s_autoCloseBeforeWhitespace = static chr => chr is ' ' or '\t';

    private readonly string _languageId;
    private HashSet<char>? _electricChars;

    public CursorConfiguration(
        string languageId,
        TextModelResolvedOptions modelOptions,
        EditorOptions options,
        LanguageConfigurationService languageConfigurationService,
        EditorInputMode inputMode = EditorInputMode.Insert)
    {
        _languageId = languageId;
        LanguageConfigurationService = languageConfigurationService;
        InputMode = inputMode;

        ReadOnly = options.ReadOnly;
        TabSize = modelOptions.TabSize;
        IndentSize = modelOptions.IndentSize;
        InsertSpaces = modelOptions.InsertSpaces;
        StickyTabStops = options.StickyTabStops;
        LineHeight = options.LineHeight;
        TypicalHalfwidthCharacterWidth = options.TypicalHalfwidthCharacterWidth;
        PageSize = Math.Max(1, (int)Math.Floor(options.LayoutHeight / LineHeight) - 2);
        UseTabStops = options.UseTabStops;
        TrimWhitespaceOnDelete = options.TrimWhitespaceOnDelete;
        WordSeparators = options.WordSeparators;
        EmptySelectionClipboard = options.EmptySelectionClipboard;
        CopyWithSyntaxHighlighting = options.CopyWithSyntaxHighlighting;
        MultiCursorMergeOverlapping = options.MultiCursorMergeOverlapping;
        MultiCursorPaste = options.MultiCursorPaste;
        MultiCursorLimit = options.MultiCursorLimit;
        AutoClosingBrackets = options.AutoClosingBrackets;
        AutoClosingComments = options.AutoClosingComments;
        AutoClosingQuotes = options.AutoClosingQuotes;
        AutoClosingDelete = options.AutoClosingDelete;
        AutoClosingOvertype = options.AutoClosingOvertype;
        AutoSurround = options.AutoSurround;
        AutoIndent = options.AutoIndent;
        WordSegmenterLocales = options.WordSegmenterLocales;
        OvertypeOnPaste = options.OvertypeOnPaste;

        var surroundingPairs = new Dictionary<string, string>();

        ShouldAutoCloseBefore = new AutoCloseBefore(
            Quote: GetShouldAutoClose(languageId, AutoClosingQuotes, true),
            Bracket: GetShouldAutoClose(languageId, AutoClosingBrackets, false),
            Comment: GetShouldAutoClose(languageId, AutoClosingComments, false));

        var languageConfiguration = LanguageConfigurationService.GetLanguageConfiguration(languageId);
        AutoClosingPairs = languageConfiguration.GetAutoClosingPairs();

        foreach (var pair in languageConfiguration.GetSurroundingPairs())
        {
            surroundingPairs[pair.Open] = pair.Close;
        }

        SurroundingPairs = surroundingPairs;
        BlockCommentStartToken = languageConfiguration.Comments?.BlockCommentStartToken;
    }

    public LanguageConfigurationService LanguageConfigurationService { get; }

    public bool ReadOnly { get; }

    public int TabSize { get; }

    public int IndentSize { get; }

    public bool InsertSpaces { get; }

    public bool StickyTabStops { get; }

    /// <summary>How many lines a page up or down moves.</summary>
    public int PageSize { get; }

    public double LineHeight { get; }

    public double TypicalHalfwidthCharacterWidth { get; }

    public bool UseTabStops { get; }

    public bool TrimWhitespaceOnDelete { get; }

    public string WordSeparators { get; }

    public bool EmptySelectionClipboard { get; }

    public bool CopyWithSyntaxHighlighting { get; }

    public bool MultiCursorMergeOverlapping { get; }

    public MultiCursorPaste MultiCursorPaste { get; }

    public int MultiCursorLimit { get; }

    public EditorAutoClosingStrategy AutoClosingBrackets { get; }

    public EditorAutoClosingStrategy AutoClosingComments { get; }

    public EditorAutoClosingStrategy AutoClosingQuotes { get; }

    public EditorAutoClosingEditStrategy AutoClosingDelete { get; }

    public EditorAutoClosingEditStrategy AutoClosingOvertype { get; }

    public EditorAutoSurroundStrategy AutoSurround { get; }

    public EditorAutoIndentStrategy AutoIndent { get; }

    public AutoClosingPairs AutoClosingPairs { get; }

    /// <summary>Opening character to closing character, for wrapping a selection.</summary>
    public IReadOnlyDictionary<string, string> SurroundingPairs { get; }

    public string? BlockCommentStartToken { get; }

    /// <summary>Whether to auto close before a character, for quotes, brackets and comments.</summary>
    public AutoCloseBefore ShouldAutoCloseBefore { get; }

    public IReadOnlyList<string> WordSegmenterLocales { get; }

    public bool OvertypeOnPaste { get; }

    public EditorInputMode InputMode { get; }

    /// <summary>Whether a change from <paramref name="oldOptions"/> to <paramref name="newOptions"/> needs a new configuration.</summary>
    public static bool ShouldRecreate(EditorOptions oldOptions, EditorOptions newOptions) =>
        oldOptions.LayoutHeight != newOptions.LayoutHeight
        || oldOptions.WordSeparators != newOptions.WordSeparators
        || oldOptions.EmptySelectionClipboard != newOptions.EmptySelectionClipboard
        || oldOptions.MultiCursorMergeOverlapping != newOptions.MultiCursorMergeOverlapping
        || oldOptions.MultiCursorPaste != newOptions.MultiCursorPaste
        || oldOptions.MultiCursorLimit != newOptions.MultiCursorLimit
        || oldOptions.AutoClosingBrackets != newOptions.AutoClosingBrackets
        || oldOptions.AutoClosingComments != newOptions.AutoClosingComments
        || oldOptions.AutoClosingQuotes != newOptions.AutoClosingQuotes
        || oldOptions.AutoClosingDelete != newOptions.AutoClosingDelete
        || oldOptions.AutoClosingOvertype != newOptions.AutoClosingOvertype
        || oldOptions.AutoSurround != newOptions.AutoSurround
        || oldOptions.UseTabStops != newOptions.UseTabStops
        || oldOptions.TrimWhitespaceOnDelete != newOptions.TrimWhitespaceOnDelete
        || oldOptions.LineHeight != newOptions.LineHeight
        || oldOptions.TypicalHalfwidthCharacterWidth != newOptions.TypicalHalfwidthCharacterWidth
        || oldOptions.ReadOnly != newOptions.ReadOnly
        || !oldOptions.WordSegmenterLocales.SequenceEqual(newOptions.WordSegmenterLocales)
        || oldOptions.OvertypeOnPaste != newOptions.OvertypeOnPaste;

    /// <summary>The characters that may re-indent a line when typed (the language's electric characters).</summary>
    public IReadOnlySet<char> ElectricChars
    {
        get
        {
            if (_electricChars is null)
            {
                var electricChars = new HashSet<char>();
                foreach (var chr in LanguageConfigurationService.GetLanguageConfiguration(_languageId).ElectricCharacter.GetElectricCharacters())
                {
                    if (chr.Length == 1)
                    {
                        electricChars.Add(chr[0]);
                    }
                }

                _electricChars = electricChars;
            }

            return _electricChars;
        }
    }

    /// <summary>The opening bracket to match the indentation of, when <paramref name="character"/> is typed.</summary>
    public ElectricAction? OnElectricCharacter(string character, LineTokens context, int column)
    {
        var scopedLineTokens = Supports.CreateScopedLineTokens(context, column - 1);
        var electricCharacterSupport = LanguageConfigurationService.GetLanguageConfiguration(scopedLineTokens.LanguageId).ElectricCharacter;
        return electricCharacterSupport.OnElectricCharacter(character, scopedLineTokens, column - scopedLineTokens.FirstCharOffset);
    }

    public string NormalizeIndentation(string str) => Indentation.NormalizeIndentation(str, IndentSize, InsertSpaces);

    private Func<char, bool> GetShouldAutoClose(string languageId, EditorAutoClosingStrategy autoCloseConfig, bool forQuotes) =>
        autoCloseConfig switch
        {
            EditorAutoClosingStrategy.BeforeWhitespace => s_autoCloseBeforeWhitespace,
            EditorAutoClosingStrategy.LanguageDefined => GetLanguageDefinedShouldAutoClose(languageId, forQuotes),
            EditorAutoClosingStrategy.Always => s_autoCloseAlways,
            _ => s_autoCloseNever,
        };

    private Func<char, bool> GetLanguageDefinedShouldAutoClose(string languageId, bool forQuotes)
    {
        var autoCloseBeforeSet = LanguageConfigurationService.GetLanguageConfiguration(languageId).GetAutoCloseBeforeSet(forQuotes);
        return c => autoCloseBeforeSet.Contains(c);
    }

    /// <summary>A visible column from a column (see <see cref="CursorColumns"/>).</summary>
    public int VisibleColumnFromColumn(ICursorSimpleModel model, TextPosition position) =>
        CursorColumns.VisibleColumnFromColumn(model.GetLineContent(position.LineNumber), position.Column, TabSize);

    /// <summary>A column from a visible column, clamped to the line (see <see cref="CursorColumns"/>).</summary>
    public int ColumnFromVisibleColumn(ICursorSimpleModel model, int lineNumber, int visibleColumn)
    {
        var result = CursorColumns.ColumnFromVisibleColumn(model.GetLineContent(lineNumber), visibleColumn, TabSize);

        var minColumn = model.GetLineMinColumn(lineNumber);
        if (result < minColumn)
        {
            return minColumn;
        }

        var maxColumn = model.GetLineMaxColumn(lineNumber);
        if (result > maxColumn)
        {
            return maxColumn;
        }

        return result;
    }

    /// <summary>Whether to auto close before a character, by kind of pair.</summary>
    internal sealed record AutoCloseBefore(Func<char, bool> Quote, Func<char, bool> Bracket, Func<char, bool> Comment);
}

/// <summary>A simple model the cursor works on: the text model, or the view model.</summary>
internal interface ICursorSimpleModel
{
    int GetLineCount();

    string GetLineContent(int lineNumber);

    int GetLineMinColumn(int lineNumber);

    int GetLineMaxColumn(int lineNumber);

    int GetLineFirstNonWhitespaceColumn(int lineNumber);

    int GetLineLastNonWhitespaceColumn(int lineNumber);

    TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity);

    /// <summary>The column at which indentation stops on a line.</summary>
    int GetLineIndentColumn(int lineNumber);
}

/// <summary>How the selection was started: by a click, a double click (a word) or a triple click (a line).</summary>
internal enum SelectionStartKind
{
    Simple,
    Word,
    Line,
}

/// <summary>The cursor state on either the model or the view model.</summary>
internal sealed class SingleCursorState : IEquatable<SingleCursorState>
{
    public SingleCursorState(
        TextRange selectionStart,
        SelectionStartKind selectionStartKind,
        int selectionStartLeftoverVisibleColumns,
        TextPosition position,
        int leftoverVisibleColumns)
    {
        SelectionStart = selectionStart;
        SelectionStartKind = selectionStartKind;
        SelectionStartLeftoverVisibleColumns = selectionStartLeftoverVisibleColumns;
        Position = position;
        LeftoverVisibleColumns = leftoverVisibleColumns;
        Selection = ComputeSelection(SelectionStart, Position);
    }

    /// <summary>What the selection was started on: an empty range, or a word or line for double and triple clicks.</summary>
    public TextRange SelectionStart { get; }

    public SelectionStartKind SelectionStartKind { get; }

    public int SelectionStartLeftoverVisibleColumns { get; }

    public TextPosition Position { get; }

    /// <summary>Visible columns past the end of a short line, kept while moving up and down.</summary>
    public int LeftoverVisibleColumns { get; }

    public TextSelection Selection { get; }

    public bool Equals(SingleCursorState? other) =>
        other is not null
        && SelectionStartLeftoverVisibleColumns == other.SelectionStartLeftoverVisibleColumns
        && LeftoverVisibleColumns == other.LeftoverVisibleColumns
        && SelectionStartKind == other.SelectionStartKind
        && Position == other.Position
        && SelectionStart == other.SelectionStart;

    public override bool Equals(object? obj) => Equals(obj as SingleCursorState);

    public override int GetHashCode() =>
        HashCode.Combine(SelectionStart, SelectionStartKind, SelectionStartLeftoverVisibleColumns, Position, LeftoverVisibleColumns);

    public bool HasSelection() => !Selection.IsEmpty || !SelectionStart.IsEmpty;

    public SingleCursorState Move(bool inSelectionMode, int lineNumber, int column, int leftoverVisibleColumns)
    {
        if (inSelectionMode)
        {
            // move just position
            return new SingleCursorState(
                SelectionStart,
                SelectionStartKind,
                SelectionStartLeftoverVisibleColumns,
                new TextPosition(lineNumber, column),
                leftoverVisibleColumns);
        }

        // move everything
        return new SingleCursorState(
            new TextRange(lineNumber, column, lineNumber, column),
            SelectionStartKind.Simple,
            leftoverVisibleColumns,
            new TextPosition(lineNumber, column),
            leftoverVisibleColumns);
    }

    private static TextSelection ComputeSelection(TextRange selectionStart, TextPosition position)
    {
        if (selectionStart.IsEmpty || !position.IsBeforeOrEqual(selectionStart.Start))
        {
            return new TextSelection(selectionStart.Start, position);
        }

        return new TextSelection(selectionStart.End, position);
    }
}

/// <summary>A cursor state on both the model and the view model.</summary>
internal sealed class CursorState : IEquatable<CursorState>
{
    public CursorState(SingleCursorState modelState, SingleCursorState viewState)
    {
        ModelState = modelState;
        ViewState = viewState;
    }

    public SingleCursorState ModelState { get; }

    public SingleCursorState ViewState { get; }

    public static PartialCursorState FromModelState(SingleCursorState modelState) => PartialCursorState.FromModel(modelState);

    public static PartialCursorState FromViewState(SingleCursorState viewState) => PartialCursorState.FromView(viewState);

    public static PartialCursorState FromModelSelection(TextSelection modelSelection)
    {
        var modelState = new SingleCursorState(
            new TextRange(modelSelection.SelectionStart),
            SelectionStartKind.Simple,
            0,
            modelSelection.Position,
            0);
        return FromModelState(modelState);
    }

    public static List<PartialCursorState> FromModelSelections(IReadOnlyList<TextSelection> modelSelections)
    {
        var states = new List<PartialCursorState>(modelSelections.Count);
        for (int i = 0, len = modelSelections.Count; i < len; i++)
        {
            states.Add(FromModelSelection(modelSelections[i]));
        }

        return states;
    }

    public bool Equals(CursorState? other) =>
        other is not null && ViewState.Equals(other.ViewState) && ModelState.Equals(other.ModelState);

    public override bool Equals(object? obj) => Equals(obj as CursorState);

    public override int GetHashCode() => HashCode.Combine(ModelState, ViewState);
}

/// <summary>
/// A cursor state that has the model state, the view state or both (upstream's
/// <c>CursorState | PartialModelCursorState | PartialViewCursorState</c>); the controller computes the missing one.
/// </summary>
internal sealed class PartialCursorState
{
    private PartialCursorState(SingleCursorState? modelState, SingleCursorState? viewState)
    {
        ModelState = modelState;
        ViewState = viewState;
    }

    public SingleCursorState? ModelState { get; }

    public SingleCursorState? ViewState { get; }

    public static PartialCursorState FromModel(SingleCursorState modelState) => new(modelState, null);

    public static PartialCursorState FromView(SingleCursorState viewState) => new(null, viewState);

    public static PartialCursorState From(CursorState state) => new(state.ModelState, state.ViewState);
}

/// <summary>The commands an edit operation (typing, deleting, ...) produces, and where to put undo stops.</summary>
internal sealed class EditOperationResult
{
    public EditOperationResult(
        EditOperationType type,
        IReadOnlyList<ICommand?> commands,
        bool shouldPushStackElementBefore,
        bool shouldPushStackElementAfter,
        Func<IReadOnlyList<TextSelection>, EditOperationResult?>? followUp = null)
    {
        Type = type;
        Commands = commands;
        ShouldPushStackElementBefore = shouldPushStackElementBefore;
        ShouldPushStackElementAfter = shouldPushStackElementAfter;
        FollowUp = followUp;
    }

    public EditOperationType Type { get; }

    public IReadOnlyList<ICommand?> Commands { get; }

    public bool ShouldPushStackElementBefore { get; }

    public bool ShouldPushStackElementAfter { get; }

    /// <summary>
    /// An edit computed from the selections this one results in, executed right after it; it may return null to skip.
    /// </summary>
    public Func<IReadOnlyList<TextSelection>, EditOperationResult?>? FollowUp { get; }
}

internal static class CursorCommon
{
    public static bool IsQuote(char ch) => ch is '\'' or '"' or '`';
}
