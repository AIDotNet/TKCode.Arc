// Ported from vscode (src/vs/editor/common/cursor/cursorAtomicMoveOperations.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the const enum Direction is AtomicTabMoveDirection (Direction alone is too generic in
// this namespace); whitespaceVisibleColumn returns a named tuple instead of a three-element array; the right-move
// loop's currentVisibleColumn is `current` (C# does not allow the name again after the left-move block); Nearest
// compares against tabSize / 2.0, as JavaScript's division is not an integer division.

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Which way an atomic tab move goes.</summary>
internal enum AtomicTabMoveDirection
{
    Left,
    Right,
    Nearest,
}

/// <summary>Cursor moves that treat indentation whitespace as whole tab stops ("sticky tab stops").</summary>
internal static class AtomicTabMoveOperations
{
    /// <summary>
    /// Get the visible column at the position. If we get to a non-whitespace character first or past the end of
    /// string then return -1 (for all three values).
    /// <para><b>Note</b>: <paramref name="position"/> and the return values are 0-based.</para>
    /// </summary>
    public static (int PrevTabStopPosition, int PrevTabStopVisibleColumn, int VisibleColumn) WhitespaceVisibleColumn(
        string lineContent, int position, int tabSize)
    {
        var lineLength = lineContent.Length;
        var visibleColumn = 0;
        var prevTabStopPosition = -1;
        var prevTabStopVisibleColumn = -1;
        for (var i = 0; i < lineLength; i++)
        {
            if (i == position)
            {
                return (prevTabStopPosition, prevTabStopVisibleColumn, visibleColumn);
            }
            if (visibleColumn % tabSize == 0)
            {
                prevTabStopPosition = i;
                prevTabStopVisibleColumn = visibleColumn;
            }
            var chCode = lineContent[i];
            switch (chCode)
            {
                case ' ':
                    visibleColumn += 1;
                    break;
                case '\t':
                    // Skip to the next multiple of tabSize.
                    visibleColumn = CursorColumns.NextRenderTabStop(visibleColumn, tabSize);
                    break;
                default:
                    return (-1, -1, -1);
            }
        }
        if (position == lineLength)
        {
            return (prevTabStopPosition, prevTabStopVisibleColumn, visibleColumn);
        }
        return (-1, -1, -1);
    }

    /// <summary>
    /// Return the position that should result from a move left, right or to the nearest tab, if atomic tabs are
    /// enabled. Left and right are used for the arrow key movements, nearest is used for mouse selection. It returns
    /// -1 if atomic tabs are not relevant and you should fall back to normal behaviour.
    /// <para><b>Note</b>: <paramref name="position"/> and the return value are 0-based.</para>
    /// </summary>
    public static int AtomicPosition(string lineContent, int position, int tabSize, AtomicTabMoveDirection direction)
    {
        var lineLength = lineContent.Length;

        // Get the 0-based visible column corresponding to the position, or return
        // -1 if it is not in the initial whitespace.
        var (prevTabStopPosition, prevTabStopVisibleColumn, visibleColumn) = WhitespaceVisibleColumn(lineContent, position, tabSize);

        if (visibleColumn == -1)
        {
            return -1;
        }

        // Is the output left or right of the current position. The case for nearest
        // where it is the same as the current position is handled in the switch.
        bool left;
        switch (direction)
        {
            case AtomicTabMoveDirection.Left:
                left = true;
                break;
            case AtomicTabMoveDirection.Right:
                left = false;
                break;
            default:
                // The code below assumes the output position is either left or right
                // of the input position. If it is the same, return immediately.
                if (visibleColumn % tabSize == 0)
                {
                    return position;
                }
                // Go to the nearest indentation.
                left = visibleColumn % tabSize <= tabSize / 2.0;
                break;
        }

        // If going left, we can just use the info about the last tab stop position and
        // last tab stop visible column that we computed in the first walk over the whitespace.
        if (left)
        {
            if (prevTabStopPosition == -1)
            {
                return -1;
            }
            // If the direction is left, we need to keep scanning right to ensure
            // that targetVisibleColumn + tabSize is before non-whitespace.
            // This is so that when we press left at the end of a partial
            // indentation it only goes one character. For example '      foo' with
            // tabSize 4, should jump from position 6 to position 5, not 4.
            var currentVisibleColumn = prevTabStopVisibleColumn;
            for (var i = prevTabStopPosition; i < lineLength; ++i)
            {
                if (currentVisibleColumn == prevTabStopVisibleColumn + tabSize)
                {
                    // It is a full indentation.
                    return prevTabStopPosition;
                }

                var chCode = lineContent[i];
                switch (chCode)
                {
                    case ' ':
                        currentVisibleColumn += 1;
                        break;
                    case '\t':
                        currentVisibleColumn = CursorColumns.NextRenderTabStop(currentVisibleColumn, tabSize);
                        break;
                    default:
                        return -1;
                }
            }
            if (currentVisibleColumn == prevTabStopVisibleColumn + tabSize)
            {
                return prevTabStopPosition;
            }
            // It must have been a partial indentation.
            return -1;
        }

        // We are going right.
        var targetVisibleColumn = CursorColumns.NextRenderTabStop(visibleColumn, tabSize);

        // We can just continue from where whitespaceVisibleColumn got to.
        var current = visibleColumn;
        for (var i = position; i < lineLength; i++)
        {
            if (current == targetVisibleColumn)
            {
                return i;
            }

            var chCode = lineContent[i];
            switch (chCode)
            {
                case ' ':
                    current += 1;
                    break;
                case '\t':
                    current = CursorColumns.NextRenderTabStop(current, tabSize);
                    break;
                default:
                    return -1;
            }
        }
        // This condition handles when the target column is at the end of the line.
        if (current == targetVisibleColumn)
        {
            return lineLength;
        }
        return -1;
    }
}
