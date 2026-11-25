// The filler of an alignment view zone (upstream's createFakeLinesDiv with the diagonal-fill background of
// src/vs/editor/browser/widget/diffEditor/style.css, commit b57d5c9e; MIT license): diagonal lines where the other
// side has lines this side does not.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The space on the shorter side of a change, filled with diagonal lines in the theme's empty-lines brush.</summary>
internal sealed class DiffEditorEmptyLinesView : Control
{
    private const double Spacing = 8;

    private readonly DiffEditor _owner;

    public DiffEditorEmptyLinesView(DiffEditor owner)
    {
        _owner = owner;
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_owner.EmptyLinesBrush is not { } brush || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var pen = new Pen(brush, 1);
        var width = Bounds.Width;
        var height = Bounds.Height;
        for (var x = -height; x < width; x += Spacing)
        {
            context.DrawLine(pen, new Point(x, height), new Point(x + height, 0));
        }
    }
}
