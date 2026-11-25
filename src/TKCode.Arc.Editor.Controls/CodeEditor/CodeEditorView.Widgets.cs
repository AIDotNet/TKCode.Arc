// Where content and overlay widgets go (upstream's ContentWidgets._prepareRenderWidget, _layoutBoxInViewport,
// _getAnchorsCoordinates and _reduceAnchorCoordinates in src/vs/editor/browser/viewParts/contentWidgets/
// contentWidgets.ts, and OverlayWidgets._renderWidget in viewParts/overlayWidgets/overlayWidgets.ts; commit b57d5c9e),
// MIT license. Differences from upstream: coordinates are relative to the view (upstream: to the scrolled lines
// content); a widget is clamped to the text area horizontally; the overlay widgets' right edge is left of the vertical
// scroll bar when it shows (upstream: verticalScrollbarWidth + minimap width).

using Avalonia;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>
    /// Where a content widget of <paramref name="size"/> goes for <paramref name="position"/>: the first preferred
    /// placement it fits in, else the first preferred one; null when its line is not on screen.
    /// </summary>
    internal ContentWidgetPlacement? PlaceContentWidget(ContentWidgetPosition position, Size size)
    {
        if (_viewModel is not { } viewModel || position.Position is not { } modelPosition || position.Preference.Count == 0)
        {
            return null;
        }

        var converter = viewModel.CoordinatesConverter;
        var affinity = position.PositionAffinity ?? PositionAffinity.None;
        var primary = converter.ConvertModelPositionToViewPosition(_editor!.Model.ValidatePosition(modelPosition), affinity);
        var (startLine, endLine) = GetVisibleLines();
        if (primary.LineNumber < startLine || primary.LineNumber > endLine)
        {
            // outside of viewport
            return null;
        }

        var anchorTop = GetY(primary.LineNumber);
        var anchorHeight = viewModel.ViewLayout.GetLineHeightForLineNumber(primary.LineNumber);
        var anchorLeft = primary.Column == 1 && affinity == PositionAffinity.LeftOfInjectedText ? GutterWidth : GetX(primary.LineNumber, primary.Column);

        // _reduceAnchorCoordinates: touch the secondary anchor on the same line if the widget can
        if (position.SecondaryPosition is { } secondaryModel)
        {
            var secondary = converter.ConvertModelPositionToViewPosition(_editor.Model.ValidatePosition(secondaryModel), affinity);
            if (secondary.LineNumber == primary.LineNumber)
            {
                var left = GetX(secondary.LineNumber, secondary.Column);
                var fullwidth = _charWidth * 2;
                anchorLeft = left < anchorLeft
                    ? Math.Max(left, anchorLeft - size.Width + fullwidth)
                    : Math.Min(left, anchorLeft + size.Width - fullwidth);
            }
        }

        // _layoutBoxInViewport: the boxes above and below the line
        var aboveTop = anchorTop - size.Height;
        var fitsAbove = anchorTop >= size.Height;
        var belowTop = anchorTop + anchorHeight;
        var fitsBelow = Bounds.Height - belowTop >= size.Height;

        var textLeft = GutterWidth;
        var placedLeft = anchorLeft;
        if (placedLeft + size.Width > Bounds.Width)
        {
            placedLeft = Bounds.Width - size.Width;
        }

        if (placedLeft < textLeft)
        {
            placedLeft = textLeft;
        }

        // two passes: first for a perfect fit, then the first preference
        for (var pass = 1; pass <= 2; pass++)
        {
            foreach (var pref in position.Preference)
            {
                switch (pref)
                {
                    case ContentWidgetPositionPreference.Above when pass == 2 || fitsAbove:
                        return new ContentWidgetPlacement(new Rect(placedLeft, aboveTop, size.Width, size.Height), pref);
                    case ContentWidgetPositionPreference.Below when pass == 2 || fitsBelow:
                        return new ContentWidgetPlacement(new Rect(placedLeft, belowTop, size.Width, size.Height), pref);
                    case ContentWidgetPositionPreference.Exact:
                        return new ContentWidgetPlacement(new Rect(anchorLeft, anchorTop, size.Width, size.Height), pref);
                }
            }
        }

        return null;
    }

    /// <summary>Where an overlay widget of <paramref name="size"/> goes in a host of <paramref name="hostSize"/>.</summary>
    internal Rect PlaceOverlayWidget(OverlayWidgetPosition? position, Size size, Size hostSize)
    {
        if (position?.Coordinates is { } coordinates)
        {
            return new Rect(coordinates, size);
        }

        var right = hostSize.Width - OverlayRightInset;
        return position?.Preference switch
        {
            OverlayWidgetPositionPreference.TopRightCorner => new Rect(right - size.Width, 0, size.Width, size.Height),
            OverlayWidgetPositionPreference.BottomRightCorner => new Rect(right - size.Width, hostSize.Height - size.Height - OverlayBottomInset, size.Width, size.Height),
            OverlayWidgetPositionPreference.TopCenter => new Rect(GutterWidth + ((hostSize.Width - GutterWidth - size.Width) / 2), 0, size.Width, size.Height),
            _ => new Rect(0, 0, size.Width, size.Height),
        };
    }

    // the room the scroll bars take at the right and bottom edges when they show
    private double OverlayRightInset => _verticalScrollBar is { IsVisible: true } bar ? bar.Bounds.Width : 0;

    private double OverlayBottomInset => _horizontalScrollBar is { IsVisible: true } bar ? bar.Bounds.Height : 0;
}
