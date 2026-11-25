// Stand-in for vscode's test code editor (src/vs/editor/test/browser/testCodeEditor.ts, commit b57d5c9e, MIT license)
// for the cursor operation tests. There is no view model or cursor controller yet, so this holds a text model, the
// cursor states and the editor options, and runs the operations the way upstream's commands do (coreCommands.ts,
// cursorMoveCommands.ts, contrib/wordOperations/browser/wordOperations.ts): model coordinates are the view
// coordinates (no wrapping, folding or injected text), commands go through CommandExecutor, and cursor states are
// kept between moves so that the leftover visible columns of up and down moves survive as they do upstream.
// Typing, pasting, composing, Tab and the controller's bookkeeping (undo stops, auto-closed characters, cursor markers,
// recovering the cursors after an undo) are in OperationsTestEditor.Typing.cs.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Tests;

internal sealed partial class OperationsTestEditor : IDisposable
{
    private OperationsTestEditor(TextModel model, EditorOptions options, LanguageConfigurationService languageConfigurationService, bool ownsService)
    {
        Model = model;
        Options = options;
        LanguageConfigurationService = languageConfigurationService;
        OwnsService = ownsService;
        Cursors = [CreateState(new TextSelection(1, 1, 1, 1))];
        OnCreated();
    }

    public TextModel Model { get; }

    public EditorOptions Options { get; }

    public LanguageConfigurationService LanguageConfigurationService { get; }

    private bool OwnsService { get; }

    /// <summary>The cursor states (view states equal model states here); the first one is the primary cursor.</summary>
    public List<SingleCursorState> Cursors { get; private set; }

    /// <summary>The last column selection, as upstream's cursor keeps it (setCursorColumnSelectData).</summary>
    public ColumnSelectData? ColumnSelectData { get; private set; }

    /// <summary>Upstream's prevEditOperationType of the view model.</summary>
    public EditOperationType PrevEditOperationType { get; set; } = EditOperationType.Other;

    public CursorConfiguration Config =>
        new(Model.GetLanguageId(), Model.GetOptions(), Options, LanguageConfigurationService, InputMode);

    public static OperationsTestEditor Create(
        string text,
        EditorOptions? options = null,
        string? languageId = null,
        LanguageConfigurationService? languageConfigurationService = null,
        TextModelCreationOptions? modelOptions = null)
    {
        var model = TestTextModel.Create(text, languageId, modelOptions);
        return new OperationsTestEditor(
            model,
            options ?? EditorOptions.Default,
            languageConfigurationService ?? new LanguageConfigurationService(),
            languageConfigurationService is null);
    }

    public static OperationsTestEditor Create(string[] lines, EditorOptions? options = null) =>
        Create(string.Join('\n', lines), options);

    public void Dispose()
    {
        OnDisposing();
        Model.Dispose();
        if (OwnsService)
        {
            LanguageConfigurationService.Dispose();
        }
    }

    public IReadOnlyList<TextSelection> GetSelections() => Cursors.Select(c => c.Selection).ToList();

    public TextSelection GetSelection() => Cursors[0].Selection;

    public TextPosition GetPosition() => Cursors[0].Position;

    public string GetValue() => Model.GetValue();

    public void SetPosition(TextPosition position)
    {
        var validated = Model.ValidatePosition(position);
        SetSelections([new TextSelection(validated)]);
    }

    public void SetSelection(TextSelection selection) => SetSelections([selection]);

    public void SetSelections(IReadOnlyList<TextSelection> selections)
    {
        Cursors = selections.Select(CreateState).ToList();
        ColumnSelectData = null;
        OnCursorsChanged();
    }

    public void SetCursorStates(IEnumerable<SingleCursorState> states)
    {
        Cursors = states.ToList();
        ColumnSelectData = null;
        OnCursorsChanged();
    }

    /// <summary>
    /// Upstream's CoreNavigationCommands.MoveTo / MoveToSelect (CursorMoveCommands.moveTo): an undo stop, then the
    /// primary cursor moves to a validated position.
    /// </summary>
    public void MoveTo(int lineNumber, int column, bool inSelectionMode = false)
    {
        Model.PushStackElement();
        var position = Model.ValidatePosition(new TextPosition(lineNumber, column));
        SetCursorStates([Cursors[0].Move(inSelectionMode, position.LineNumber, position.Column, 0)]);
    }

    /// <summary>A core navigation command: an undo stop, then <paramref name="move"/> applied to every cursor.</summary>
    public void Move(Func<CursorConfiguration, ICursorSimpleModel, SingleCursorState, SingleCursorState> move)
    {
        Model.PushStackElement();
        var config = Config;
        SetCursorStates(Cursors.Select(c => move(config, Model, c)).ToList());
    }

    /// <summary>
    /// Upstream's editor.executeCommands (cursor.ts executeCommands): one command per cursor, run as an edit of type
    /// Other; the cursors go where the commands put them.
    /// </summary>
    public void ExecuteCommands(IReadOnlyList<ICommand?> commands) =>
        ExecuteEdit(() => ExecuteEditOperation(new EditOperationResult(EditOperationType.Other, commands, false, false)));

    /// <summary>Upstream's cursor.getCursorColumnSelectData.</summary>
    public ColumnSelectData GetCursorColumnSelectData()
    {
        if (ColumnSelectData is { } data)
        {
            return data;
        }
        var config = Config;
        var primary = Cursors[0];
        var viewSelectionStart = primary.SelectionStart.Start;
        var viewPosition = primary.Position;
        return new ColumnSelectData(
            IsReal: false,
            FromViewLineNumber: viewSelectionStart.LineNumber,
            FromViewVisualColumn: config.VisibleColumnFromColumn(Model, viewSelectionStart),
            ToViewLineNumber: viewPosition.LineNumber,
            ToViewVisualColumn: config.VisibleColumnFromColumn(Model, viewPosition));
    }

    /// <summary>Upstream's ColumnSelectCommand.runCoreEditorCommand: sets the cursors and remembers the column selection.</summary>
    public void ApplyColumnSelect(ColumnSelectResult result)
    {
        Model.PushStackElement();
        Cursors = result.ViewStates.ToList();
        ColumnSelectData = new ColumnSelectData(
            IsReal: true,
            FromViewLineNumber: result.FromLineNumber,
            FromViewVisualColumn: result.FromVisualColumn,
            ToViewLineNumber: result.ToLineNumber,
            ToViewVisualColumn: result.ToVisualColumn);
        OnCursorsChanged();
    }

    /// <summary>Upstream's CoreEditingCommands.DeleteLeft.</summary>
    public void DeleteLeft(IReadOnlyList<TextRange>? autoClosedCharacters = null)
    {
        var (shouldPushStackElementBefore, commands) = DeleteOperations.DeleteLeft(PrevEditOperationType, Config, Model, GetSelections(), autoClosedCharacters ?? GetAutoClosedCharacters());
        if (shouldPushStackElementBefore)
        {
            Model.PushStackElement();
        }
        ExecuteCommands(commands);
        PrevEditOperationType = EditOperationType.DeletingLeft;
    }

    /// <summary>Upstream's CoreEditingCommands.DeleteRight.</summary>
    public void DeleteRight()
    {
        var (shouldPushStackElementBefore, commands) = DeleteOperations.DeleteRight(PrevEditOperationType, Config, Model, GetSelections());
        if (shouldPushStackElementBefore)
        {
            Model.PushStackElement();
        }
        ExecuteCommands(commands);
        PrevEditOperationType = EditOperationType.DeletingRight;
    }

    /// <summary>Upstream's viewModel.cut (cursor.ts _executeEditOperation with DeleteOperations.cut).</summary>
    public void Cut()
    {
        var result = DeleteOperations.Cut(Config, Model, GetSelections());
        if (result.ShouldPushStackElementBefore)
        {
            Model.PushStackElement();
        }
        ExecuteCommands(result.Commands);
        if (result.ShouldPushStackElementAfter)
        {
            Model.PushStackElement();
        }
    }

    public void AssertCursor(params TextSelection[] expected) =>
        GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    public void AssertCursor(TextPosition expected) =>
        AssertCursor(new TextSelection(expected.LineNumber, expected.Column, expected.LineNumber, expected.Column));

    private static SingleCursorState CreateState(TextSelection selection) =>
        new(new TextRange(selection.SelectionStart), SelectionStartKind.Simple, 0, selection.Position, 0);
}
