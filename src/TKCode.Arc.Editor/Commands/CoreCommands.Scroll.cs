// Ported from vscode (src/vs/editor/browser/coreCommands.ts: namespaces EditorScroll_ and RevealLine_, and from
// CoreNavigationCommands EditorScrollImpl, ScrollLineUp/Down, ScrollPageUp/Down, ScrollEditorTop/Bottom,
// ScrollLeft/Right and RevealLine, commit b57d5c9e), MIT license.
// The other core commands are in CoreCommands.Navigation.cs and CoreCommands.Editing.cs; the base classes in
// CoreCommands.cs (which says what of the registration layer is left out).
// Differences from upstream:
//  - the namespaces are static classes with upstream's names (EditorScroll_, RevealLine_); RawDirection, RawUnit and
//    RawAtArgument hold string constants; RawArguments are records with optional members (upstream's
//    Partial<RawArguments>); ParsedArguments is a record;
//  - the command metadata (description, argument schema) is left out; its constraints are IsEditorScrollArgs and
//    IsRevealLineArgs, which take the RawArguments records or a JSON-like dictionary (the form a keybinding's arguments
//    come in), as CursorMove.IsCursorMoveArgs does;
//  - EditorScrollCommandOptions and RevealLineCommandOptions are the RawArguments with the source (upstream's
//    RawArguments & BaseCommandOptions);
//  - determineScrollMethod returns a delegate (upstream returns a bound method);
//  - RevealLine's lineNumber is a number or a string as upstream (an object here): a number that is not an integer is
//    rounded down, a string is read as JavaScript's parseInt reads it, and a line number that is not a number at all
//    (parseInt's NaN) reveals nothing (upstream goes on with NaN and fails in getLineMaxColumn).

using System.Globalization;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Commands;

/// <summary>The arguments of the <c>editorScroll</c> command and their parsing.</summary>
internal static class EditorScroll_
{
    /// <summary>
    /// Whether <paramref name="arg"/> is a valid editor scroll argument object: a <see cref="RawArguments"/> with a
    /// <c>to</c>, or a dictionary whose <c>to</c> is a string and whose <c>by</c> (a string), <c>value</c> (a number)
    /// and <c>revealCursor</c> (a boolean) have the right types when present.
    /// </summary>
    public static bool IsEditorScrollArgs(object? arg)
    {
        if (arg is RawArguments rawArguments)
        {
            return rawArguments.To is not null;
        }

        if (arg is not IReadOnlyDictionary<string, object?> scrollArg)
        {
            return false;
        }

        if (!scrollArg.TryGetValue("to", out var to) || to is not string)
        {
            return false;
        }

        if (scrollArg.TryGetValue("by", out var by) && by is not null && by is not string)
        {
            return false;
        }

        if (scrollArg.TryGetValue("value", out var value) && value is not null && !CoreCommandsArgs.IsNumber(value))
        {
            return false;
        }

        if (scrollArg.TryGetValue("revealCursor", out var revealCursor) && revealCursor is not null && revealCursor is not bool)
        {
            return false;
        }

        return true;
    }

    /// <summary>Directions in the view for editor scroll command.</summary>
    public static class RawDirection
    {
        public const string Up = "up";
        public const string Right = "right";
        public const string Down = "down";
        public const string Left = "left";
    }

    /// <summary>Units for editor scroll 'by' argument.</summary>
    public static class RawUnit
    {
        public const string Line = "line";
        public const string WrappedLine = "wrappedLine";
        public const string Page = "page";
        public const string HalfPage = "halfPage";
        public const string Editor = "editor";
        public const string Column = "column";
    }

    /// <summary>Arguments for editor scroll command.</summary>
    internal record RawArguments
    {
        /// <summary>A mandatory direction value: <c>up</c> or <c>down</c> (<c>left</c> and <c>right</c> with <c>column</c>).</summary>
        public string? To { get; init; }

        /// <summary>
        /// Unit to move: <c>line</c>, <c>wrappedLine</c>, <c>page</c>, <c>halfPage</c>, <c>editor</c> or <c>column</c>.
        /// Default is <c>wrappedLine</c>.
        /// </summary>
        public string? By { get; init; }

        /// <summary>Number of units to move. Default is 1.</summary>
        public double? Value { get; init; }

        /// <summary>If true reveals the cursor if it is outside view port.</summary>
        public bool? RevealCursor { get; init; }

        /// <summary>If true the cursor that is revealed selects up to where it was.</summary>
        public bool? Select { get; init; }
    }

    /// <summary>Parses the arguments; null for illegal arguments.</summary>
    public static ParsedArguments? Parse(RawArguments args)
    {
        Direction direction;
        switch (args.To)
        {
            case RawDirection.Up:
                direction = Direction.Up;
                break;
            case RawDirection.Right:
                direction = Direction.Right;
                break;
            case RawDirection.Down:
                direction = Direction.Down;
                break;
            case RawDirection.Left:
                direction = Direction.Left;
                break;
            default:
                // Illegal arguments
                return null;
        }

        Unit unit;
        switch (args.By)
        {
            case RawUnit.Line:
                unit = Unit.Line;
                break;
            case RawUnit.WrappedLine:
                unit = Unit.WrappedLine;
                break;
            case RawUnit.Page:
                unit = Unit.Page;
                break;
            case RawUnit.HalfPage:
                unit = Unit.HalfPage;
                break;
            case RawUnit.Editor:
                unit = Unit.Editor;
                break;
            case RawUnit.Column:
                unit = Unit.Column;
                break;
            default:
                unit = Unit.WrappedLine;
                break;
        }

        // Math.floor(args.value || 1)
        var rawValue = args.Value is { } v && v != 0 && !double.IsNaN(v) ? v : 1;
        var value = (int)Math.Floor(rawValue);
        var revealCursor = args.RevealCursor ?? false;

        return new ParsedArguments(direction, unit, value, revealCursor, args.Select ?? false);
    }

    /// <summary>The parsed arguments of the editor scroll command.</summary>
    internal sealed record ParsedArguments(Direction Direction, Unit Unit, int Value, bool RevealCursor, bool Select);

    internal enum Direction
    {
        Up = 1,
        Right = 2,
        Down = 3,
        Left = 4,
    }

    internal enum Unit
    {
        Line = 1,
        WrappedLine = 2,
        Page = 3,
        HalfPage = 4,
        Editor = 5,
        Column = 6,
    }
}

/// <summary>The arguments of the <c>revealLine</c> command.</summary>
internal static class RevealLine_
{
    /// <summary>
    /// Whether <paramref name="arg"/> is a valid reveal line argument object: a <see cref="RawArguments"/> with a line
    /// number, or a dictionary whose <c>lineNumber</c> is a number or a string and whose <c>at</c> is a string when
    /// present.
    /// </summary>
    public static bool IsRevealLineArgs(object? arg)
    {
        if (arg is RawArguments rawArguments)
        {
            return rawArguments.LineNumber is string || (rawArguments.LineNumber is { } n && CoreCommandsArgs.IsNumber(n));
        }

        if (arg is not IReadOnlyDictionary<string, object?> revealLineArg)
        {
            return false;
        }

        if (!revealLineArg.TryGetValue("lineNumber", out var lineNumber) || lineNumber is null || (lineNumber is not string && !CoreCommandsArgs.IsNumber(lineNumber)))
        {
            return false;
        }

        if (revealLineArg.TryGetValue("at", out var at) && at is not null && at is not string)
        {
            return false;
        }

        return true;
    }

    /// <summary>Arguments for reveal line command.</summary>
    internal record RawArguments
    {
        /// <summary>A mandatory line number value (0-based): a number or a string.</summary>
        public object? LineNumber { get; init; }

        /// <summary>Logical position at which line has to be revealed: <c>top</c>, <c>center</c> or <c>bottom</c>.</summary>
        public string? At { get; init; }
    }

    /// <summary>Values for reveal line 'at' argument.</summary>
    public static class RawAtArgument
    {
        public const string Top = "top";
        public const string Center = "center";
        public const string Bottom = "bottom";
    }
}

/// <summary>Helpers for the JSON-like arguments of the core commands.</summary>
internal static class CoreCommandsArgs
{
    /// <summary>Whether <paramref name="value"/> is a number (JavaScript's typeof value === 'number').</summary>
    public static bool IsNumber(object value) =>
        value is int or long or short or sbyte or byte or uint or ulong or ushort or double or float or decimal;

    /// <summary>A boxed number as a double.</summary>
    public static double ToDouble(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    /// <summary>JavaScript's <c>parseInt(text)</c>: the integer at the start of the text, or NaN.</summary>
    public static double ParseInt(string text)
    {
        var i = 0;
        while (i < text.Length && IsJsWhitespace(text[i]))
        {
            i++;
        }

        var sign = 1;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            sign = text[i] == '-' ? -1 : 1;
            i++;
        }

        var radix = 10;
        if (i + 1 < text.Length && text[i] == '0' && (text[i + 1] == 'x' || text[i + 1] == 'X'))
        {
            radix = 16;
            i += 2;
        }

        double result = 0;
        var digits = 0;
        for (; i < text.Length; i++)
        {
            var digit = DigitValue(text[i]);
            if (digit < 0 || digit >= radix)
            {
                break;
            }
            result = result * radix + digit;
            digits++;
        }

        return digits == 0 ? double.NaN : sign * result;
    }

    private static int DigitValue(char ch)
    {
        if (ch >= '0' && ch <= '9')
        {
            return ch - '0';
        }
        if (ch >= 'a' && ch <= 'z')
        {
            return ch - 'a' + 10;
        }
        if (ch >= 'A' && ch <= 'Z')
        {
            return ch - 'A' + 10;
        }
        return -1;
    }

    // The white space and line terminators String.prototype.trim removes.
    private static bool IsJsWhitespace(char ch) =>
        ch is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' or (char)0x00A0 or (char)0x1680 or (char)0x2028 or (char)0x2029 or (char)0x202F or (char)0x205F or (char)0x3000 or (char)0xFEFF
        || (ch >= (char)0x2000 && ch <= (char)0x200A);
}

internal static partial class CoreNavigationCommands
{
    /// <summary>The arguments of the <c>editorScroll</c> command (upstream's EditorScroll_.RawArguments &amp; BaseCommandOptions).</summary>
    internal sealed record EditorScrollCommandOptions : EditorScroll_.RawArguments
    {
        /// <summary>What started the command.</summary>
        public string? Source { get; init; }
    }

    /// <summary>The <c>editorScroll</c> command: scrolls the editor, and moves the cursor into the new view port if asked.</summary>
    internal sealed class EditorScrollImpl : CoreEditorCommand<EditorScrollCommandOptions>
    {
        public EditorScrollImpl()
            : base("editorScroll")
        {
        }

        public Action<IViewModel, string?, EditorScroll_.ParsedArguments>? DetermineScrollMethod(EditorScroll_.ParsedArguments args)
        {
            var isHorizontalUnit = args.Unit == EditorScroll_.Unit.Column;
            var isVerticalUnit = args.Unit is EditorScroll_.Unit.Line
                or EditorScroll_.Unit.WrappedLine
                or EditorScroll_.Unit.Page
                or EditorScroll_.Unit.HalfPage
                or EditorScroll_.Unit.Editor;
            var isHorizontalDirection = args.Direction is EditorScroll_.Direction.Left or EditorScroll_.Direction.Right;
            var isVerticalDirection = args.Direction is EditorScroll_.Direction.Up or EditorScroll_.Direction.Down;

            if (isHorizontalUnit && isHorizontalDirection)
            {
                return RunHorizontalEditorScroll;
            }
            if (isVerticalUnit && isVerticalDirection)
            {
                return RunVerticalEditorScroll;
            }
            return null;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, EditorScrollCommandOptions args)
        {
            var parsed = EditorScroll_.Parse(args);
            if (parsed is null)
            {
                // illegal arguments
                return;
            }
            var runEditorScroll = DetermineScrollMethod(parsed);
            if (runEditorScroll is null)
            {
                // Incompatible unit and direction
                return;
            }
            runEditorScroll(viewModel, args.Source, parsed);
        }

        public void RunVerticalEditorScroll(IViewModel viewModel, string? source, EditorScroll_.ParsedArguments args)
        {
            var desiredScrollTop = ComputeDesiredScrollTop(viewModel, args);

            if (args.RevealCursor)
            {
                // must ensure cursor is in new visible range
                var desiredVisibleViewRange = viewModel.GetCompletelyVisibleViewRangeAtScrollTop(desiredScrollTop);
                var paddedRange = viewModel.GetViewRangeWithCursorPadding(desiredVisibleViewRange);

                viewModel.SetCursorStates(
                    source,
                    CursorChangeReason.Explicit,
                    [
                        CursorMoveCommands.FindPositionInViewportIfOutside(viewModel, viewModel.GetPrimaryCursorState(), paddedRange, args.Select),
                    ]);
            }

            viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: desiredScrollTop), ScrollType.Smooth);
        }

        private static double ComputeDesiredScrollTop(IViewModel viewModel, EditorScroll_.ParsedArguments args)
        {
            if (args.Unit == EditorScroll_.Unit.Line)
            {
                // scrolling by model lines
                var futureViewport = viewModel.ViewLayout.GetFutureViewport();
                var visibleViewRange = viewModel.GetCompletelyVisibleViewRangeAtScrollTop(futureViewport.Top);
                var visibleModelRange = viewModel.CoordinatesConverter.ConvertViewRangeToModelRange(visibleViewRange);

                int desiredTopModelLineNumber;
                if (args.Direction == EditorScroll_.Direction.Up)
                {
                    // must go x model lines up
                    desiredTopModelLineNumber = Math.Max(1, visibleModelRange.StartLineNumber - args.Value);
                }
                else
                {
                    // must go x model lines down
                    desiredTopModelLineNumber = Math.Min(viewModel.Model.GetLineCount(), visibleModelRange.StartLineNumber + args.Value);
                }

                var viewPosition = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(desiredTopModelLineNumber, 1));
                return viewModel.ViewLayout.GetVerticalOffsetForLineNumber(viewPosition.LineNumber);
            }

            if (args.Unit == EditorScroll_.Unit.Editor)
            {
                var desiredTopModelLineNumber = 0;
                if (args.Direction == EditorScroll_.Direction.Down)
                {
                    desiredTopModelLineNumber = viewModel.Model.GetLineCount() - viewModel.CursorConfig.PageSize;
                }
                return viewModel.ViewLayout.GetVerticalOffsetForLineNumber(desiredTopModelLineNumber);
            }

            int noOfLines;
            if (args.Unit == EditorScroll_.Unit.Page)
            {
                noOfLines = viewModel.CursorConfig.PageSize * args.Value;
            }
            else if (args.Unit == EditorScroll_.Unit.HalfPage)
            {
                // Math.round of a non-negative half
                noOfLines = (viewModel.CursorConfig.PageSize + 1) / 2 * args.Value;
            }
            else
            {
                noOfLines = args.Value;
            }
            var deltaLines = (args.Direction == EditorScroll_.Direction.Up ? -1 : 1) * noOfLines;
            return viewModel.ViewLayout.GetCurrentScrollTop() + deltaLines * viewModel.CursorConfig.LineHeight;
        }

        public void RunHorizontalEditorScroll(IViewModel viewModel, string? source, EditorScroll_.ParsedArguments args)
        {
            var desiredScrollLeft = ComputeDesiredScrollLeft(viewModel, args);
            viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollLeft: desiredScrollLeft), ScrollType.Smooth);
        }

        public double ComputeDesiredScrollLeft(IViewModel viewModel, EditorScroll_.ParsedArguments args)
        {
            var deltaColumns = (args.Direction == EditorScroll_.Direction.Left ? -1 : 1) * args.Value;
            return viewModel.ViewLayout.GetCurrentScrollLeft() + deltaColumns * viewModel.CursorConfig.TypicalHalfwidthCharacterWidth;
        }
    }

    public static readonly EditorScrollImpl EditorScroll = new();

    // The commands that run editorScroll with fixed arguments (ScrollLineUp, ScrollPageDown, ...).
    private sealed class FixedEditorScrollCommand : CoreEditorCommand<BaseCommandOptions>
    {
        private readonly string _to;
        private readonly string _by;
        private readonly int _value;

        public FixedEditorScrollCommand(string id, string to, string by, int value)
            : base(id)
        {
            _to = to;
            _by = by;
            _value = value;
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, BaseCommandOptions args)
        {
            EditorScroll.RunCoreEditorCommand(viewModel, new EditorScrollCommandOptions
            {
                To = _to,
                By = _by,
                Value = _value,
                RevealCursor = false,
                Select = false,
                Source = args.Source,
            });
        }
    }

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollLineUp =
        new FixedEditorScrollCommand("scrollLineUp", EditorScroll_.RawDirection.Up, EditorScroll_.RawUnit.WrappedLine, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollPageUp =
        new FixedEditorScrollCommand("scrollPageUp", EditorScroll_.RawDirection.Up, EditorScroll_.RawUnit.Page, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollEditorTop =
        new FixedEditorScrollCommand("scrollEditorTop", EditorScroll_.RawDirection.Up, EditorScroll_.RawUnit.Editor, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollLineDown =
        new FixedEditorScrollCommand("scrollLineDown", EditorScroll_.RawDirection.Down, EditorScroll_.RawUnit.WrappedLine, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollPageDown =
        new FixedEditorScrollCommand("scrollPageDown", EditorScroll_.RawDirection.Down, EditorScroll_.RawUnit.Page, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollEditorBottom =
        new FixedEditorScrollCommand("scrollEditorBottom", EditorScroll_.RawDirection.Down, EditorScroll_.RawUnit.Editor, 1);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollLeft =
        new FixedEditorScrollCommand("scrollLeft", EditorScroll_.RawDirection.Left, EditorScroll_.RawUnit.Column, 2);

    public static readonly CoreEditorCommand<BaseCommandOptions> ScrollRight =
        new FixedEditorScrollCommand("scrollRight", EditorScroll_.RawDirection.Right, EditorScroll_.RawUnit.Column, 2);

    /// <summary>The arguments of the <c>revealLine</c> command (upstream's RevealLine_.RawArguments &amp; BaseCommandOptions).</summary>
    internal sealed record RevealLineCommandOptions : RevealLine_.RawArguments
    {
        /// <summary>What started the command.</summary>
        public string? Source { get; init; }
    }

    private sealed class RevealLineImpl : CoreEditorCommand<RevealLineCommandOptions>
    {
        public RevealLineImpl()
            : base("revealLine")
        {
        }

        public override void RunCoreEditorCommand(IViewModel viewModel, RevealLineCommandOptions args)
        {
            var revealLineArg = args;
            // revealLineArg.lineNumber || 0, then + 1
            double lineNumberValue = revealLineArg.LineNumber switch
            {
                string text => text.Length == 0 ? 0 : CoreCommandsArgs.ParseInt(text),
                { } number when CoreCommandsArgs.IsNumber(number) => CoreCommandsArgs.ToDouble(number) is var value && !double.IsNaN(value) ? value : 0,
                _ => 0,
            };
            if (double.IsNaN(lineNumberValue))
            {
                return;
            }
            var lineNumberDouble = Math.Floor(lineNumberValue) + 1;
            if (lineNumberDouble < 1)
            {
                lineNumberDouble = 1;
            }
            var lineCount = viewModel.Model.GetLineCount();
            if (lineNumberDouble > lineCount)
            {
                lineNumberDouble = lineCount;
            }
            var lineNumber = (int)lineNumberDouble;

            var range = new TextRange(
                lineNumber, 1,
                lineNumber, viewModel.Model.GetLineMaxColumn(lineNumber));

            var revealAt = VerticalRevealType.Simple;
            if (!string.IsNullOrEmpty(revealLineArg.At))
            {
                switch (revealLineArg.At)
                {
                    case RevealLine_.RawAtArgument.Top:
                        revealAt = VerticalRevealType.Top;
                        break;
                    case RevealLine_.RawAtArgument.Center:
                        revealAt = VerticalRevealType.Center;
                        break;
                    case RevealLine_.RawAtArgument.Bottom:
                        revealAt = VerticalRevealType.Bottom;
                        break;
                    default:
                        break;
                }
            }

            var viewRange = viewModel.CoordinatesConverter.ConvertModelRangeToViewRange(range);

            viewModel.RevealRange(args.Source, false, viewRange, revealAt, ScrollType.Smooth);
        }
    }

    public static readonly CoreEditorCommand<RevealLineCommandOptions> RevealLine = new RevealLineImpl();
}
