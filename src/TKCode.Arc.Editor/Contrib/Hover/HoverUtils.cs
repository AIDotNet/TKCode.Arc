// Ported from vscode (src/vs/editor/contrib/hover/browser/hoverUtils.ts; computeDistanceFromPointToRectangle of
// contentHoverWidget.ts; RenderedContentHover.computeHoverPositions of contentHoverRendered.ts, commit b57d5c9e), MIT
// license.
// Differences from upstream:
//  - the element is its bounds (left, top, width, height) instead of an HTMLElement;
//  - the mouse event is its modifier keys;
//  - computeHoverPositions takes the hover editor (its view model for wrapped lines) instead of ICodeEditor.

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>The modifier keys of a pointer or key event.</summary>
internal readonly record struct HoverModifierKeys(bool CtrlKey, bool AltKey, bool MetaKey, bool ShiftKey = false);

/// <summary>Helpers of the hover controller (upstream's hoverUtils.ts).</summary>
internal static class HoverUtils
{
    private const double Padding = 3;

    /// <summary>Whether the point is inside the element, 3 pixels in from its edges (upstream's isMousePositionWithinElement).</summary>
    public static bool IsMousePositionWithinElement(double left, double top, double width, double height, double posx, double posy)
    {
        if (posx < left + Padding
            || posx > left + width - Padding
            || posy < top + Padding
            || posy > top + height - Padding)
        {
            return false;
        }

        return true;
    }

    /// <summary>Whether a hover should show for the pointer (upstream's shouldShowHover).</summary>
    public static bool ShouldShowHover(HoverEnabled hoverEnabled, MultiCursorModifier multiCursorModifier, HoverModifierKeys mouseEvent)
    {
        if (hoverEnabled == HoverEnabled.On)
        {
            return true;
        }

        if (hoverEnabled == HoverEnabled.Off)
        {
            return false;
        }

        return IsTriggerModifierPressed(multiCursorModifier, mouseEvent);
    }

    /// <summary>Whether the modifier that is not the multi-cursor modifier is pressed (upstream's isTriggerModifierPressed).</summary>
    public static bool IsTriggerModifierPressed(MultiCursorModifier multiCursorModifier, HoverModifierKeys e)
    {
        if (multiCursorModifier == MultiCursorModifier.AltKey)
        {
            return e.CtrlKey || e.MetaKey;
        }

        // multiCursorModifier is ctrlKey or metaKey
        return e.AltKey;
    }

    /// <summary>The distance from a point to a rectangle, 0 inside (upstream's computeDistanceFromPointToRectangle).</summary>
    public static double ComputeDistanceFromPointToRectangle(double pointX, double pointY, double left, double top, double width, double height)
    {
        var x = left + (width / 2); // x center of rectangle
        var y = top + (height / 2); // y center of rectangle
        var dx = Math.Max(Math.Abs(pointX - x) - (width / 2), 0);
        var dy = Math.Max(Math.Abs(pointY - y) - (height / 2), 0);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Where a hover over <paramref name="anchorRange"/> with <paramref name="hoverParts"/> shows (upstream's
    /// RenderedContentHover.computeHoverPositions): at the anchor, and as a secondary position the start of the parts on
    /// the anchor's line (not before the anchor's view line on wrapped lines).
    /// </summary>
    public static (TextPosition ShowAtPosition, TextPosition ShowAtSecondaryPosition) ComputeHoverPositions(
        IHoverEditor editor,
        TextRange anchorRange,
        IReadOnlyList<IHoverPart> hoverParts)
    {
        var startColumnBoundary = 1;
        if (editor.Model is not null && editor.ViewModel is { } viewModel)
        {
            // ensure the range is on the current view line
            var coordinatesConverter = viewModel.CoordinatesConverter;
            var anchorViewRange = coordinatesConverter.ConvertModelRangeToViewRange(anchorRange);
            var anchorViewMinColumn = viewModel.GetLineMinColumn(anchorViewRange.StartLineNumber);
            var anchorViewRangeStart = new TextPosition(anchorViewRange.StartLineNumber, anchorViewMinColumn);
            startColumnBoundary = coordinatesConverter.ConvertViewPositionToModelPosition(anchorViewRangeStart).Column;
        }

        // the anchor range is always on a single line
        var anchorStartLineNumber = anchorRange.StartLineNumber;
        var secondaryPositionColumn = anchorRange.StartColumn;
        TextRange? forceShowAtRange = null;

        foreach (var hoverPart in hoverParts)
        {
            var hoverPartRange = hoverPart.Range;
            var hoverPartRangeOnAnchorStartLine = hoverPartRange.StartLineNumber == anchorStartLineNumber;
            var hoverPartRangeOnAnchorEndLine = hoverPartRange.EndLineNumber == anchorStartLineNumber;
            var hoverPartRangeIsOnAnchorLine = hoverPartRangeOnAnchorStartLine && hoverPartRangeOnAnchorEndLine;
            if (hoverPartRangeIsOnAnchorLine)
            {
                // this message has a range that is completely sitting on the line of the anchor
                var hoverPartStartColumn = hoverPartRange.StartColumn;
                var minSecondaryPositionColumn = Math.Min(secondaryPositionColumn, hoverPartStartColumn);
                secondaryPositionColumn = Math.Max(minSecondaryPositionColumn, startColumnBoundary);
            }

            if (hoverPart.ForceShowAtRange)
            {
                forceShowAtRange = hoverPartRange;
            }
        }

        if (forceShowAtRange is { } force)
        {
            var forceShowAtPosition = force.Start;
            return (forceShowAtPosition, forceShowAtPosition);
        }

        return (anchorRange.Start, new TextPosition(anchorStartLineNumber, secondaryPositionColumn));
    }
}
