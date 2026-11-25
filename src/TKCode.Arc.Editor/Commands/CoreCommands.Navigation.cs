// Ported from vscode (src/vs/editor/browser/coreCommands.ts: namespace CoreNavigationCommands, commit b57d5c9e), MIT
// license.
// This part: the cursor commands (move to, column select, cursor move, home / end / top / bottom, create cursor, word
// and line select, cancel selection, remove secondary cursors, select all, set selection). The scroll and reveal
// commands are in CoreCommands.Scroll.cs, the base classes and argument types in CoreCommands.cs, and the ids,
// preconditions and keybindings in CoreCommands.Keybindings.cs (the registration layer is not ported; see
// CoreCommands.cs).
// Differences from upstream:
//  - each command is a static readonly field holding an immutable command object (upstream registers the object and
//    exports it); the anonymous classes upstream registers are named after their field (ColumnSelectImpl,
//    CreateCursorImpl, ...);
//  - the classes that take `inSelectionMode` or `isPaged` in their options take them as constructor parameters;
//  - CursorMove's arguments are CursorMoveArguments (upstream's BaseCommandOptions & CursorMove_.RawArguments), turned
//    into a CursorMove.RawArguments for CursorMove.Parse; TextEditorSelectionSource.PROGRAMMATIC is the string
//    "api" (its value upstream);
//  - SelectAll's id is the one of the command it implements (editor.action.selectAll, SelectAllCommand in
//    editorExtensions.ts); its native text input fallback (EditorOrNativeTextInputCommand) is left out;
//  - RemoveSecondaryCursors does not announce "Removed secondary cursors" (aria status is the host's).

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;
using CursorMove_ = TKCode.Arc.Editor.Cursor.CursorMove;

namespace TKCode.Arc.Editor.Commands;

/// <summary>The editor's navigation commands: cursor moves, selections and scrolling.</summary>
internal static partial class CoreNavigationCommands
{
    // TextEditorSelectionSource.PROGRAMMATIC (platform/editor/common/editor.ts).
    private const string ProgrammaticSource = "api";

    // Constants.PAGE_SIZE_MARKER: -1 is a marker for page size.
    private const int PageSizeMarker = -1;

    private sealed class BaseMoveToCommand : CoreEditorCommand<MoveCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public BaseMoveToCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            viewModel.Model.PushStackElement();
            var cursorStateChanged = viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    CursorMoveCommands.MoveTo(viewModel, viewModel.GetPrimaryCursorState(), _inSelectionMode, position, args.ViewPosition),
                ]);
            if (cursorStateChanged && args.RevealType != NavigationCommandRevealType.None)
            {
                viewModel.RevealAllCursors(args.Source, true, true);
            }
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> MoveTo = new BaseMoveToCommand("_moveTo", inSelectionMode: false);

    public static readonly CoreEditorCommand<MoveCommandOptions> MoveToSelect = new BaseMoveToCommand("_moveToSelect", inSelectionMode: true);

    private abstract class ColumnSelectCommand<T> : CoreEditorCommand<T>
        where T : BaseCommandOptions, new()
    {
        protected ColumnSelectCommand(string id)
            : base(id)
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, T args)
        {
            viewModel.Model.PushStackElement();
            var result = GetColumnSelectResult(viewModel, viewModel.GetPrimaryCursorState(), viewModel.GetCursorColumnSelectData(), args);
            if (result is null)
            {
                // invalid arguments
                return;
            }
            var states = new PartialCursorState[result.ViewStates.Count];
            for (var i = 0; i < states.Length; i++)
            {
                states[i] = CursorState.FromViewState(result.ViewStates[i]);
            }
            viewModel.SetCursorStates(args.Source, CursorChangeReason.Explicit, states);
            viewModel.SetCursorColumnSelectData(new ColumnSelectData(
                IsReal: true,
                FromViewLineNumber: result.FromLineNumber,
                FromViewVisualColumn: result.FromVisualColumn,
                ToViewLineNumber: result.ToLineNumber,
                ToViewVisualColumn: result.ToVisualColumn));
            if (result.Reversed)
            {
                viewModel.RevealTopMostCursor(args.Source);
            }
            else
            {
                viewModel.RevealBottomMostCursor(args.Source);
            }
        }

        protected abstract ColumnSelectResult? GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, T args);
    }

    private sealed class ColumnSelectImpl : ColumnSelectCommand<ColumnSelectCommandOptions>
    {
        public ColumnSelectImpl()
            : base("columnSelect")
        {
        }

        protected override ColumnSelectResult? GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, ColumnSelectCommandOptions args)
        {
            if (args.Position is not { } position || args.ViewPosition is not { } viewPosition || args.MouseColumn is not { } mouseColumn)
            {
                return null;
            }
            // validate `args`
            var validatedPosition = viewModel.Model.ValidatePosition(position);
            var validatedViewPosition = viewModel.CoordinatesConverter.ValidateViewPosition(new TextPosition(viewPosition.LineNumber, viewPosition.Column), validatedPosition);

            var fromViewLineNumber = args.DoColumnSelect ? prevColumnSelectData.FromViewLineNumber : validatedViewPosition.LineNumber;
            var fromViewVisualColumn = args.DoColumnSelect ? prevColumnSelectData.FromViewVisualColumn : mouseColumn - 1;
            return ColumnSelection.ColumnSelect(viewModel.CursorConfig, viewModel, fromViewLineNumber, fromViewVisualColumn, validatedViewPosition.LineNumber, mouseColumn - 1);
        }
    }

    public static readonly CoreEditorCommand<ColumnSelectCommandOptions> ColumnSelect = new ColumnSelectImpl();

    private sealed class CursorColumnSelectLeftImpl : ColumnSelectCommand<BaseCommandOptions>
    {
        public CursorColumnSelectLeftImpl()
            : base("cursorColumnSelectLeft")
        {
        }

        protected override ColumnSelectResult GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, BaseCommandOptions args) =>
            ColumnSelection.ColumnSelectLeft(viewModel.CursorConfig, viewModel, prevColumnSelectData);
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectLeft = new CursorColumnSelectLeftImpl();

    private sealed class CursorColumnSelectRightImpl : ColumnSelectCommand<BaseCommandOptions>
    {
        public CursorColumnSelectRightImpl()
            : base("cursorColumnSelectRight")
        {
        }

        protected override ColumnSelectResult GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, BaseCommandOptions args) =>
            ColumnSelection.ColumnSelectRight(viewModel.CursorConfig, viewModel, prevColumnSelectData);
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectRight = new CursorColumnSelectRightImpl();

    private sealed class ColumnSelectUpCommand : ColumnSelectCommand<BaseCommandOptions>
    {
        private readonly bool _isPaged;

        public ColumnSelectUpCommand(string id, bool isPaged)
            : base(id)
        {
            _isPaged = isPaged;
        }

        protected override ColumnSelectResult GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, BaseCommandOptions args) =>
            ColumnSelection.ColumnSelectUp(viewModel.CursorConfig, viewModel, prevColumnSelectData, _isPaged);
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectUp = new ColumnSelectUpCommand("cursorColumnSelectUp", isPaged: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectPageUp = new ColumnSelectUpCommand("cursorColumnSelectPageUp", isPaged: true);

    private sealed class ColumnSelectDownCommand : ColumnSelectCommand<BaseCommandOptions>
    {
        private readonly bool _isPaged;

        public ColumnSelectDownCommand(string id, bool isPaged)
            : base(id)
        {
            _isPaged = isPaged;
        }

        protected override ColumnSelectResult GetColumnSelectResult(IViewModel viewModel, CursorState primary, ColumnSelectData prevColumnSelectData, BaseCommandOptions args) =>
            ColumnSelection.ColumnSelectDown(viewModel.CursorConfig, viewModel, prevColumnSelectData, _isPaged);
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectDown = new ColumnSelectDownCommand("cursorColumnSelectDown", isPaged: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorColumnSelectPageDown = new ColumnSelectDownCommand("cursorColumnSelectPageDown", isPaged: true);

    /// <summary>The <c>cursorMove</c> command: moves the cursors to a logical position in the view.</summary>
    internal sealed class CursorMoveImpl : CoreEditorCommand<CursorMoveArguments>
    {
        public CursorMoveImpl()
            : base("cursorMove")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, CursorMoveArguments args)
        {
            var parsed = CursorMove_.Parse(new CursorMove_.RawArguments
            {
                To = args.To,
                Select = args.Select,
                By = args.By,
                Value = args.Value,
                NoHistory = args.NoHistory,
            });
            if (parsed is null)
            {
                // illegal arguments
                return;
            }
            RunCursorMove(viewModel, args.Source, parsed);
        }

        private static void RunCursorMove(IViewModel viewModel, string? source, CursorMove_.ParsedArguments args)
        {
            // If noHistory is true, use PROGRAMMATIC source to prevent adding to navigation history
            var effectiveSource = args.NoHistory ? ProgrammaticSource : source;

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                effectiveSource,
                CursorChangeReason.Explicit,
                Move(viewModel, viewModel.GetCursorStates(), args));
            viewModel.RevealAllCursors(effectiveSource, true);
        }

        private static PartialCursorState[]? Move(IViewModel viewModel, IReadOnlyList<CursorState> cursors, CursorMove_.ParsedArguments args)
        {
            var inSelectionMode = args.Select;
            var value = args.Value;

            switch (args.Direction)
            {
                case CursorMove_.Direction.Left:
                case CursorMove_.Direction.Right:
                case CursorMove_.Direction.Up:
                case CursorMove_.Direction.Down:
                case CursorMove_.Direction.PrevBlankLine:
                case CursorMove_.Direction.NextBlankLine:
                case CursorMove_.Direction.WrappedLineStart:
                case CursorMove_.Direction.WrappedLineFirstNonWhitespaceCharacter:
                case CursorMove_.Direction.WrappedLineColumnCenter:
                case CursorMove_.Direction.WrappedLineEnd:
                case CursorMove_.Direction.WrappedLineLastNonWhitespaceCharacter:
                    return CursorMoveCommands.SimpleMove(viewModel, cursors, args.Direction, inSelectionMode, value, args.Unit);

                case CursorMove_.Direction.ViewPortTop:
                case CursorMove_.Direction.ViewPortBottom:
                case CursorMove_.Direction.ViewPortCenter:
                case CursorMove_.Direction.ViewPortIfOutside:
                    return CursorMoveCommands.ViewportMove(viewModel, cursors, args.Direction, inSelectionMode, value);
                default:
                    return null;
            }
        }
    }

    public static readonly CursorMoveImpl CursorMove = new();

    private sealed class CursorMoveBasedCommand : CoreEditorCommand<CursorMoveCommandOptions>
    {
        private readonly CursorMove_.SimpleMoveArguments _staticArgs;

        public CursorMoveBasedCommand(string id, CursorMove_.SimpleMoveArguments args)
            : base(id)
        {
            _staticArgs = args;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, CursorMoveCommandOptions dynamicArgs)
        {
            var args = _staticArgs;
            if (_staticArgs.Value == PageSizeMarker)
            {
                // -1 is a marker for page size
                args = new CursorMove_.SimpleMoveArguments(
                    _staticArgs.Direction,
                    _staticArgs.Unit,
                    _staticArgs.Select,
                    dynamicArgs.PageSize is { } pageSize and not 0 ? pageSize : viewModel.CursorConfig.PageSize);
            }

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                dynamicArgs.Source,
                CursorChangeReason.Explicit,
                CursorMoveCommands.SimpleMove(viewModel, viewModel.GetCursorStates(), args.Direction, args.Select, args.Value, args.Unit));
            viewModel.RevealAllCursors(dynamicArgs.Source, true);
        }
    }

    private static CursorMoveBasedCommand CursorMoveBased(string id, CursorMove_.Direction direction, CursorMove_.Unit unit, bool select, int value) =>
        new(id, new CursorMove_.SimpleMoveArguments(direction, unit, select, value));

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorLeft =
        CursorMoveBased("cursorLeft", CursorMove_.Direction.Left, CursorMove_.Unit.None, select: false, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorLeftSelect =
        CursorMoveBased("cursorLeftSelect", CursorMove_.Direction.Left, CursorMove_.Unit.None, select: true, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorRight =
        CursorMoveBased("cursorRight", CursorMove_.Direction.Right, CursorMove_.Unit.None, select: false, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorRightSelect =
        CursorMoveBased("cursorRightSelect", CursorMove_.Direction.Right, CursorMove_.Unit.None, select: true, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorUp =
        CursorMoveBased("cursorUp", CursorMove_.Direction.Up, CursorMove_.Unit.WrappedLine, select: false, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorUpSelect =
        CursorMoveBased("cursorUpSelect", CursorMove_.Direction.Up, CursorMove_.Unit.WrappedLine, select: true, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorPageUp =
        CursorMoveBased("cursorPageUp", CursorMove_.Direction.Up, CursorMove_.Unit.WrappedLine, select: false, value: PageSizeMarker);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorPageUpSelect =
        CursorMoveBased("cursorPageUpSelect", CursorMove_.Direction.Up, CursorMove_.Unit.WrappedLine, select: true, value: PageSizeMarker);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorDown =
        CursorMoveBased("cursorDown", CursorMove_.Direction.Down, CursorMove_.Unit.WrappedLine, select: false, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorDownSelect =
        CursorMoveBased("cursorDownSelect", CursorMove_.Direction.Down, CursorMove_.Unit.WrappedLine, select: true, value: 1);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorPageDown =
        CursorMoveBased("cursorPageDown", CursorMove_.Direction.Down, CursorMove_.Unit.WrappedLine, select: false, value: PageSizeMarker);

    public static readonly CoreEditorCommand<CursorMoveCommandOptions> CursorPageDownSelect =
        CursorMoveBased("cursorPageDownSelect", CursorMove_.Direction.Down, CursorMove_.Unit.WrappedLine, select: true, value: PageSizeMarker);

    private sealed class CreateCursorImpl : CoreEditorCommand<CreateCursorCommandOptions>
    {
        public CreateCursorImpl()
            : base("createCursor")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, CreateCursorCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            PartialCursorState newState;
            if (args.WholeLine == true)
            {
                newState = CursorMoveCommands.Line(viewModel, viewModel.GetPrimaryCursorState(), false, position, args.ViewPosition);
            }
            else
            {
                newState = CursorMoveCommands.MoveTo(viewModel, viewModel.GetPrimaryCursorState(), false, position, args.ViewPosition);
            }

            var cursorStates = viewModel.GetCursorStates();
            var states = new List<PartialCursorState>(cursorStates.Count + 1);
            foreach (var cursorState in cursorStates)
            {
                states.Add(PartialCursorState.From(cursorState));
            }

            // Check if we should remove a cursor (sort of like a toggle)
            if (states.Count > 1)
            {
                var newModelPosition = newState.ModelState?.Position;
                var newViewPosition = newState.ViewState?.Position;

                for (int i = 0, len = states.Count; i < len; i++)
                {
                    var state = cursorStates[i];

                    if (newModelPosition is { } modelPosition && !state.ModelState.Selection.Range.ContainsPosition(modelPosition))
                    {
                        continue;
                    }

                    if (newViewPosition is { } viewPositionToFind && !state.ViewState.Selection.Range.ContainsPosition(viewPositionToFind))
                    {
                        continue;
                    }

                    // => Remove the cursor
                    states.RemoveAt(i);

                    viewModel.Model.PushStackElement();
                    viewModel.SetCursorStates(
                        args.Source,
                        CursorChangeReason.Explicit,
                        states);
                    return;
                }
            }

            // => Add the new cursor
            states.Add(newState);

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                states);
        }
    }

    public static readonly CoreEditorCommand<CreateCursorCommandOptions> CreateCursor = new CreateCursorImpl();

    // states.slice(0): the cursor states as partial states, to replace one of them.
    private static PartialCursorState[] CopyStates(IReadOnlyList<CursorState> states)
    {
        var result = new PartialCursorState[states.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = PartialCursorState.From(states[i]);
        }
        return result;
    }

    private sealed class LastCursorMoveToSelectImpl : CoreEditorCommand<MoveCommandOptions>
    {
        public LastCursorMoveToSelectImpl()
            : base("_lastCursorMoveToSelect")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            var lastAddedCursorIndex = viewModel.GetLastAddedCursorIndex();

            var states = viewModel.GetCursorStates();
            var newStates = CopyStates(states);
            newStates[lastAddedCursorIndex] = CursorMoveCommands.MoveTo(viewModel, states[lastAddedCursorIndex], true, position, args.ViewPosition);

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                newStates);
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> LastCursorMoveToSelect = new LastCursorMoveToSelectImpl();

    private sealed class HomeCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public HomeCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                CursorMoveCommands.MoveToBeginningOfLine(viewModel, viewModel.GetCursorStates(), _inSelectionMode));
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorHome = new HomeCommand("cursorHome", inSelectionMode: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorHomeSelect = new HomeCommand("cursorHomeSelect", inSelectionMode: true);

    private sealed class LineStartCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public LineStartCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                Exec(viewModel.GetCursorStates()));
            viewModel.RevealAllCursors(args.Source, true);
        }

        private PartialCursorState[] Exec(IReadOnlyList<CursorState> cursors)
        {
            var result = new PartialCursorState[cursors.Count];
            for (int i = 0, len = cursors.Count; i < len; i++)
            {
                var cursor = cursors[i];
                var lineNumber = cursor.ModelState.Position.LineNumber;
                result[i] = CursorState.FromModelState(cursor.ModelState.Move(_inSelectionMode, lineNumber, 1, 0));
            }
            return result;
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorLineStart = new LineStartCommand("cursorLineStart", inSelectionMode: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorLineStartSelect = new LineStartCommand("cursorLineStartSelect", inSelectionMode: true);

    private sealed class EndCommand : CoreEditorCommand<EndCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public EndCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        // The keybindings of cursorEnd and cursorEndSelect pass { "sticky": false } (CoreCommandsTable).
        public override EndCommandOptions? ConvertArgs(object? args) =>
            args is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Object } json
                ? new EndCommandOptions
                {
                    Source = json.TryGetProperty("source", out var source) && source.ValueKind == System.Text.Json.JsonValueKind.String ? source.GetString() : null,
                    Sticky = json.TryGetProperty("sticky", out var sticky) && sticky.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False ? sticky.GetBoolean() : null,
                }
                : base.ConvertArgs(args);

        public override void RunCoreEditorCommand(IViewModel viewModel, EndCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                CursorMoveCommands.MoveToEndOfLine(viewModel, viewModel.GetCursorStates(), _inSelectionMode, args.Sticky ?? false));
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<EndCommandOptions> CursorEnd = new EndCommand("cursorEnd", inSelectionMode: false);

    public static readonly CoreEditorCommand<EndCommandOptions> CursorEndSelect = new EndCommand("cursorEndSelect", inSelectionMode: true);

    private sealed class LineEndCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public LineEndCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                Exec(viewModel, viewModel.GetCursorStates()));
            viewModel.RevealAllCursors(args.Source, true);
        }

        private PartialCursorState[] Exec(IViewModel viewModel, IReadOnlyList<CursorState> cursors)
        {
            var result = new PartialCursorState[cursors.Count];
            for (int i = 0, len = cursors.Count; i < len; i++)
            {
                var cursor = cursors[i];
                var lineNumber = cursor.ModelState.Position.LineNumber;
                var maxColumn = viewModel.Model.GetLineMaxColumn(lineNumber);
                result[i] = CursorState.FromModelState(cursor.ModelState.Move(_inSelectionMode, lineNumber, maxColumn, 0));
            }
            return result;
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorLineEnd = new LineEndCommand("cursorLineEnd", inSelectionMode: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorLineEndSelect = new LineEndCommand("cursorLineEndSelect", inSelectionMode: true);

    private sealed class TopCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public TopCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                CursorMoveCommands.MoveToBeginningOfBuffer(viewModel, viewModel.GetCursorStates(), _inSelectionMode));
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorTop = new TopCommand("cursorTop", inSelectionMode: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorTopSelect = new TopCommand("cursorTopSelect", inSelectionMode: true);

    private sealed class BottomCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public BottomCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                CursorMoveCommands.MoveToEndOfBuffer(viewModel, viewModel.GetCursorStates(), _inSelectionMode));
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorBottom = new BottomCommand("cursorBottom", inSelectionMode: false);

    public static readonly CoreEditorCommand<BaseCommandOptions> CursorBottomSelect = new BottomCommand("cursorBottomSelect", inSelectionMode: true);

    private sealed class WordCommand : CoreEditorCommand<MoveCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public WordCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    CursorMoveCommands.Word(viewModel, viewModel.GetPrimaryCursorState(), _inSelectionMode, position),
                ]);
            if (args.RevealType != NavigationCommandRevealType.None)
            {
                viewModel.RevealAllCursors(args.Source, true, true);
            }
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> WordSelect = new WordCommand("_wordSelect", inSelectionMode: false);

    public static readonly CoreEditorCommand<MoveCommandOptions> WordSelectDrag = new WordCommand("_wordSelectDrag", inSelectionMode: true);

    private sealed class LastCursorWordSelectImpl : CoreEditorCommand<MoveCommandOptions>
    {
        public LastCursorWordSelectImpl()
            : base("lastCursorWordSelect")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            var lastAddedCursorIndex = viewModel.GetLastAddedCursorIndex();

            var states = viewModel.GetCursorStates();
            var newStates = CopyStates(states);
            var lastAddedState = states[lastAddedCursorIndex];
            newStates[lastAddedCursorIndex] = CursorMoveCommands.Word(viewModel, lastAddedState, lastAddedState.ModelState.HasSelection(), position);

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                newStates);
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> LastCursorWordSelect = new LastCursorWordSelectImpl();

    private sealed class LineCommand : CoreEditorCommand<MoveCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public LineCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    CursorMoveCommands.Line(viewModel, viewModel.GetPrimaryCursorState(), _inSelectionMode, position, args.ViewPosition),
                ]);
            if (args.RevealType != NavigationCommandRevealType.None)
            {
                viewModel.RevealAllCursors(args.Source, false, true);
            }
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> LineSelect = new LineCommand("_lineSelect", inSelectionMode: false);

    public static readonly CoreEditorCommand<MoveCommandOptions> LineSelectDrag = new LineCommand("_lineSelectDrag", inSelectionMode: true);

    private sealed class LastCursorLineCommand : CoreEditorCommand<MoveCommandOptions>
    {
        private readonly bool _inSelectionMode;

        public LastCursorLineCommand(string id, bool inSelectionMode)
            : base(id)
        {
            _inSelectionMode = inSelectionMode;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, MoveCommandOptions args)
        {
            if (args.Position is not { } position)
            {
                return;
            }
            var lastAddedCursorIndex = viewModel.GetLastAddedCursorIndex();

            var states = viewModel.GetCursorStates();
            var newStates = CopyStates(states);
            newStates[lastAddedCursorIndex] = CursorMoveCommands.Line(viewModel, states[lastAddedCursorIndex], _inSelectionMode, position, args.ViewPosition);

            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                newStates);
        }
    }

    public static readonly CoreEditorCommand<MoveCommandOptions> LastCursorLineSelect = new LastCursorLineCommand("lastCursorLineSelect", inSelectionMode: false);

    public static readonly CoreEditorCommand<MoveCommandOptions> LastCursorLineSelectDrag = new LastCursorLineCommand("lastCursorLineSelectDrag", inSelectionMode: true);

    private sealed class CancelSelectionImpl : CoreEditorCommand<BaseCommandOptions>
    {
        public CancelSelectionImpl()
            : base("cancelSelection")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    CursorMoveCommands.CancelSelection(viewModel, viewModel.GetPrimaryCursorState()),
                ]);
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> CancelSelection = new CancelSelectionImpl();

    private sealed class RemoveSecondaryCursorsImpl : CoreEditorCommand<BaseCommandOptions>
    {
        public RemoveSecondaryCursorsImpl()
            : base("removeSecondaryCursors")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    PartialCursorState.From(viewModel.GetPrimaryCursorState()),
                ]);
            viewModel.RevealAllCursors(args.Source, true);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> RemoveSecondaryCursors = new RemoveSecondaryCursorsImpl();

    private sealed class SelectAllImpl : CoreEditorCommand<BaseCommandOptions>
    {
        public SelectAllImpl()
            : base("editor.action.selectAll")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                "keyboard",
                CursorChangeReason.Explicit,
                [
                    CursorMoveCommands.SelectAll(viewModel, viewModel.GetPrimaryCursorState()),
                ]);
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> SelectAll = new SelectAllImpl();

    private sealed class SetSelectionImpl : CoreEditorCommand<SetSelectionCommandOptions>
    {
        public SetSelectionImpl()
            : base("setSelection")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, SetSelectionCommandOptions args)
        {
            if (args.Selection is not { } selection)
            {
                return;
            }
            viewModel.Model.PushStackElement();
            viewModel.SetCursorStates(
                args.Source,
                CursorChangeReason.Explicit,
                [
                    CursorState.FromModelSelection(selection),
                ]);
        }
    }

    public static readonly CoreEditorCommand<SetSelectionCommandOptions> SetSelection = new SetSelectionImpl();
}
