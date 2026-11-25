// Ported from vscode (src/vs/editor/common/cursor/cursorMoveCommands.ts: namespace CursorMove, commit b57d5c9e),
// MIT license.
// Differences from upstream:
//  - the namespace is a static class; RawDirection and RawUnit hold string constants;
//  - RawArguments has optional properties (upstream's Partial<RawArguments>) and Value is an int;
//  - ParsedArguments and SimpleMoveArguments are records; the SimpleMoveDirection and ViewportDirection type unions are
//    the Direction enum (CursorMoveCommands.SimpleMove and ViewportMove return null for the directions they do not
//    take);
//  - metadata (ICommandMetadata: description, argument description and JSON schema) is description only: the
//    descriptions are the XML docs of CursorMove and RawArguments; its constraint is IsCursorMoveArgs, which takes a
//    RawArguments or a JSON-like dictionary (the form a keybinding's arguments come in).

namespace TKCode.Arc.Editor.Cursor;

/// <summary>
/// Move cursor to a logical position in the view: the arguments of the <c>cursorMove</c> command (see
/// <see cref="RawArguments"/>) and their parsing.
/// </summary>
internal static class CursorMove
{
    /// <summary>
    /// Whether <paramref name="arg"/> is a valid cursor move argument object: a <see cref="RawArguments"/> with a
    /// <c>to</c>, or a dictionary whose <c>to</c> is a string and whose <c>select</c> and <c>noHistory</c> (booleans),
    /// <c>by</c> (a string) and <c>value</c> (a number) have the right types when present.
    /// </summary>
    public static bool IsCursorMoveArgs(object? arg)
    {
        if (arg is RawArguments rawArguments)
        {
            return rawArguments.To is not null;
        }

        if (arg is not IReadOnlyDictionary<string, object?> cursorMoveArg)
        {
            return false;
        }

        if (!cursorMoveArg.TryGetValue("to", out var to) || to is not string)
        {
            return false;
        }

        if (cursorMoveArg.TryGetValue("select", out var select) && select is not null && select is not bool)
        {
            return false;
        }

        if (cursorMoveArg.TryGetValue("by", out var by) && by is not null && by is not string)
        {
            return false;
        }

        if (cursorMoveArg.TryGetValue("value", out var value) && value is not null && !IsNumber(value))
        {
            return false;
        }

        if (cursorMoveArg.TryGetValue("noHistory", out var noHistory) && noHistory is not null && noHistory is not bool)
        {
            return false;
        }

        return true;
    }

    private static bool IsNumber(object value) =>
        value is int or long or short or sbyte or byte or uint or ulong or ushort or double or float or decimal;

    /// <summary>Positions in the view for cursor move command.</summary>
    public static class RawDirection
    {
        public const string Left = "left";
        public const string Right = "right";
        public const string Up = "up";
        public const string Down = "down";

        public const string PrevBlankLine = "prevBlankLine";
        public const string NextBlankLine = "nextBlankLine";

        public const string WrappedLineStart = "wrappedLineStart";
        public const string WrappedLineFirstNonWhitespaceCharacter = "wrappedLineFirstNonWhitespaceCharacter";
        public const string WrappedLineColumnCenter = "wrappedLineColumnCenter";
        public const string WrappedLineEnd = "wrappedLineEnd";
        public const string WrappedLineLastNonWhitespaceCharacter = "wrappedLineLastNonWhitespaceCharacter";

        public const string ViewPortTop = "viewPortTop";
        public const string ViewPortCenter = "viewPortCenter";
        public const string ViewPortBottom = "viewPortBottom";

        public const string ViewPortIfOutside = "viewPortIfOutside";
    }

    /// <summary>Units for Cursor move 'by' argument.</summary>
    public static class RawUnit
    {
        public const string Line = "line";
        public const string WrappedLine = "wrappedLine";
        public const string Character = "character";
        public const string HalfLine = "halfLine";
        public const string FoldedLine = "foldedLine";
    }

    /// <summary>Arguments for Cursor move command (the cursor move argument object).</summary>
    internal sealed class RawArguments
    {
        /// <summary>
        /// A mandatory logical position value providing where to move the cursor: <c>left</c>, <c>right</c>, <c>up</c>,
        /// <c>down</c>, <c>prevBlankLine</c>, <c>nextBlankLine</c>, <c>wrappedLineStart</c>, <c>wrappedLineEnd</c>,
        /// <c>wrappedLineColumnCenter</c>, <c>wrappedLineFirstNonWhitespaceCharacter</c>,
        /// <c>wrappedLineLastNonWhitespaceCharacter</c>, <c>viewPortTop</c>, <c>viewPortCenter</c>, <c>viewPortBottom</c>
        /// or <c>viewPortIfOutside</c> (see <see cref="RawDirection"/>).
        /// </summary>
        public string? To { get; init; }

        /// <summary>If <c>true</c> makes the selection. Default is <c>false</c>.</summary>
        public bool? Select { get; init; }

        /// <summary>
        /// Unit to move. Default is computed based on <see cref="To"/> value: <c>line</c>, <c>wrappedLine</c>,
        /// <c>character</c>, <c>halfLine</c> or <c>foldedLine</c> (see <see cref="RawUnit"/>). Use <c>foldedLine</c> with
        /// <c>up</c>/<c>down</c> to move by logical lines while treating each folded region as a single step.
        /// </summary>
        public string? By { get; init; }

        /// <summary>Number of units to move. Default is <c>1</c>.</summary>
        public int? Value { get; init; }

        /// <summary>If <c>true</c> does not add the movement to navigation history. Default is <c>false</c>.</summary>
        public bool? NoHistory { get; init; }
    }

    /// <summary>Parses the arguments; null for illegal arguments.</summary>
    public static ParsedArguments? Parse(RawArguments args)
    {
        if (string.IsNullOrEmpty(args.To))
        {
            // illegal arguments
            return null;
        }

        Direction direction;
        switch (args.To)
        {
            case RawDirection.Left:
                direction = Direction.Left;
                break;
            case RawDirection.Right:
                direction = Direction.Right;
                break;
            case RawDirection.Up:
                direction = Direction.Up;
                break;
            case RawDirection.Down:
                direction = Direction.Down;
                break;
            case RawDirection.PrevBlankLine:
                direction = Direction.PrevBlankLine;
                break;
            case RawDirection.NextBlankLine:
                direction = Direction.NextBlankLine;
                break;
            case RawDirection.WrappedLineStart:
                direction = Direction.WrappedLineStart;
                break;
            case RawDirection.WrappedLineFirstNonWhitespaceCharacter:
                direction = Direction.WrappedLineFirstNonWhitespaceCharacter;
                break;
            case RawDirection.WrappedLineColumnCenter:
                direction = Direction.WrappedLineColumnCenter;
                break;
            case RawDirection.WrappedLineEnd:
                direction = Direction.WrappedLineEnd;
                break;
            case RawDirection.WrappedLineLastNonWhitespaceCharacter:
                direction = Direction.WrappedLineLastNonWhitespaceCharacter;
                break;
            case RawDirection.ViewPortTop:
                direction = Direction.ViewPortTop;
                break;
            case RawDirection.ViewPortBottom:
                direction = Direction.ViewPortBottom;
                break;
            case RawDirection.ViewPortCenter:
                direction = Direction.ViewPortCenter;
                break;
            case RawDirection.ViewPortIfOutside:
                direction = Direction.ViewPortIfOutside;
                break;
            default:
                // illegal arguments
                return null;
        }

        var unit = Unit.None;
        switch (args.By)
        {
            case RawUnit.Line:
                unit = Unit.Line;
                break;
            case RawUnit.WrappedLine:
                unit = Unit.WrappedLine;
                break;
            case RawUnit.Character:
                unit = Unit.Character;
                break;
            case RawUnit.HalfLine:
                unit = Unit.HalfLine;
                break;
            case RawUnit.FoldedLine:
                unit = Unit.FoldedLine;
                break;
        }

        return new ParsedArguments(
            Direction: direction,
            Unit: unit,
            Select: args.Select ?? false,
            // args.value || 1
            Value: args.Value is { } value && value != 0 ? value : 1,
            NoHistory: args.NoHistory ?? false);
    }

    /// <summary>The parsed arguments of the cursor move command.</summary>
    internal sealed record ParsedArguments(Direction Direction, Unit Unit, bool Select, int Value, bool NoHistory);

    /// <summary>The arguments of a simple move (a direction from <see cref="Direction.Left"/> to <see cref="Direction.WrappedLineLastNonWhitespaceCharacter"/>).</summary>
    internal sealed record SimpleMoveArguments(Direction Direction, Unit Unit, bool Select, int Value);

    internal enum Direction
    {
        Left,
        Right,
        Up,
        Down,
        PrevBlankLine,
        NextBlankLine,

        WrappedLineStart,
        WrappedLineFirstNonWhitespaceCharacter,
        WrappedLineColumnCenter,
        WrappedLineEnd,
        WrappedLineLastNonWhitespaceCharacter,

        ViewPortTop,
        ViewPortCenter,
        ViewPortBottom,

        ViewPortIfOutside,
    }

    internal enum Unit
    {
        None,
        Line,
        WrappedLine,
        Character,
        HalfLine,
        FoldedLine,
    }
}
